namespace Test.Shared.Helpers
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Deterministic OpenAI-compatible model server on 127.0.0.1 for tests. Behavior is scripted by model name:
    /// stub-chat (text "ready"; tool requests return submit_result {"answer":"ready"}),
    /// stub-repair (an empty answer until the request carries a repair turn, then a valid one),
    /// stub-429 (the first sighting of each distinct request body returns 429; the identical retry succeeds),
    /// stub-error (always 400), stub-refusal (finish_reason refusal),
    /// stub-embed-N (embeddings of N dimensions).
    /// Requests whose system prompt contains "JSON Schema" receive the JSON answer as fenced text. Streaming requests receive SSE chunks
    /// (text or tool-call argument deltas split in two) and a final chunk with usage including cached and reasoning tokens.
    /// Thread safety: safe for concurrent requests.
    /// </summary>
    public class StubModelServer : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Base URL including /v1.
        /// </summary>
        public string BaseUrl { get; }

        /// <summary>
        /// Request bodies received, in order.
        /// </summary>
        public ConcurrentQueue<string> Requests { get; } = new ConcurrentQueue<string>();

        #endregion

        #region Private-Members

        private static readonly Lazy<StubModelServer> _Shared = new Lazy<StubModelServer>(() => new StubModelServer());
        private readonly HttpListener _Listener = new HttpListener();
        private readonly ConcurrentDictionary<string, int> _Counts = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, byte> _Seen = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        private readonly CancellationTokenSource _Stop = new CancellationTokenSource();
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Start a server on a free port.
        /// </summary>
        public StubModelServer()
        {
            int port = PortAllocator.GetFreePort();
            _Listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
            _Listener.Start();
            BaseUrl = "http://127.0.0.1:" + port + "/v1";
            Task.Run(() => LoopAsync(_Stop.Token));
        }

        /// <summary>
        /// Shared instance for the test process.
        /// </summary>
        public static StubModelServer Shared
        {
            get { return _Shared.Value; }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Number of chat requests received for a scenario tag (the model name).
        /// </summary>
        /// <param name="model">Model name.</param>
        /// <returns>Count.</returns>
        public int Count(string model)
        {
            return _Counts.TryGetValue(model, out int c) ? c : 0;
        }

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        /// <param name="disposing">True when called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing)
            {
                _Stop.Cancel();
                _Listener.Close();
                _Stop.Dispose();
            }

            _Disposed = true;
        }

        #endregion

        #region Private-Methods

        private async Task LoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await _Listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    return;
                }

                _ = Task.Run(() => HandleAsync(ctx));
            }
        }

        private async Task HandleAsync(HttpListenerContext ctx)
        {
            try
            {
                string body;
                using (StreamReader reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                {
                    body = await reader.ReadToEndAsync().ConfigureAwait(false);
                }

                string path = ctx.Request.Url?.AbsolutePath ?? String.Empty;
                if (path.EndsWith("/models", StringComparison.Ordinal))
                {
                    await WriteAsync(ctx, 200, "{\"object\":\"list\",\"data\":[{\"id\":\"stub-chat\",\"object\":\"model\"},{\"id\":\"stub-embed-8\",\"object\":\"model\"}]}").ConfigureAwait(false);
                    return;
                }

                Requests.Enqueue(body);
                using (JsonDocument doc = JsonDocument.Parse(String.IsNullOrWhiteSpace(body) ? "{}" : body))
                {
                    JsonElement root = doc.RootElement;
                    string model = root.TryGetProperty("model", out JsonElement m) ? m.GetString() ?? "stub-chat" : "stub-chat";
                    int count = _Counts.AddOrUpdate(model, 1, (k, v) => v + 1);

                    if (path.EndsWith("/embeddings", StringComparison.Ordinal))
                    {
                        int dims = model.StartsWith("stub-embed-", StringComparison.Ordinal) && Int32.TryParse(model.Substring(11), out int d) ? d : 8;
                        int n = root.TryGetProperty("input", out JsonElement input) && input.ValueKind == JsonValueKind.Array ? input.GetArrayLength() : 1;
                        StringBuilder data = new StringBuilder();
                        for (int i = 0; i < n; i++)
                        {
                            if (i > 0) data.Append(',');
                            data.Append("{\"object\":\"embedding\",\"index\":").Append(i).Append(",\"embedding\":[")
                                .Append(String.Join(",", Enumerable.Range(0, dims).Select(j => ((i + 1) * 0.01 + j * 0.001).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture))))
                                .Append("]}");
                        }

                        await WriteAsync(ctx, 200, "{\"object\":\"list\",\"model\":\"" + model + "\",\"data\":[" + data + "],\"usage\":{\"prompt_tokens\":" + n + ",\"total_tokens\":" + n + "}}").ConfigureAwait(false);
                        return;
                    }

                    if (model == "stub-error")
                    {
                        await WriteAsync(ctx, 400, "{\"error\":{\"message\":\"stub rejected the request\",\"type\":\"invalid_request_error\"}}").ConfigureAwait(false);
                        return;
                    }

                    if (model == "stub-429" && _Seen.TryAdd(body, 0))
                    {
                        await WriteAsync(ctx, 429, "{\"error\":{\"message\":\"rate limited\",\"type\":\"rate_limit\"}}").ConfigureAwait(false);
                        return;
                    }

                    bool tools = root.TryGetProperty("tools", out JsonElement t) && t.ValueKind == JsonValueKind.Array && t.GetArrayLength() > 0;
                    bool stream = root.TryGetProperty("stream", out JsonElement st) && st.ValueKind == JsonValueKind.True;
                    bool jsonMode = body.Contains("JSON Schema", StringComparison.Ordinal);
                    bool repairTurn = body.Contains("\"role\":\"tool\"", StringComparison.Ordinal) || body.Contains("did not pass validation", StringComparison.OrdinalIgnoreCase);
                    string answer = model == "stub-repair" && !repairTurn ? "" : "ready";
                    bool toolTurn = tools && model != "stub-refusal";
                    string finish = model == "stub-refusal" ? "refusal" : toolTurn ? "tool_calls" : "stop";
                    string text = jsonMode ? "Here you go:\n```json\n{\"answer\":\"" + answer + "\"}\n```" : "ready";
                    string args = "{\"answer\":\"" + answer + "\"}";
                    string usage = "{\"prompt_tokens\":12,\"completion_tokens\":3,\"total_tokens\":15,\"prompt_tokens_details\":{\"cached_tokens\":4},\"completion_tokens_details\":{\"reasoning_tokens\":1}}";

                    if (stream)
                    {
                        List<string> events = new List<string>();
                        string head = "{\"id\":\"stub-" + count + "\",\"object\":\"chat.completion.chunk\",\"model\":\"" + model + "\",\"choices\":[{\"index\":0,\"delta\":";
                        if (toolTurn)
                        {
                            string half = args.Substring(0, args.Length / 2);
                            string rest = args.Substring(args.Length / 2);
                            events.Add(head + "{\"role\":\"assistant\",\"tool_calls\":[{\"index\":0,\"id\":\"call_" + count + "\",\"type\":\"function\",\"function\":{\"name\":\"submit_result\",\"arguments\":" + JsonSerializer.Serialize(half) + "}}]},\"finish_reason\":null}]}");
                            events.Add(head + "{\"tool_calls\":[{\"index\":0,\"function\":{\"arguments\":" + JsonSerializer.Serialize(rest) + "}}]},\"finish_reason\":null}]}");
                        }
                        else
                        {
                            int cut = text.Length / 2;
                            events.Add(head + "{\"role\":\"assistant\",\"content\":" + JsonSerializer.Serialize(text.Substring(0, cut)) + "},\"finish_reason\":null}]}");
                            events.Add(head + "{\"content\":" + JsonSerializer.Serialize(text.Substring(cut)) + "},\"finish_reason\":null}]}");
                        }

                        events.Add(head + "{},\"finish_reason\":\"" + finish + "\"}],\"usage\":" + usage + "}");
                        await WriteSseAsync(ctx, events).ConfigureAwait(false);
                        return;
                    }

                    string message = toolTurn
                        ? "{\"role\":\"assistant\",\"content\":null,\"tool_calls\":[{\"id\":\"call_" + count + "\",\"type\":\"function\",\"function\":{\"name\":\"submit_result\",\"arguments\":" + JsonSerializer.Serialize(args) + "}}]}"
                        : "{\"role\":\"assistant\",\"content\":" + JsonSerializer.Serialize(text) + "}";
                    string response = "{\"id\":\"stub-" + count + "\",\"object\":\"chat.completion\",\"model\":\"" + model + "\",\"choices\":[{\"index\":0,\"message\":" + message +
                        ",\"finish_reason\":\"" + finish + "\"}],\"usage\":" + usage + "}";
                    await WriteAsync(ctx, 200, response).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    await WriteAsync(ctx, 500, "{\"error\":{\"message\":" + JsonSerializer.Serialize(ex.Message) + "}}").ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            }
        }

        private static async Task WriteSseAsync(HttpListenerContext ctx, List<string> events)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.SendChunked = true;
            foreach (string e in events)
            {
                byte[] data = Encoding.UTF8.GetBytes("data: " + e + "\n\n");
                await ctx.Response.OutputStream.WriteAsync(data).ConfigureAwait(false);
                await ctx.Response.OutputStream.FlushAsync().ConfigureAwait(false);
                await Task.Delay(5).ConfigureAwait(false);
            }

            byte[] done = Encoding.UTF8.GetBytes("data: [DONE]\n\n");
            await ctx.Response.OutputStream.WriteAsync(done).ConfigureAwait(false);
            ctx.Response.Close();
        }

        private static async Task WriteAsync(HttpListenerContext ctx, int status, string json)
        {
            byte[] data = Encoding.UTF8.GetBytes(json);
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = data.Length;
            await ctx.Response.OutputStream.WriteAsync(data).ConfigureAwait(false);
            ctx.Response.Close();
        }

        #endregion
    }
}
