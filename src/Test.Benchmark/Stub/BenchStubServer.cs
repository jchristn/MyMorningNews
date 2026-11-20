namespace MyMorningNews.Benchmark.Stub
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Benchmark.Datasets;
    using MyMorningNews.Benchmark.Reference;
    using MyMorningNews.Benchmark.Reporting;

    /// <summary>
    /// One loopback HTTP server that stands in for every expensive or external dependency, speaking the same protocols
    /// the real ones do so the product cannot tell the difference: an OpenAI-compatible model (chat with streaming and
    /// tool calls, embeddings, models), a SearXNG search endpoint per corpus round, the corpus pages and robots.txt, and
    /// OpenAI-compatible speech. Routes:
    /// POST /v1/chat/completions, POST /v1/embeddings, GET /v1/models, POST /v1/audio/speech,
    /// GET /search/{corpus}/r{round}/search?q=..., GET /corpus/{corpus}/{item}.html, GET /robots.txt,
    /// GET /_stub/state, POST /_stub/faults?target=Model&amp;mode=Down (manual testing), anything else 404.
    /// Thread safety: safe for concurrent requests.
    /// </summary>
    public class BenchStubServer : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Base URL, such as http://127.0.0.1:12503.
        /// </summary>
        public string BaseUrl { get; }

        /// <summary>
        /// OpenAI-compatible base URL (BaseUrl + /v1).
        /// </summary>
        public string ModelUrl
        {
            get { return BaseUrl + "/v1"; }
        }

        /// <summary>
        /// The chat model name the harness assigns to every task.
        /// </summary>
        public const string ChatModel = "bench-stub-chat";

        /// <summary>
        /// The embedding model name.
        /// </summary>
        public const string EmbeddingModel = "bench-hash-384";

        /// <summary>
        /// Embedding dimensions.
        /// </summary>
        public const int EmbeddingDimensions = 384;

        /// <summary>
        /// Stub behavior version; part of every workspace hash, so a change to the stub gets fresh workspaces.
        /// </summary>
        public const string Version = "stub-2";

        /// <summary>
        /// Fault injection.
        /// </summary>
        public StubFaults Faults { get; } = new StubFaults();

        /// <summary>
        /// What the stub saw.
        /// </summary>
        public StubLedger Ledger { get; } = new StubLedger();

        /// <summary>
        /// The stub model.
        /// </summary>
        public StubModelBrain Brain { get; }

        #endregion

        #region Private-Members

        private static readonly Regex _Topic = new Regex(@"Topic: ([^\n]+)", RegexOptions.Compiled);
        private static readonly Regex _SearchPath = new Regex(@"^/search/([a-z0-9-]+)/r([0-9]+)/search$", RegexOptions.Compiled);
        private readonly HttpListener _Listener = new HttpListener();
        private readonly CancellationTokenSource _Stop = new CancellationTokenSource();
        private readonly DatasetIndex? _Index;
        private readonly StubSearch? _Search;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Start listening on 127.0.0.1.
        /// </summary>
        /// <param name="port">Port, 1 to 65535.</param>
        /// <param name="dataset">Dataset whose corpora are served, or null to serve only the model and speech routes.</param>
        /// <exception cref="ArgumentOutOfRangeException">port is out of range.</exception>
        /// <exception cref="HttpListenerException">The port is in use.</exception>
        public BenchStubServer(int port, BenchmarkDataset? dataset)
        {
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            BaseUrl = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
            _Index = dataset == null ? null : new DatasetIndex(dataset, BaseUrl);
            _Search = _Index == null ? null : new StubSearch(_Index);
            Brain = new StubModelBrain(_Index, Ledger);
            _Listener.Prefixes.Add(BaseUrl + "/");
            _Listener.Start();
            Task.Run(() => LoopAsync(_Stop.Token));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// The index of the served dataset, or null.
        /// </summary>
        /// <returns>Index.</returns>
        public DatasetIndex? Index()
        {
            return _Index;
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
                try
                {
                    _Listener.Close();
                }
                catch (ObjectDisposedException)
                {
                }

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
                catch (Exception ex) when (ex is HttpListenerException || ex is ObjectDisposedException || ex is InvalidOperationException)
                {
                    return;
                }

                _ = Task.Run(() => HandleAsync(ctx, token));
            }
        }

        private async Task HandleAsync(HttpListenerContext ctx, CancellationToken token)
        {
            try
            {
                string body;
                using (StreamReader reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                {
                    body = await reader.ReadToEndAsync(token).ConfigureAwait(false);
                }

                string path = ctx.Request.Url?.AbsolutePath ?? String.Empty;
                string method = ctx.Request.HttpMethod.ToUpperInvariant();
                if (path == "/robots.txt") { Ledger.Count("robots"); await WriteAsync(ctx, 200, "text/plain", "User-agent: *\nAllow: /\n", token).ConfigureAwait(false); return; }
                if (path == "/_stub/state") { await WriteJsonAsync(ctx, 200, State(), token).ConfigureAwait(false); return; }
                if (path == "/_stub/faults" && method == "POST") { await FaultsAsync(ctx, token).ConfigureAwait(false); return; }
                if (path.EndsWith("/chat/completions", StringComparison.Ordinal) && method == "POST") { await ChatAsync(ctx, body, token).ConfigureAwait(false); return; }
                if (path.EndsWith("/embeddings", StringComparison.Ordinal) && method == "POST") { await EmbeddingsAsync(ctx, body, token).ConfigureAwait(false); return; }
                if (path.EndsWith("/audio/speech", StringComparison.Ordinal) && method == "POST") { await SpeechAsync(ctx, body, token).ConfigureAwait(false); return; }
                if (path.EndsWith("/models", StringComparison.Ordinal))
                {
                    Ledger.Count("models");
                    await WriteJsonAsync(ctx, 200, new { @object = "list", data = new[] { new { id = ChatModel, @object = "model" }, new { id = EmbeddingModel, @object = "model" } } }, token).ConfigureAwait(false);
                    return;
                }

                Match search = _SearchPath.Match(path);
                if (search.Success && method == "GET") { await SearchAsync(ctx, search.Groups[1].Value, Int32.Parse(search.Groups[2].Value, CultureInfo.InvariantCulture), token).ConfigureAwait(false); return; }
                if (path.StartsWith("/search/", StringComparison.Ordinal) && path.EndsWith("/search", StringComparison.Ordinal))
                {
                    Ledger.Count("search");
                    await WriteJsonAsync(ctx, 200, new { query = ctx.Request.QueryString["q"] ?? String.Empty, results = new List<object>() }, token).ConfigureAwait(false);
                    return;
                }

                if (_Index != null && _Index.TryPage(path, out string corpus, out string item)) { await PageAsync(ctx, corpus, item, token).ConfigureAwait(false); return; }
                Ledger.Count("not-found");
                await WriteJsonAsync(ctx, 404, new { error = "not found", path }, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                try
                {
                    await WriteJsonAsync(ctx, 500, new { error = new { message = ex.Message } }, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception inner) when (inner is HttpListenerException || inner is ObjectDisposedException || inner is InvalidOperationException || inner is IOException)
                {
                }
            }
        }

        private async Task<bool> FaultAsync(HttpListenerContext ctx, FaultTarget target, string key, CancellationToken token)
        {
            FaultMode mode = Faults.Get(target);
            if (mode == FaultMode.Slow) await Task.Delay(Faults.SlowLatencyMs, token).ConfigureAwait(false);
            int status = Faults.Decide(target, key);
            if (status == 0) return false;
            if (status == 429) ctx.Response.AddHeader("Retry-After", "1");
            await WriteJsonAsync(ctx, status, new { error = new { message = status == 429 ? "rate limited (stub fault)" : "unavailable (stub fault)", type = status == 429 ? "rate_limit_error" : "server_error" } }, token).ConfigureAwait(false);
            return true;
        }

        private async Task ChatAsync(HttpListenerContext ctx, string body, CancellationToken token)
        {
            Ledger.Count("chat");
            ParsedChat chat;
            using (JsonDocument doc = JsonDocument.Parse(String.IsNullOrWhiteSpace(body) ? "{}" : body))
            {
                chat = ParsedChat.Parse(doc.RootElement);
            }

            Match topic = _Topic.Match(chat.User);
            StubChatRecord record = new StubChatRecord
            {
                Tag = Ledger.CurrentTag,
                Topic = topic.Success ? topic.Groups[1].Value.Trim() : String.Empty,
                Model = chat.Model,
                Streamed = chat.Stream,
                Guardrail = Brain.ExpectedGuardrail.Length == 0 || chat.System.StartsWith(Brain.ExpectedGuardrail, StringComparison.Ordinal)
            };

            if (Faults.BaseModelLatencyMs > 0) await Task.Delay(Faults.BaseModelLatencyMs, token).ConfigureAwait(false);
            if (await FaultAsync(ctx, FaultTarget.Model, body, token).ConfigureAwait(false))
            {
                record.Prompt = "fault";
                record.Status = Faults.Get(FaultTarget.Model) == FaultMode.Down ? 503 : 429;
                Ledger.Chats.Enqueue(record);
                return;
            }

            StubAnswer answer = Brain.Answer(chat);
            record.Prompt = answer.Prompt;
            Ledger.Chats.Enqueue(record);
            string id = "bench-" + StableHashHex(body);
            bool toolTurn = chat.Tools && answer.Arguments != null;
            string text = answer.Arguments != null && !toolTurn ? "```json\n" + answer.Arguments + "\n```" : answer.Text;
            int promptTokens = Math.Max(1, (chat.System.Length + chat.User.Length) / 4);
            int completionTokens = Math.Max(1, (answer.Arguments ?? answer.Text).Length / 4);
            object usage = new { prompt_tokens = promptTokens, completion_tokens = completionTokens, total_tokens = promptTokens + completionTokens };
            string finish = toolTurn ? "tool_calls" : "stop";

            if (chat.Stream)
            {
                List<string> events = new List<string>();
                if (toolTurn)
                {
                    string args = answer.Arguments!;
                    string half = args.Substring(0, args.Length / 2);
                    string rest = args.Substring(args.Length / 2);
                    events.Add(Chunk(id, chat.Model, new { role = "assistant", tool_calls = new[] { new { index = 0, id = "call_" + id, type = "function", function = new { name = answer.ToolName, arguments = half } } } }, null, null));
                    events.Add(Chunk(id, chat.Model, new { tool_calls = new[] { new { index = 0, function = new { arguments = rest } } } }, null, null));
                }
                else
                {
                    int cut = text.Length / 2;
                    events.Add(Chunk(id, chat.Model, new { role = "assistant", content = text.Substring(0, cut) }, null, null));
                    events.Add(Chunk(id, chat.Model, new { content = text.Substring(cut) }, null, null));
                }

                events.Add(Chunk(id, chat.Model, new { }, finish, usage));
                await WriteSseAsync(ctx, events, token).ConfigureAwait(false);
                return;
            }

            object message = toolTurn
                ? new { role = "assistant", content = (string?)null, tool_calls = new[] { new { id = "call_" + id, type = "function", function = new { name = answer.ToolName, arguments = answer.Arguments } } } }
                : (object)new { role = "assistant", content = text };
            await WriteJsonAsync(ctx, 200, new { id, @object = "chat.completion", model = chat.Model, choices = new[] { new { index = 0, message, finish_reason = finish } }, usage }, token).ConfigureAwait(false);
        }

        private async Task EmbeddingsAsync(HttpListenerContext ctx, string body, CancellationToken token)
        {
            Ledger.Count("embeddings");
            if (await FaultAsync(ctx, FaultTarget.Embeddings, body, token).ConfigureAwait(false)) return;
            List<string> texts = new List<string>();
            string model = EmbeddingModel;
            using (JsonDocument doc = JsonDocument.Parse(String.IsNullOrWhiteSpace(body) ? "{}" : body))
            {
                JsonElement root = doc.RootElement;
                if (root.TryGetProperty("model", out JsonElement m) && m.ValueKind == JsonValueKind.String) model = m.GetString() ?? EmbeddingModel;
                if (root.TryGetProperty("input", out JsonElement input))
                {
                    if (input.ValueKind == JsonValueKind.Array) foreach (JsonElement e in input.EnumerateArray()) texts.Add(e.GetString() ?? String.Empty);
                    else if (input.ValueKind == JsonValueKind.String) texts.Add(input.GetString() ?? String.Empty);
                }
            }

            List<object> data = new List<object>();
            for (int i = 0; i < texts.Count; i++) data.Add(new { @object = "embedding", index = i, embedding = TextTools.Vector(texts[i], EmbeddingDimensions) });
            int tokens = texts.Sum(t => Math.Max(1, t.Length / 4));
            await WriteJsonAsync(ctx, 200, new { @object = "list", model, data, usage = new { prompt_tokens = tokens, total_tokens = tokens } }, token).ConfigureAwait(false);
        }

        private async Task SpeechAsync(HttpListenerContext ctx, string body, CancellationToken token)
        {
            Ledger.Count("speech");
            if (await FaultAsync(ctx, FaultTarget.Tts, body, token).ConfigureAwait(false)) return;
            string input = String.Empty;
            using (JsonDocument doc = JsonDocument.Parse(String.IsNullOrWhiteSpace(body) ? "{}" : body))
            {
                if (doc.RootElement.TryGetProperty("input", out JsonElement ie) && ie.ValueKind == JsonValueKind.String) input = ie.GetString() ?? String.Empty;
            }

            byte[] wav = WavWriter.Tone(Math.Max(0.05, input.Length / 400.0), 440, 8000);
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "audio/wav";
            ctx.Response.ContentLength64 = wav.Length;
            await ctx.Response.OutputStream.WriteAsync(wav, token).ConfigureAwait(false);
            ctx.Response.Close();
        }

        private async Task SearchAsync(HttpListenerContext ctx, string corpus, int round, CancellationToken token)
        {
            Ledger.Count("search");
            string query = ctx.Request.QueryString["q"] ?? String.Empty;
            if (await FaultAsync(ctx, FaultTarget.Search, corpus + "|" + round + "|" + query, token).ConfigureAwait(false)) return;
            if (_Search == null || _Index?.Corpus(corpus) == null)
            {
                await WriteJsonAsync(ctx, 404, new { error = "unknown corpus " + corpus }, token).ConfigureAwait(false);
                return;
            }

            await WriteJsonAsync(ctx, 200, _Search.Response(corpus, round, query, DateTime.UtcNow), token).ConfigureAwait(false);
        }

        private async Task PageAsync(HttpListenerContext ctx, string corpus, string itemId, CancellationToken token)
        {
            Ledger.Count("page");
            if (await FaultAsync(ctx, FaultTarget.Pages, corpus + "|" + itemId, token).ConfigureAwait(false)) return;
            BenchmarkItem item = _Index!.ContentItem(corpus, itemId)!;
            StringBuilder html = new StringBuilder();
            html.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>").Append(WebUtility.HtmlEncode(item.Title)).Append("</title></head><body>");
            html.Append("<nav><a href=\"/\">Harbor Wire</a> | <a href=\"/about\">About</a></nav><main><article><h1>").Append(WebUtility.HtmlEncode(item.Title)).Append("</h1>");
            foreach (string p in item.Paragraphs) html.Append("<p>").Append(WebUtility.HtmlEncode(p)).Append("</p>");
            html.Append("</article></main><footer>Copyright 2026 Harbor Wire (fictional, benchmark corpus).</footer></body></html>");
            await WriteAsync(ctx, 200, "text/html; charset=utf-8", html.ToString(), token).ConfigureAwait(false);
        }

        private async Task FaultsAsync(HttpListenerContext ctx, CancellationToken token)
        {
            string? target = ctx.Request.QueryString["target"];
            string? mode = ctx.Request.QueryString["mode"];
            if (target == "all" && mode == "None") Faults.Clear();
            else if (Enum.TryParse(target, true, out FaultTarget t) && Enum.TryParse(mode, true, out FaultMode m)) Faults.Set(t, m);
            else
            {
                await WriteJsonAsync(ctx, 400, new { error = "target (Model, Embeddings, Search, Pages, Tts, or all) and mode (None, Slow, Down, RateLimited, RateLimitedOnce) are required" }, token).ConfigureAwait(false);
                return;
            }

            await WriteJsonAsync(ctx, 200, State(), token).ConfigureAwait(false);
        }

        private object State()
        {
            return new
            {
                version = Version,
                requests = Ledger.Requests.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => kv.Value),
                faults = Enum.GetValues<FaultTarget>().ToDictionary(t => t.ToString(), t => Faults.Get(t).ToString()),
                injected = Faults.InjectedCounts()
            };
        }

        private static string Chunk(string id, string model, object delta, string? finish, object? usage)
        {
            object chunk = usage == null
                ? new { id, @object = "chat.completion.chunk", model, choices = new[] { new { index = 0, delta, finish_reason = finish } } }
                : (object)new { id, @object = "chat.completion.chunk", model, choices = new[] { new { index = 0, delta, finish_reason = finish } }, usage };
            return Json.Serialize(chunk);
        }

        private static async Task WriteSseAsync(HttpListenerContext ctx, List<string> events, CancellationToken token)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.SendChunked = true;
            foreach (string e in events)
            {
                byte[] data = Encoding.UTF8.GetBytes("data: " + e + "\n\n");
                await ctx.Response.OutputStream.WriteAsync(data, token).ConfigureAwait(false);
                await ctx.Response.OutputStream.FlushAsync(token).ConfigureAwait(false);
            }

            byte[] done = Encoding.UTF8.GetBytes("data: [DONE]\n\n");
            await ctx.Response.OutputStream.WriteAsync(done, token).ConfigureAwait(false);
            ctx.Response.Close();
        }

        private static Task WriteJsonAsync(HttpListenerContext ctx, int status, object value, CancellationToken token)
        {
            return WriteAsync(ctx, status, "application/json", Json.Serialize(value), token);
        }

        private static async Task WriteAsync(HttpListenerContext ctx, int status, string contentType, string content, CancellationToken token)
        {
            byte[] data = Encoding.UTF8.GetBytes(content);
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = contentType;
            ctx.Response.ContentLength64 = data.Length;
            await ctx.Response.OutputStream.WriteAsync(data, token).ConfigureAwait(false);
            ctx.Response.Close();
        }

        private static string StableHashHex(string text)
        {
            return Metrics.StableHash.Fnv1a(text).ToString("x8", CultureInfo.InvariantCulture);
        }

        #endregion
    }
}
