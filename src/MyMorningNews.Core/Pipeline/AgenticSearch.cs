namespace MyMorningNews.Core.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Fetch;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Models.Results;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Search;
    using MyMorningNews.Core.Services;

    /// <summary>
    /// Agentic search (plan section 15.1): for a topic that opts in, the research model explores with web_search,
    /// fetch_url, and the topic's allowed MCP tools, within Pipeline.MaxToolCallsPerRun, and submits the pages to read.
    /// Those pages become the run's search results, so filtering, fetching, extraction, and quote verification work as
    /// usual. Every tool execution is logged in runtoolcalls. Thread safe per run.
    /// </summary>
    public static class AgenticSearch
    {
        #region Public-Members

        /// <summary>
        /// Prompt key.
        /// </summary>
        public const string PromptKey = "research.agentic-search";

        /// <summary>
        /// Most pages the agent may pick. Default 20.
        /// </summary>
        public const int MaxPicks = 20;

        #endregion

        #region Private-Members

        private static readonly Regex _Unsafe = new Regex("[^a-zA-Z0-9_]+", RegexOptions.Compiled);

        #endregion

        #region Public-Methods

        /// <summary>
        /// Run agentic search for the run. Returns null when it produced the run's results, or a note explaining why
        /// the fixed query plan should be used instead (no tool support, no providers, or no valid picks).
        /// </summary>
        /// <param name="ctx">Run context.</param>
        /// <param name="services">Services.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Null on success, or the fallback reason.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static async Task<string?> TryRunAsync(RunContext ctx, PipelineServices services, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            ArgumentNullException.ThrowIfNull(services);
            List<SearchProvider> providers = ctx.Providers.Where(p => p.Kind != SearchProviderKindEnum.Rss).ToList();
            if (providers.Count == 0) return "Agentic search needs a web search provider; used the fixed query plan.";
            List<string> blocked = ctx.Constraints.Where(c => c.Kind == ConstraintKindEnum.BlockedDomain).Select(c => c.Value).ToList();
            List<SearchResult> found = new List<SearchResult>();
            object sync = new object();
            int order = 0;

            async Task<string> Logged(string tool, string args, Func<Task<string>> work)
            {
                Stopwatch sw = Stopwatch.StartNew();
                int sort = Interlocked.Increment(ref order);
                string? error = null;
                string result = String.Empty;
                try
                {
                    result = await work().ConfigureAwait(false);
                    if (result.StartsWith("Error:", StringComparison.Ordinal)) error = result;
                    return result;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    error = ex.Message;
                    throw;
                }
                finally
                {
                    await services.Database.RunToolCalls.CreateAsync(new RunToolCall
                    {
                        TenantId = ctx.Run.TenantId, RunId = ctx.Run.Id, TopicId = ctx.Topic.Id, Tool = tool, ArgumentsSummary = Shorten(args, 1000),
                        Succeeded = error == null, ErrorMessage = error == null ? null : Shorten(error, 2000), ResultCharacters = result.Length,
                        DurationMs = Math.Round(sw.Elapsed.TotalMilliseconds, 1), SortOrder = sort
                    }, CancellationToken.None).ConfigureAwait(false);
                }
            }

            List<AgentTool> tools = new List<AgentTool>
            {
                new AgentTool("web_search", "Search the web. Returns titles, URLs, dates, and snippets.",
                    Schema(new Dictionary<string, object> { { "query", Prop("string", "Short, specific search query.") }, { "maxResults", Prop("integer", "Results to return, 1 to 10. Default 8.") } }, "query"),
                    (args, t) => Logged("web_search", args, () => WebSearchAsync(ctx, services, providers, blocked, args, found, sync, t))),
                new AgentTool("fetch_url", "Read a web page. Returns its title and main text (shortened).",
                    Schema(new Dictionary<string, object> { { "url", Prop("string", "Absolute http or https URL.") } }, "url"),
                    (args, t) => Logged("fetch_url", args, () => FetchAsync(services, blocked, args, t)))
            };
            tools.AddRange(await McpToolsAsync(ctx, services, Logged, token).ConfigureAwait(false));

            StructuredCallResult<AgenticSearchResult> result;
            try
            {
                result = await services.CallAgenticAsync<AgenticSearchResult>(ctx, PromptKey, new Dictionary<string, object?>
                {
                    { "queries.planned", ctx.PlannedQueries.Select(q => q.Query + (String.IsNullOrEmpty(q.Dimension) ? String.Empty : " (" + q.Dimension + ")")).ToList() },
                    { "max.picks", MaxPicks.ToString(CultureInfo.InvariantCulture) },
                    { "max.tools", services.Settings.Pipeline.MaxToolCallsPerRun.ToString(CultureInfo.InvariantCulture) }
                }, tools, services.Settings.Pipeline.MaxToolCallsPerRun, token).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                return "Agentic search is off for this run: " + ex.Message + " Used the fixed query plan.";
            }
            catch (ModelCallException ex)
            {
                return "Agentic search failed (" + ex.Message + "); used the fixed query plan.";
            }

            List<SearchResult> picked = await PicksAsync(ctx, services, result.Value.Picks.Take(MaxPicks).ToList(), found, blocked, token).ConfigureAwait(false);
            if (picked.Count == 0) return "Agentic search picked no usable pages; used the fixed query plan.";
            ctx.Results.Clear();
            ctx.Results.AddRange(picked);
            ctx.Notes.Add("Agentic search picked " + picked.Count + " pages.");
            return null;
        }

        #endregion

        #region Private-Methods

        private static async Task<string> WebSearchAsync(RunContext ctx, PipelineServices services, List<SearchProvider> providers, List<string> blocked, string args, List<SearchResult> found, object sync, CancellationToken token)
        {
            string query;
            int max;
            using (JsonDocument doc = JsonDocument.Parse(args))
            {
                query = doc.RootElement.TryGetProperty("query", out JsonElement q) ? (q.GetString() ?? String.Empty).Trim() : String.Empty;
                max = doc.RootElement.TryGetProperty("maxResults", out JsonElement m) && m.TryGetInt32(out int n) ? Math.Clamp(n, 1, 10) : 8;
            }

            if (query.Length == 0) return "Error: query is required.";
            if (query.Length > 400) query = query.Substring(0, 400);
            StringBuilder sb = new StringBuilder();
            int shown = 0;
            foreach (SearchProvider p in providers.Take(2))
            {
                SearchOutcome outcome = await services.Search.SearchAsync(p, new ResearchSearchRequest { Query = query, MaxResults = max, ExcludeDomains = blocked }, token).ConfigureAwait(false);
                SearchQuery row = await services.Database.SearchQueries.CreateAsync(new SearchQuery
                {
                    TenantId = ctx.Run.TenantId, RunId = ctx.Run.Id, TopicId = ctx.Topic.Id, QueryText = query, Rationale = "Agentic search", SearchProviderId = p.Id,
                    Provider = p.Kind.ToString(), ResultCount = outcome.Hits.Count, DurationMs = outcome.DurationMs, Succeeded = outcome.Success, ErrorMessage = outcome.Error
                }, token).ConfigureAwait(false);
                foreach (SearchHit h in outcome.Hits.Take(max))
                {
                    SearchResult r = await services.Database.SearchResults.CreateAsync(new SearchResult
                    {
                        TenantId = ctx.Run.TenantId, SearchQueryId = row.Id, RunId = ctx.Run.Id, TopicId = ctx.Topic.Id, Url = h.Url,
                        CanonicalUrlHash = UrlCanonicalizer.Hash(h.Url) ?? h.CanonicalUrl, Title = h.Title, Snippet = h.Snippet, PublishedUtc = h.PublishedUtc, Rank = h.Rank, Provider = h.Provider
                    }, token).ConfigureAwait(false);
                    lock (sync) found.Add(r);
                    shown++;
                    sb.Append(shown).Append(". ").Append(h.Title).Append('\n').Append("   ").Append(h.Url);
                    if (h.PublishedUtc.HasValue) sb.Append(" (").Append(h.PublishedUtc.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(')');
                    sb.Append('\n');
                    if (!String.IsNullOrWhiteSpace(h.Snippet)) sb.Append("   ").Append(Shorten(h.Snippet, 300)).Append('\n');
                }

                if (outcome.Success && outcome.Hits.Count > 0) break;
            }

            return shown == 0 ? "No results for \"" + query + "\". Try different words." : sb.ToString();
        }

        private static async Task<string> FetchAsync(PipelineServices services, List<string> blocked, string args, CancellationToken token)
        {
            string url;
            using (JsonDocument doc = JsonDocument.Parse(args))
            {
                url = doc.RootElement.TryGetProperty("url", out JsonElement u) ? (u.GetString() ?? String.Empty).Trim() : String.Empty;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) return "Error: url must be an absolute http or https URL.";
            FetchResult r = await services.Fetch.FetchAsync(url, blocked, token).ConfigureAwait(false);
            if (r.Disposition != FetchDispositionEnum.Fetched || String.IsNullOrWhiteSpace(r.Text))
                return "Error: could not read " + url + " (" + r.Disposition + (String.IsNullOrEmpty(r.Error) ? String.Empty : ": " + r.Error) + ").";
            return "Title: " + (r.Title ?? "(none)") + "\nURL: " + (r.FinalUrl ?? url) + "\n\n" + Shorten(r.Text, 6000);
        }

        private static async Task<List<AgentTool>> McpToolsAsync(RunContext ctx, PipelineServices services, Func<string, string, Func<Task<string>>, Task<string>> logged, CancellationToken token)
        {
            List<AgentTool> tools = new List<AgentTool>();
            if (services.Mcp == null) return tools;
            List<McpServer> servers = (await services.Database.McpServers.EnumerateAsync(new McpServerFilter { TenantId = ctx.Run.TenantId, Enabled = true, PageSize = 50 }, token).ConfigureAwait(false)).Objects;
            HashSet<string> allowed = new HashSet<string>(ctx.Topic.McpToolIds, StringComparer.Ordinal);
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal) { "web_search", "fetch_url", ModelGateway.SubmitToolName };
            foreach (McpServer server in servers)
            {
                List<McpServerTool> serverTools = (await services.Database.McpServerTools.EnumerateAsync(new McpServerToolFilter { TenantId = ctx.Run.TenantId, McpServerId = server.Id, Enabled = true, PageSize = 200 }, token).ConfigureAwait(false)).Objects;
                foreach (McpServerTool t in serverTools.Where(t => allowed.Count == 0 || allowed.Contains(t.Id)))
                {
                    string name = ("mcp_" + _Unsafe.Replace(server.Name, "_") + "_" + _Unsafe.Replace(t.Name, "_")).ToLowerInvariant();
                    if (name.Length > 64) name = name.Substring(0, 64);
                    if (!names.Add(name)) continue;
                    McpServer captured = server;
                    McpServerTool tool = t;
                    tools.Add(new AgentTool(name, Shorten(t.Description ?? ("Tool " + t.Name + " on " + server.Name + "."), 1000), ParseSchema(t.InputSchema),
                        (args, tk) => logged("mcp:" + captured.Name + ":" + tool.Name, args, () => services.Mcp.CallToolAsync(captured, tool.Name, args, tk))));
                }
            }

            return tools;
        }

        private static async Task<List<SearchResult>> PicksAsync(RunContext ctx, PipelineServices services, List<AgenticPick> picks, List<SearchResult> found, List<string> blocked, CancellationToken token)
        {
            List<SearchResult> picked = new List<SearchResult>();
            SearchQuery? pickRow = null;
            int rank = 0;
            foreach (AgenticPick p in picks)
            {
                if (!Uri.TryCreate(p.Url, UriKind.Absolute, out Uri? uri)) continue;
                if (blocked.Any(b => uri.Host.Equals(b, StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith("." + b, StringComparison.OrdinalIgnoreCase))) continue;
                string? hash = UrlCanonicalizer.Hash(p.Url);
                rank++;
                SearchResult? existing = found.FirstOrDefault(r => hash != null && r.CanonicalUrlHash == hash);
                if (existing != null)
                {
                    existing.Rank = rank;
                    if (picked.All(x => x.Id != existing.Id)) picked.Add(existing);
                    continue;
                }

                pickRow ??= await services.Database.SearchQueries.CreateAsync(new SearchQuery
                {
                    TenantId = ctx.Run.TenantId, RunId = ctx.Run.Id, TopicId = ctx.Topic.Id, QueryText = "Agentic search picks", Rationale = "Pages the agent found by reading", Provider = "Agent", Succeeded = true
                }, token).ConfigureAwait(false);
                picked.Add(await services.Database.SearchResults.CreateAsync(new SearchResult
                {
                    TenantId = ctx.Run.TenantId, SearchQueryId = pickRow.Id, RunId = ctx.Run.Id, TopicId = ctx.Topic.Id, Url = p.Url, CanonicalUrlHash = hash ?? p.Url,
                    Title = p.Title, Snippet = p.Reason, Rank = rank, Provider = "Agent"
                }, token).ConfigureAwait(false));
            }

            if (pickRow != null)
            {
                pickRow.ResultCount = picked.Count(r => r.SearchQueryId == pickRow.Id);
                await services.Database.SearchQueries.UpdateAsync(pickRow, token).ConfigureAwait(false);
            }

            return picked;
        }

        private static Dictionary<string, object> Schema(Dictionary<string, object> properties, params string[] required)
        {
            return new Dictionary<string, object> { { "type", "object" }, { "properties", properties }, { "required", required } };
        }

        private static Dictionary<string, object> Prop(string type, string description)
        {
            return new Dictionary<string, object> { { "type", type }, { "description", description } };
        }

        private static Dictionary<string, object> ParseSchema(string? json)
        {
            if (!String.IsNullOrWhiteSpace(json))
            {
                try
                {
                    Dictionary<string, object>? parsed = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    if (parsed != null && parsed.Count > 0)
                    {
                        if (!parsed.ContainsKey("type")) parsed["type"] = "object";
                        return parsed;
                    }
                }
                catch (JsonException)
                {
                }
            }

            return new Dictionary<string, object> { { "type", "object" }, { "properties", new Dictionary<string, object>() } };
        }

        private static string Shorten(string? text, int max)
        {
            if (String.IsNullOrEmpty(text)) return String.Empty;
            return text.Length <= max ? text : text.Substring(0, max) + "...";
        }

        #endregion
    }
}
