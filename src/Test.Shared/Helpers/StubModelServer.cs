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
    /// stub-embed-N (embeddings of N dimensions), stub-refine (shaped answers for every refinement prompt, detected from the prompt text).
    /// GET requests other than /models return an RSS document (200), or 404 when the path contains "missing" (for source checks).
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

        /// <summary>
        /// Expo push request bodies received at /push/send. Tokens containing "dead" get a DeviceNotRegistered ticket.
        /// </summary>
        public ConcurrentQueue<string> PushRequests { get; } = new ConcurrentQueue<string>();

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
                if (path.EndsWith("/push/send", StringComparison.Ordinal))
                {
                    PushRequests.Enqueue(body);
                    StringBuilder tickets = new StringBuilder();
                    using (JsonDocument push = JsonDocument.Parse(String.IsNullOrWhiteSpace(body) ? "[]" : body))
                    {
                        foreach (JsonElement message in push.RootElement.EnumerateArray())
                        {
                            if (tickets.Length > 0) tickets.Append(',');
                            string to = message.TryGetProperty("to", out JsonElement t0) ? t0.GetString() ?? String.Empty : String.Empty;
                            tickets.Append(to.Contains("dead", StringComparison.Ordinal)
                                ? "{\"status\":\"error\",\"message\":\"not registered\",\"details\":{\"error\":\"DeviceNotRegistered\"}}"
                                : "{\"status\":\"ok\",\"id\":\"" + Guid.NewGuid().ToString("N") + "\"}");
                        }
                    }

                    await WriteAsync(ctx, 200, "{\"data\":[" + tickets + "]}").ConfigureAwait(false);
                    return;
                }

                if (path.EndsWith("/tavily/search", StringComparison.Ordinal))
                {
                    string auth = ctx.Request.Headers["Authorization"] ?? String.Empty;
                    if (!auth.Contains("tvly-test", StringComparison.Ordinal)) { await WriteAsync(ctx, 401, "{\"detail\":\"bad key\"}").ConfigureAwait(false); return; }
                    await WriteAsync(ctx, 200, "{\"query\":\"q\",\"results\":[{\"title\":\"Tavily one\",\"url\":\"https://news.example.com/a?utm_source=x\",\"content\":\"First.\",\"score\":0.9,\"published_date\":\"2026-11-18\"}," +
                        "{\"title\":\"Tavily two\",\"url\":\"https://blocked.test/b\",\"content\":\"Second.\",\"score\":0.5}]}").ConfigureAwait(false);
                    return;
                }

                if (path.EndsWith("/you/search", StringComparison.Ordinal))
                {
                    await WriteAsync(ctx, 200, "{\"results\":{\"web\":[{\"title\":\"You one\",\"url\":\"https://you.example.com/1\",\"description\":\"From You.\",\"snippets\":[\"s\"]}],\"news\":[]},\"metadata\":{\"query\":\"q\"}}").ConfigureAwait(false);
                    return;
                }

                if (String.Equals(ctx.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase) && !path.EndsWith("/models", StringComparison.Ordinal))
                {
                    await WriteGetAsync(ctx, path).ConfigureAwait(false);
                    return;
                }

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
                    string args = model == "stub-refine" ? RefineArgs(body) : "{\"answer\":\"" + answer + "\"}";
                    string text = jsonMode ? "Here you go:\n```json\n" + args + "\n```" : "ready";
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

        private string RefineArgs(string body)
        {
            string self = BaseUrl;
            if (body.Contains("python web scraper", StringComparison.OrdinalIgnoreCase) || body.Contains("write code for", StringComparison.OrdinalIgnoreCase))
                return "{\"dimensions\":[],\"outOfScopeReason\":\"My Morning News tracks topics and researches subjects; it does not write code.\",\"title\":\"\",\"brief\":\"\",\"seedQueries\":[],\"questions\":[]}";
            if (body.Contains("Plan the deliverable", StringComparison.Ordinal))
                return "{\"title\":\"The shipping container revolution\",\"brief\":\"Cover the invention, standardization, and economic impact of the shipping container.\"," +
                    "\"dimensions\":[{\"name\":\"Invention\",\"description\":\"Malcom McLean and the first container ships.\",\"rationale\":\"Origin story.\",\"weight\":\"High\",\"includeTerms\":[\"McLean\"],\"excludeTerms\":[]}," +
                    "{\"name\":\"Standards\",\"description\":\"ISO container sizes.\",\"rationale\":\"Why it scaled.\",\"weight\":\"Normal\",\"includeTerms\":[\"ISO 668\"],\"excludeTerms\":[]}]," +
                    "\"includeKeywords\":[\"intermodal\"],\"excludeKeywords\":[\"container gardening\"],\"seedQueries\":[\"history of the shipping container\",\"Malcom McLean Ideal X\"]," +
                    "\"questions\":" + (body.Contains("Q: ", StringComparison.Ordinal) ? "[]" : "[{\"question\":\"Which angle matters most?\",\"rationale\":\"Sets the focus.\",\"kind\":\"SingleChoice\",\"options\":[\"Economics\",\"Technology\",\"People\"],\"dimension\":null}]") + ",\"outOfScopeReason\":null}";
            if (body.Contains("Propose 6 to 12 dimensions", StringComparison.Ordinal))
            {
                StringBuilder dims = new StringBuilder();
                string[] names = { "Coding assistants", "Agent frameworks", "Pricing and plans", "Model releases", "IDE integrations", "Security and compliance", "Open source projects", "Developer surveys" };
                for (int i = 0; i < names.Length; i++)
                {
                    if (i > 0) dims.Append(',');
                    dims.Append("{\"name\":\"" + names[i] + "\",\"description\":\"About " + names[i].ToLowerInvariant() + ".\",\"rationale\":\"Shapes tool choices.\",\"weight\":\"" + (i < 2 ? "High" : "Normal") + "\",\"includeTerms\":[\"" + names[i].Split(' ')[0].ToLowerInvariant() + "\"],\"excludeTerms\":[\"rumor\"]}");
                }

                return "{\"dimensions\":[" + dims + "],\"outOfScopeReason\":null}";
            }

            if (body.Contains("Ask 5 to 8 questions", StringComparison.Ordinal) || body.Contains("follow-up questions", StringComparison.Ordinal))
            {
                bool followup = body.Contains("follow-up questions", StringComparison.Ordinal);
                return "{\"sufficientContext\":" + (followup ? "true" : "false") + ",\"questions\":[" +
                    "{\"question\":\"" + (followup ? "Which vendors should we watch most closely?" : "What is your role?") + "\",\"rationale\":\"Sets depth.\",\"kind\":\"SingleChoice\",\"options\":[\"Engineer\",\"Manager\",\"Investor\"],\"dimension\":\"Coding assistants\"}," +
                    "{\"question\":\"Which areas matter?\",\"rationale\":\"Focus.\",\"kind\":\"MultiChoice\",\"options\":[\"Pricing\",\"Security\",\"Releases\"],\"dimension\":null}," +
                    "{\"question\":\"Anything to avoid?\",\"rationale\":\"Excludes noise.\",\"kind\":\"FreeText\",\"options\":[],\"dimension\":null}," +
                    "{\"question\":\"How technical should it be?\",\"rationale\":\"Depth.\",\"kind\":\"Scale\",\"options\":[],\"dimension\":null}," +
                    "{\"question\":\"Include funding news?\",\"rationale\":\"Scope.\",\"kind\":\"YesNo\",\"options\":[],\"dimension\":null}]}";
            }

            if (body.Contains("Produce: a Markdown brief", StringComparison.Ordinal))
                return "{\"name\":\"AI developer tools\",\"brief\":\"Track releases and pricing of AI coding tools.\\n\\n- Focus on assistants and agents.\",\"includeKeywords\":[\"copilot\",\"agent\"],\"excludeKeywords\":[\"rumor\"]," +
                    "\"includeEntities\":[\"GitHub\"],\"excludeEntities\":[],\"preferredDomains\":[\"https://www.Example.com/blog\"],\"blockedDomains\":[\"spam.test\"],\"trackedQuestions\":[\"When will agents be free?\"]," +
                    "\"audienceLevel\":\"Expert\",\"horizon\":\"Months\",\"noveltyPreference\":140,\"seedQueries\":[\"ai coding assistant release\",\"agent framework pricing\"]}";
            if (body.Contains("Suggest up to 12 feed URLs", StringComparison.Ordinal))
                return "{\"feeds\":[{\"url\":\"" + self + "/feed.xml\",\"reason\":\"Official feed.\",\"dimension\":\"Model releases\"},{\"url\":\"" + self + "/missing.xml\",\"reason\":\"Gone.\",\"dimension\":null},{\"url\":\"not a url\",\"reason\":\"x\",\"dimension\":null}]," +
                    "\"seedUrls\":[{\"url\":\"" + self + "/changelog\",\"reason\":\"Changelog.\",\"dimension\":null}]}";
            return "{\"answer\":\"ready\"}";
        }

        private static async Task WriteGetAsync(HttpListenerContext ctx, string path)
        {
            if (path.EndsWith("/robots.txt", StringComparison.Ordinal)) { await WriteRawAsync(ctx, 200, "text/plain", "User-agent: *\nDisallow: /v1/private\nAllow: /v1/private/open\n").ConfigureAwait(false); return; }
            if (path.Contains("missing", StringComparison.Ordinal)) { await WriteAsync(ctx, 404, "{\"error\":\"not found\"}").ConfigureAwait(false); return; }
            if (path.EndsWith("/search", StringComparison.Ordinal))
            {
                await WriteAsync(ctx, 200, "{\"query\":\"q\",\"results\":[" +
                    "{\"url\":\"https://www.example.com/story?utm_source=feed&id=7\",\"title\":\"Story one\",\"content\":\"Snippet one.\",\"publishedDate\":\"2026-11-18T10:00:00\",\"score\":2.5}," +
                    "{\"url\":\"https://example.com/story?id=7&fbclid=abc\",\"title\":\"Story one again\",\"content\":\"Duplicate.\"}," +
                    "{\"url\":\"https://sub.blocked.test/x\",\"title\":\"Blocked\",\"content\":\"No.\"}," +
                    "{\"url\":\"https://other.example.org/two\",\"title\":\"Story two\",\"content\":\"Snippet two.\"}]}").ConfigureAwait(false);
                return;
            }

            if (path.Contains("/hn/search_by_date", StringComparison.Ordinal))
            {
                await WriteAsync(ctx, 200, "{\"hits\":[{\"objectID\":\"101\",\"title\":\"Show HN: agents\",\"url\":\"https://agents.example.com\",\"points\":120,\"created_at\":\"2026-11-18T09:00:00.000Z\"}," +
                    "{\"objectID\":\"102\",\"title\":\"Ask HN: tools?\",\"url\":null,\"points\":40,\"created_at\":\"2026-11-17T09:00:00.000Z\"}]}").ConfigureAwait(false);
                return;
            }

            if (path.Contains("/arxiv/query", StringComparison.Ordinal))
            {
                await WriteRawAsync(ctx, 200, "application/atom+xml", "<?xml version=\"1.0\"?><feed xmlns=\"http://www.w3.org/2005/Atom\"><entry><id>http://arxiv.org/abs/2611.00001v1</id><title>Agentic Coding Benchmarks</title>" +
                    "<summary>We study agents.</summary><published>2026-11-17T00:00:00Z</published><link href=\"http://arxiv.org/abs/2611.00001v1\" rel=\"alternate\" type=\"text/html\"/><link href=\"http://arxiv.org/pdf/2611.00001v1\" rel=\"related\"/></entry></feed>").ConfigureAwait(false);
                return;
            }

            if (path.EndsWith(".html", StringComparison.Ordinal) || path.Contains("/private/", StringComparison.Ordinal))
            {
                await WriteRawAsync(ctx, 200, "text/html; charset=utf-8", "<!doctype html><html><head><title>Heat pump rebates expand</title></head><body><nav>Menu</nav><article><h1>Heat pump rebates expand</h1>" +
                    "<p>The state announced <strong>new rebates</strong> for heat pumps on Tuesday.</p><p>Applications open in January.</p></article></body></html>").ConfigureAwait(false);
                return;
            }

            if (path.EndsWith(".bin", StringComparison.Ordinal)) { await WriteRawAsync(ctx, 200, "application/octet-stream", "binary").ConfigureAwait(false); return; }
            await WriteRawAsync(ctx, 200, "application/rss+xml", "<?xml version=\"1.0\"?><rss version=\"2.0\"><channel><title>stub</title>" +
                "<item><title>Heat pump rebates expand in California</title><link>https://energy.example.com/rebates</link><description>&lt;p&gt;New &lt;b&gt;rebates&lt;/b&gt; announced.&lt;/p&gt;</description><pubDate>Wed, 18 Nov 2026 08:00:00 GMT</pubDate></item>" +
                "<item><title>Unrelated gardening tips</title><link>https://garden.example.com/tips</link><description>Tomatoes.</description><pubDate>Tue, 17 Nov 2026 08:00:00 GMT</pubDate></item>" +
                "</channel></rss>").ConfigureAwait(false);
        }

        private static async Task WriteRawAsync(HttpListenerContext ctx, int status, string contentType, string content)
        {
            byte[] data = Encoding.UTF8.GetBytes(content);
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = contentType;
            ctx.Response.ContentLength64 = data.Length;
            await ctx.Response.OutputStream.WriteAsync(data).ConfigureAwait(false);
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
