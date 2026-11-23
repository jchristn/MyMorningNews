namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.McpServer;
    using Test.Shared.Helpers;
    using Touchstone.Core;
    using Voltaic.Mcp;

    /// <summary>
    /// The MCP server (plan section 24.2): the REST server and the MCP server run in process, and every tool, resource,
    /// and prompt is driven over Streamable HTTP with Voltaic's client, plus a stdio smoke test, authentication, and
    /// cross-tenant denial.
    /// </summary>
    public static class McpSuite
    {
        private const string S = "Mcp";

        /// <summary>
        /// Create the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "MCP server", new[]
            {
                Cases.Case(S, "Surface", "Every tool declares input and output schemas, and the resources and prompts are listed", SurfaceAsync),
                Cases.Case(S, "Auth", "Requests without valid credentials are refused; bearer tokens and access keys work", AuthAsync),
                Cases.Case(S, "ResearchFlow", "Topics, runs, findings, evidence, briefings, predictions, suggestions, feedback, preferences, and analytics work over MCP", FlowAsync),
                Cases.Case(S, "Resources", "Briefing, topic brief, and run summary resources and both prompts render the caller's data", ResourcesAsync),
                Cases.Case(S, "CrossTenant", "A caller from another tenant cannot see or change anything", CrossTenantAsync),
                Cases.Case(S, "CreateTopic", "create_topic runs the wizard defaults and activates a topic; edit tools change it", CreateTopicAsync),
                Cases.Case(S, "Stdio", "The executable serves the same tools over stdio with credentials from the environment", StdioAsync)
            });
        }

        private static async Task<McpHost> StartHostAsync(CancellationToken token)
        {
            McpServerSettings settings = new McpServerSettings
            {
                ApiUrl = await SharedTestServer.GetBaseUrlAsync(token).ConfigureAwait(false),
                Hostname = "127.0.0.1",
                Port = PortAllocator.GetFreePort(),
                CredentialCacheSeconds = 0,
                TelemetryEnabled = false
            };
            McpHost host = new McpHost(settings);
            await host.StartHttpAsync(token).ConfigureAwait(false);
            return host;
        }

        private static async Task<McpTestClient> ConnectAsync(McpHost host, string? bearer, CancellationToken token, Dictionary<string, string>? extra = null)
        {
            McpTestClient c = new McpTestClient();
            Dictionary<string, string> headers = extra ?? new Dictionary<string, string>();
            if (bearer != null) headers["Authorization"] = "Bearer " + bearer;
            Assert.True(await c.ConnectAsync(host.HttpBaseUrl, headers, token).ConfigureAwait(false), "MCP client connected");
            return c;
        }

        private static async Task SurfaceAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            using (McpHost host = await StartHostAsync(token).ConfigureAwait(false))
            using (McpTestClient c = await ConnectAsync(host, f.User.Client.Token, token).ConfigureAwait(false))
            {
                JsonElement tools = (await c.CallAsync("tools/list", null, token).ConfigureAwait(false)).GetProperty("tools");
                List<string> names = tools.EnumerateArray().Select(t => t.GetProperty("name").GetString()!).ToList();
                string[] planned = { "list_topics", "get_topic", "create_topic", "run_topic", "get_run", "get_run_costs", "list_schedules", "set_schedule", "remove_schedule", "list_findings", "get_finding", "search_findings", "get_latest_briefing", "get_briefing", "share_briefing", "list_briefing_shares", "revoke_briefing_share",
                    "list_predictions", "resolve_prediction", "list_suggestions", "update_suggestion_status", "submit_feedback", "list_learned_preferences", "accept_learned_preference", "get_analytics_summary" };
                Assert.True(planned.All(names.Contains), "every planned tool: missing " + String.Join(", ", planned.Where(p => !names.Contains(p))));
                Assert.Equal(McpSurface.AllTools().Count, names.Count, "tool count");
                Assert.True(tools.EnumerateArray().All(t => t.TryGetProperty("inputSchema", out JsonElement i) && i.GetProperty("type").GetString() == "object"
                    && t.TryGetProperty("outputSchema", out JsonElement o) && o.GetProperty("type").GetString() == "object" && t.GetProperty("description").GetString()!.Length > 20), "schemas and descriptions");
                Assert.True(tools.EnumerateArray().Single(t => t.GetProperty("name").GetString() == "list_topics").GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean(), "read-only hint");
                List<string> resources = (await c.CallAsync("resources/list", null, token).ConfigureAwait(false)).GetProperty("resources").EnumerateArray().Select(r => r.GetProperty("uri").GetString()!).ToList();
                List<string> templates = (await c.CallAsync("resources/templates/list", null, token).ConfigureAwait(false)).GetProperty("resourceTemplates").EnumerateArray().Select(r => r.GetProperty("uriTemplate").GetString()!).ToList();
                Assert.True(resources.Contains("mmn://briefings/latest") && templates.Contains("mmn://briefings/{id}") && templates.Contains("mmn://topics/{id}/brief") && templates.Contains("mmn://runs/{id}/summary"), "resources");
                List<string> prompts = (await c.CallAsync("prompts/list", null, token).ConfigureAwait(false)).GetProperty("prompts").EnumerateArray().Select(p => p.GetProperty("name").GetString()!).ToList();
                Assert.True(prompts.Contains("review_morning_briefing") && prompts.Contains("refine_topic"), "prompts");
                string? doc = FindUp("MCP_API.md");
                if (doc != null)
                {
                    string text = File.ReadAllText(doc);
                    Assert.True(names.All(n => text.Contains("### " + n + "\n", StringComparison.Ordinal)), "every tool documented in MCP_API.md: missing " + String.Join(", ", names.Where(n => !text.Contains("### " + n + "\n", StringComparison.Ordinal))));
                }

                McpToolOutcome bad = await c.ToolAsync("list_findings", new { tier = "Bogus" }, token).ConfigureAwait(false);
                Assert.True(bad.IsError && bad.Text.Contains("MustRead", StringComparison.Ordinal), "input schema enforced: " + bad.Text);
            }
        }

        private static string? FindUp(string fileName)
        {
            DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, fileName);
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }

            return null;
        }

        private static async Task AuthAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            using (McpHost host = await StartHostAsync(token).ConfigureAwait(false))
            {
                using (McpTestClient none = new McpTestClient())
                {
                    Assert.True(!await none.ConnectAsync(host.HttpBaseUrl, new Dictionary<string, string>(), token).ConfigureAwait(false), "no credentials refused");
                }

                using (McpTestClient wrong = new McpTestClient())
                {
                    Assert.True(!await wrong.ConnectAsync(host.HttpBaseUrl, new Dictionary<string, string> { { "Authorization", "Bearer not-a-token" } }, token).ConfigureAwait(false), "bad token refused");
                }

                CredentialCreatedResponse key = (await f.User.Client.PostAsync("/v1.0/tenants/" + f.Topic.TenantId + "/credentials", new { name = "mcp-key" }, token).ConfigureAwait(false))
                    .Expect(201, "credential").Json<CredentialCreatedResponse>();
                Dictionary<string, string> keyHeaders = new Dictionary<string, string> { { "x-access-key", key.Credential.AccessKey }, { "x-secret-key", key.SecretKey }, { "x-tenant-guid", f.Topic.TenantId } };
                using (McpTestClient byKey = await ConnectAsync(host, null, token, keyHeaders).ConfigureAwait(false))
                {
                    McpToolOutcome topics = await byKey.ToolAsync("list_topics", new { }, token).ConfigureAwait(false);
                    Assert.True(!topics.IsError && topics.Structured.GetProperty("items").EnumerateArray().Any(t => t.GetProperty("id").GetString() == f.Topic.Id), "access key sees the owner's topic: " + topics.Text);
                }

                using (McpTestClient byToken = await ConnectAsync(host, f.User.Client.Token, token).ConfigureAwait(false))
                {
                    (await f.User.Client.DeleteAsync("/v1.0/token", token).ConfigureAwait(false)).Expect(204, "logout");
                    bool refused;
                    try
                    {
                        refused = (await byToken.ToolAsync("list_topics", new { }, token).ConfigureAwait(false)).IsError;
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        refused = true;
                    }

                    Assert.True(refused, "a revoked session stops working at once");
                }
            }
        }

        private static async Task<Run> ResearchedAsync(PipelineFixture f, CancellationToken token)
        {
            Run run = await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            Assert.True(run.Status == RunStatusEnum.Completed, "run completed: " + run.Status);
            Briefing b = (await f.User.Client.GetAsync("/v1.0/api/briefings?runId=" + run.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Briefing>>().Items.Single();
            await BriefingWaiter.FinishAsync(f.Server, b.TenantId, b.Id, token).ConfigureAwait(false);
            return run;
        }

        private static async Task FlowAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            Run run = await ResearchedAsync(f, token).ConfigureAwait(false);
            using (McpHost host = await StartHostAsync(token).ConfigureAwait(false))
            using (McpTestClient c = await ConnectAsync(host, f.User.Client.Token, token).ConfigureAwait(false))
            {
                McpToolOutcome topics = await c.ToolAsync("list_topics", new { status = "Active" }, token).ConfigureAwait(false);
                Assert.True(!topics.IsError && topics.Structured.GetProperty("totalCount").GetInt64() == 1 && topics.Structured.GetProperty("items")[0].GetProperty("id").GetString() == f.Topic.Id, "list_topics: " + topics.Text);
                McpToolOutcome topic = await c.ToolAsync("get_topic", new { topicId = f.Topic.Id }, token).ConfigureAwait(false);
                Assert.True(!topic.IsError && topic.Structured.GetProperty("dimensions").EnumerateArray().Any(d => d.GetProperty("name").GetString() == "Rebates"), "get_topic: " + topic.Text);

                McpToolOutcome gotRun = await c.ToolAsync("get_run", new { runId = run.Id }, token).ConfigureAwait(false);
                Assert.True(!gotRun.IsError && gotRun.Structured.GetProperty("run").GetProperty("status").GetString() == "Completed" && gotRun.Structured.GetProperty("stages").GetArrayLength() == 15, "get_run: " + gotRun.Text);
                JsonElement runShape = gotRun.Structured.GetProperty("run");
                Assert.True(runShape.GetProperty("llmCallCount").GetInt32() > 0 && runShape.GetProperty("estimatedCostUsd").ValueKind == JsonValueKind.Number, "get_run carries the run's cost: " + gotRun.Text);
                Assert.True(gotRun.Structured.GetProperty("stages").EnumerateArray().Any(s => s.GetProperty("llmCallCount").GetInt32() > 0 && s.GetProperty("estimatedCostUsd").ValueKind == JsonValueKind.Number), "get_run stages carry their cost");
                McpToolOutcome costs = await c.ToolAsync("get_run_costs", new { runId = run.Id }, token).ConfigureAwait(false);
                Assert.True(!costs.IsError && costs.Str("runId") == run.Id && costs.Str("currency") == "USD", "get_run_costs: " + costs.Text);
                JsonElement costTotals = costs.Structured.GetProperty("totals");
                JsonElement costStages = costs.Structured.GetProperty("stages");
                Assert.Equal(15, costStages.GetArrayLength(), "a cost row per stage");
                Assert.Equal(runShape.GetProperty("llmCallCount").GetInt32(), costTotals.GetProperty("llmCallCount").GetInt32(), "report and run agree on calls");
                int attributed = costStages.EnumerateArray().Sum(s => s.GetProperty("llmCallCount").GetInt32())
                    + (costs.Structured.GetProperty("unattributed").ValueKind == JsonValueKind.Object ? costs.Structured.GetProperty("unattributed").GetProperty("llmCallCount").GetInt32() : 0);
                Assert.Equal(costTotals.GetProperty("llmCallCount").GetInt32(), attributed, "stage calls add up to the total");
                Assert.True(costs.Structured.GetProperty("byModel").GetArrayLength() >= 1 && costs.Structured.GetProperty("byTask").GetArrayLength() >= 1, "by model and by task");
                Assert.True((await c.ToolAsync("get_run_costs", new { runId = "run_missing" }, token).ConfigureAwait(false)).IsError, "get_run_costs for a missing run is an error");

                McpToolOutcome latest = await c.ToolAsync("get_latest_briefing", new { }, token).ConfigureAwait(false);
                JsonElement brief = latest.Structured.GetProperty("briefing");
                Assert.True(!latest.IsError && latest.Structured.GetProperty("found").GetBoolean() && brief.GetProperty("markdown").GetString()!.Contains("Heat pump rebates expand", StringComparison.Ordinal), "get_latest_briefing: " + latest.Text);
                string briefingId = brief.GetProperty("briefing").GetProperty("id").GetString()!;
                McpToolOutcome small = await c.ToolAsync("get_briefing", new { briefingId = briefingId, maxCharacters = 1000 }, token).ConfigureAwait(false);
                Assert.True(!small.IsError && small.Structured.GetProperty("markdown").GetString()!.Length <= 1200 && small.Structured.GetProperty("items").GetArrayLength() >= 3, "get_briefing with a budget");
                Assert.True((await c.ToolAsync("list_briefings", new { kind = "Run" }, token).ConfigureAwait(false)).Structured.GetProperty("totalCount").GetInt64() == 1, "list_briefings");

                McpToolOutcome tooLong = await c.ToolAsync("share_briefing", new { briefingId = briefingId, expiresInDays = 15 }, token).ConfigureAwait(false);
                Assert.True(tooLong.IsError, "share_briefing refuses 15 days: " + tooLong.Text);
                McpToolOutcome shared = await c.ToolAsync("share_briefing", new { briefingId = briefingId, expiresInDays = 14 }, token).ConfigureAwait(false);
                Assert.True(!shared.IsError && shared.Structured.GetProperty("url").GetString()!.Contains("/v1.0/public/shares/mmns_", StringComparison.Ordinal)
                    && shared.Structured.GetProperty("listenUrl").GetString() == shared.Structured.GetProperty("url").GetString() && shared.Structured.GetProperty("share").GetProperty("status").GetString() == "Live", "share_briefing: " + shared.Text);
                Assert.True(!shared.Text.Contains("tokenHash", StringComparison.Ordinal), "the stored hash is left out");
                string shareId = shared.Structured.GetProperty("share").GetProperty("id").GetString()!;
                McpToolOutcome shares = await c.ToolAsync("list_briefing_shares", new { briefingId = briefingId }, token).ConfigureAwait(false);
                Assert.True(!shares.IsError && shares.Structured.GetProperty("totalCount").GetInt64() == 1 && shares.Structured.GetProperty("items")[0].GetProperty("id").GetString() == shareId, "list_briefing_shares: " + shares.Text);
                McpToolOutcome revoked = await c.ToolAsync("revoke_briefing_share", new { briefingId = briefingId, shareId = shareId }, token).ConfigureAwait(false);
                Assert.True(!revoked.IsError && revoked.Structured.GetProperty("revoked").GetBoolean(), "revoke_briefing_share: " + revoked.Text);
                Assert.Equal("Revoked", (await c.ToolAsync("list_briefing_shares", new { briefingId = briefingId }, token).ConfigureAwait(false)).Structured.GetProperty("items")[0].GetProperty("status").GetString(), "revoked share listed");

                McpToolOutcome findings = await c.ToolAsync("list_findings", new { topicId = f.Topic.Id, minScore = 1 }, token).ConfigureAwait(false);
                JsonElement items = findings.Structured.GetProperty("items");
                Assert.True(!findings.IsError && items.GetArrayLength() >= 2, "list_findings: " + findings.Text);
                string findingId = items[0].GetProperty("id").GetString()!;
                McpToolOutcome search = await c.ToolAsync("search_findings", new { query = "rebates", topicId = f.Topic.Id }, token).ConfigureAwait(false);
                Assert.True(!search.IsError && search.Structured.GetProperty("items").GetArrayLength() >= 1, "search_findings: " + search.Text);
                McpToolOutcome since = await c.ToolAsync("list_findings", new { since = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd") }, token).ConfigureAwait(false);
                Assert.Equal(0L, since.Structured.GetProperty("totalCount").GetInt64(), "since filter");
                McpToolOutcome finding = await c.ToolAsync("get_finding", new { findingId = findingId }, token).ConfigureAwait(false);
                Assert.True(!finding.IsError && finding.Structured.GetProperty("evidence").GetArrayLength() >= 1 && finding.Structured.GetProperty("evidence")[0].GetProperty("quote").GetString()!.Length > 0
                    && finding.Structured.GetProperty("scores").GetProperty("llm").GetDouble() > 0, "get_finding with evidence: " + finding.Text);

                McpToolOutcome rated = await c.ToolAsync("submit_feedback", new { targetType = "Finding", targetId = findingId, rating = "not-relevant", reason = "Already covered elsewhere" }, token).ConfigureAwait(false);
                Assert.True(!rated.IsError && rated.Str("channel") == "Mcp" && rated.Str("relevance") == "NotRelevant" && rated.Str("reason") == "Already covered elsewhere", "submit_feedback: " + rated.Text);
                EnumerationResult<Feedback> stored = (await f.User.Client.GetAsync("/v1.0/api/feedback?targetId=" + findingId, token).ConfigureAwait(false)).Json<EnumerationResult<Feedback>>();
                Assert.True(stored.Items.Any(x => x.Channel == FeedbackChannelEnum.Mcp && x.IsCurrent), "stored with the Mcp channel");
                McpToolOutcome wrongRating = await c.ToolAsync("submit_feedback", new { targetType = "Finding", targetId = findingId, rating = "accurate" }, token).ConfigureAwait(false);
                Assert.True(wrongRating.IsError && wrongRating.Text.Contains("400", StringComparison.Ordinal), "rating checked against the type: " + wrongRating.Text);
                McpToolOutcome empty = await c.ToolAsync("submit_feedback", new { targetType = "Finding", targetId = findingId }, token).ConfigureAwait(false);
                Assert.True(empty.IsError, "a rating or reason is needed");

                McpToolOutcome hidden = await c.ToolAsync("update_finding", new { findingId = findingId, hidden = true }, token).ConfigureAwait(false);
                Assert.True(!hidden.IsError && hidden.Structured.GetProperty("hidden").GetBoolean(), "update_finding: " + hidden.Text);
                long visible = (await c.ToolAsync("list_findings", new { topicId = f.Topic.Id, minScore = 1 }, token).ConfigureAwait(false)).Structured.GetProperty("totalCount").GetInt64();
                Assert.Equal((long)items.GetArrayLength() - 1, visible, "hidden finding left out");

                McpToolOutcome preds = await c.ToolAsync("list_predictions", new { topicId = f.Topic.Id, status = "Open" }, token).ConfigureAwait(false);
                string predictionId = preds.Structured.GetProperty("items")[0].GetProperty("id").GetString()!;
                McpToolOutcome resolved = await c.ToolAsync("resolve_prediction", new { predictionId = predictionId, outcome = "Accurate", note = "It happened." }, token).ConfigureAwait(false);
                Assert.True(!resolved.IsError && resolved.Str("status") == "Accurate" && resolved.Structured.GetProperty("brierScore").ValueKind == JsonValueKind.Number, "resolve_prediction: " + resolved.Text);
                Assert.True((await c.ToolAsync("resolve_prediction", new { predictionId = predictionId, outcome = "Open" }, token).ConfigureAwait(false)).IsError, "Open is not an outcome");

                McpToolOutcome sugs = await c.ToolAsync("list_suggestions", new { topicId = f.Topic.Id }, token).ConfigureAwait(false);
                string suggestionId = sugs.Structured.GetProperty("items")[0].GetProperty("id").GetString()!;
                Assert.True((await c.ToolAsync("update_suggestion_status", new { suggestionId = suggestionId, status = "Snoozed" }, token).ConfigureAwait(false)).IsError, "snooze needs a time");
                McpToolOutcome snoozed = await c.ToolAsync("update_suggestion_status", new { suggestionId = suggestionId, status = "Snoozed", snoozeDays = 3 }, token).ConfigureAwait(false);
                Assert.True(!snoozed.IsError && snoozed.Str("status") == "Snoozed" && snoozed.Str("snoozedUntilUtc") != null, "update_suggestion_status: " + snoozed.Text);

                LearnedPreference proposal = await f.Server.Context.Database.LearnedPreferences.CreateAsync(new LearnedPreference
                {
                    TenantId = f.Topic.TenantId, UserId = f.Topic.UserId, TopicId = f.Topic.Id, Kind = PreferenceKindEnum.Avoid, Statement = "Skip stories already covered elsewhere.", Status = PreferenceStatusEnum.Proposed,
                    Origin = PreferenceOriginEnum.Distilled, Confidence = 0.7, SupportCount = 3
                }, token).ConfigureAwait(false);
                McpToolOutcome prefs = await c.ToolAsync("list_learned_preferences", new { status = "Proposed" }, token).ConfigureAwait(false);
                Assert.True(prefs.Structured.GetProperty("items").EnumerateArray().Any(p => p.GetProperty("id").GetString() == proposal.Id), "list_learned_preferences: " + prefs.Text);
                McpToolOutcome accepted = await c.ToolAsync("accept_learned_preference", new { preferenceId = proposal.Id }, token).ConfigureAwait(false);
                Assert.True(!accepted.IsError && accepted.Str("status") == "Active", "accept_learned_preference: " + accepted.Text);

                McpToolOutcome summary = await c.ToolAsync("get_analytics_summary", new { topicId = f.Topic.Id }, token).ConfigureAwait(false);
                Assert.True(!summary.IsError && summary.Structured.GetProperty("runs").GetProperty("total").GetInt64() == 1 && summary.Structured.GetProperty("feedback").GetProperty("total").GetInt32() >= 1
                    && summary.Structured.GetProperty("predictions").GetProperty("resolved").GetInt32() == 1 && summary.Structured.GetProperty("model").GetProperty("calls").GetInt64() > 0, "get_analytics_summary: " + summary.Text);

                McpToolOutcome missing = await c.ToolAsync("get_finding", new { findingId = "fnd_missing" }, token).ConfigureAwait(false);
                Assert.True(missing.IsError && missing.Text.Contains("404", StringComparison.Ordinal) && missing.Text.Contains("Request rqi_", StringComparison.Ordinal), "REST errors are readable: " + missing.Text);

                McpToolOutcome dry = await c.ToolAsync("run_topic", new { topicId = f.Topic.Id, dryRun = true }, token).ConfigureAwait(false);
                Assert.True(!dry.IsError && dry.Structured.GetProperty("run").GetProperty("isDryRun").GetBoolean(), "run_topic queues: " + dry.Text);
                Run queued = (await f.Server.Context.Database.Runs.ReadAsync(f.Topic.TenantId, dry.Structured.GetProperty("run").GetProperty("id").GetString()!, token).ConfigureAwait(false))!;
                await RunWaiter.FinishAsync(f.Server, queued, token).ConfigureAwait(false);
            }
        }

        private static async Task ResourcesAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            using (McpHost host = await StartHostAsync(token).ConfigureAwait(false))
            using (McpTestClient c = await ConnectAsync(host, f.User.Client.Token, token).ConfigureAwait(false))
            {
                string none = (await c.CallAsync("resources/read", new { uri = "mmn://briefings/latest" }, token).ConfigureAwait(false)).GetProperty("contents")[0].GetProperty("text").GetString()!;
                Assert.True(none.Contains("No briefing yet", StringComparison.Ordinal), "no briefing yet");
                string emptyPrompt = (await c.CallAsync("prompts/get", new { name = "review_morning_briefing", arguments = new { } }, token).ConfigureAwait(false)).GetProperty("messages")[0].GetProperty("content").GetProperty("text").GetString()!;
                Assert.True(emptyPrompt.Contains("none is ready yet", StringComparison.Ordinal), "review prompt without a briefing");

                Run run = await ResearchedAsync(f, token).ConfigureAwait(false);
                JsonElement latest = (await c.CallAsync("resources/read", new { uri = "mmn://briefings/latest" }, token).ConfigureAwait(false)).GetProperty("contents")[0];
                Assert.True(latest.GetProperty("mimeType").GetString() == "text/markdown" && latest.GetProperty("text").GetString()!.Contains("Heat pump rebates expand", StringComparison.Ordinal), "latest briefing resource");
                Briefing b = (await f.User.Client.GetAsync("/v1.0/api/briefings?runId=" + run.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Briefing>>().Items.Single();
                string byId = (await c.CallAsync("resources/read", new { uri = "mmn://briefings/" + b.Id }, token).ConfigureAwait(false)).GetProperty("contents")[0].GetProperty("text").GetString()!;
                Assert.True(byId.Contains("Heat pump rebates expand", StringComparison.Ordinal), "briefing by ID");
                string topicBrief = (await c.CallAsync("resources/read", new { uri = "mmn://topics/" + f.Topic.Id + "/brief" }, token).ConfigureAwait(false)).GetProperty("contents")[0].GetProperty("text").GetString()!;
                Assert.True(topicBrief.Contains("## Dimensions", StringComparison.Ordinal) && topicBrief.Contains("Rebates", StringComparison.Ordinal) && topicBrief.Contains("blocked.test", StringComparison.Ordinal), "topic brief");
                string runSummary = (await c.CallAsync("resources/read", new { uri = "mmn://runs/" + run.Id + "/summary" }, token).ConfigureAwait(false)).GetProperty("contents")[0].GetProperty("text").GetString()!;
                Assert.True(runSummary.Contains("| Extract |", StringComparison.Ordinal) && runSummary.Contains("## Top findings", StringComparison.Ordinal) && runSummary.Contains("fnd_", StringComparison.Ordinal), "run summary");
                Assert.True(runSummary.Contains("Cost: $", StringComparison.Ordinal) && runSummary.Contains("| Cost (USD) |", StringComparison.Ordinal), "run summary shows the cost: " + runSummary);
                Assert.True(await c.ErrorCodeAsync("resources/read", new { uri = "mmn://briefings/brf_missing" }, token).ConfigureAwait(false) != 0, "missing briefing is an error");

                string review = (await c.CallAsync("prompts/get", new { name = "review_morning_briefing", arguments = new { } }, token).ConfigureAwait(false)).GetProperty("messages")[0].GetProperty("content").GetProperty("text").GetString()!;
                Assert.True(review.Contains(b.Id, StringComparison.Ordinal) && review.Contains("submit_feedback", StringComparison.Ordinal) && review.Contains("Finding fnd_", StringComparison.Ordinal), "review prompt lists items");
                string refine = (await c.CallAsync("prompts/get", new { name = "refine_topic", arguments = new { topicId = f.Topic.Id } }, token).ConfigureAwait(false)).GetProperty("messages")[0].GetProperty("content").GetProperty("text").GetString()!;
                Assert.True(refine.Contains(f.Topic.Id, StringComparison.Ordinal) && refine.Contains("set_topic_dimension", StringComparison.Ordinal) && refine.Contains("Rebates", StringComparison.Ordinal), "refine prompt");
            }
        }

        private static async Task CrossTenantAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            Run run = await ResearchedAsync(f, token).ConfigureAwait(false);
            Finding finding = (await f.User.Client.GetAsync("/v1.0/api/findings?runId=" + run.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Finding>>().Items.First();
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant other = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser stranger = await TestFixtures.CreateUserAsync(admin, other.Id, true, token).ConfigureAwait(false);
            TestUser neighbour = await TestFixtures.CreateUserAsync(admin, f.Topic.TenantId, false, token).ConfigureAwait(false);
            using (McpHost host = await StartHostAsync(token).ConfigureAwait(false))
            {
                foreach (TestUser outsider in new[] { stranger, neighbour })
                {
                    using (McpTestClient c = await ConnectAsync(host, outsider.Client.Token, token).ConfigureAwait(false))
                    {
                        Assert.Equal(0L, (await c.ToolAsync("list_topics", new { }, token).ConfigureAwait(false)).Structured.GetProperty("totalCount").GetInt64(), "no topics");
                        Assert.Equal(0L, (await c.ToolAsync("list_findings", new { }, token).ConfigureAwait(false)).Structured.GetProperty("totalCount").GetInt64(), "no findings");
                        Assert.True((await c.ToolAsync("get_topic", new { topicId = f.Topic.Id }, token).ConfigureAwait(false)).IsError, "topic hidden");
                        Assert.True((await c.ToolAsync("get_finding", new { findingId = finding.Id }, token).ConfigureAwait(false)).IsError, "finding hidden");
                        Assert.True((await c.ToolAsync("get_run", new { runId = run.Id }, token).ConfigureAwait(false)).IsError, "run hidden");
                        Assert.True((await c.ToolAsync("get_run_costs", new { runId = run.Id }, token).ConfigureAwait(false)).IsError, "run cost hidden");
                        Assert.True((await c.ToolAsync("run_topic", new { topicId = f.Topic.Id }, token).ConfigureAwait(false)).IsError, "cannot run it");
                        Assert.True((await c.ToolAsync("submit_feedback", new { targetType = "Finding", targetId = finding.Id, rating = "relevant" }, token).ConfigureAwait(false)).IsError, "cannot rate it");
                        Assert.True((await c.ToolAsync("update_topic", new { topicId = f.Topic.Id, name = "Taken over" }, token).ConfigureAwait(false)).IsError, "cannot edit it");
                        Assert.True(!(await c.ToolAsync("get_latest_briefing", new { }, token).ConfigureAwait(false)).Structured.GetProperty("found").GetBoolean(), "no briefing");
                        Assert.True(await c.ErrorCodeAsync("resources/read", new { uri = "mmn://topics/" + f.Topic.Id + "/brief" }, token).ConfigureAwait(false) != 0, "topic resource hidden");
                    }
                }
            }

            Topic still = (await f.User.Client.GetAsync("/v1.0/api/topics/" + f.Topic.Id, token).ConfigureAwait(false)).Json<TopicDetailResponse>().Topic;
            Assert.Equal(f.Topic.Name, still.Name, "topic unchanged");
        }

        private static async Task CreateTopicAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser ta = await TestFixtures.CreateUserAsync(admin, t.Id, true, token).ConfigureAwait(false);
            ModelEndpoint e = (await ta.Client.PostAsync("/v1.0/api/model-endpoints", new { name = "stub refine", provider = "OpenAiCompatible", endpoint = StubModelServer.Shared.BaseUrl, model = "stub-refine", supportsChat = true }, token)
                .ConfigureAwait(false)).Expect(201, "endpoint").Json<ModelEndpoint>();
            (await ta.Client.PutAsync("/v1.0/api/model-assignments", new { task = "Refinement", modelEndpointId = e.Id }, token).ConfigureAwait(false)).Expect(200, "assign refinement");
            using (McpHost host = await StartHostAsync(token).ConfigureAwait(false))
            using (McpTestClient c = await ConnectAsync(host, ta.Client.Token, token).ConfigureAwait(false))
            {
                Assert.True((await c.ToolAsync("create_topic", new { description = "short" }, token).ConfigureAwait(false)).IsError, "description too short");
                McpToolOutcome created = await c.ToolAsync("create_topic", new { description = "I want to learn about AI developer tools", goal = "Work decisions" }, token).ConfigureAwait(false);
                JsonElement topic = created.Structured.GetProperty("topic");
                Assert.True(!created.IsError && topic.GetProperty("status").GetString() == "Active" && created.Structured.GetProperty("dimensions").GetArrayLength() >= 3, "create_topic: " + created.Text);
                string topicId = topic.GetProperty("id").GetString()!;

                McpToolOutcome updated = await c.ToolAsync("update_topic", new { topicId = topicId, name = "AI coding tools", status = "Paused" }, token).ConfigureAwait(false);
                Assert.True(!updated.IsError && updated.Str("name") == "AI coding tools" && updated.Str("status") == "Paused", "update_topic: " + updated.Text);
                Assert.True((await c.ToolAsync("update_topic", new { topicId = topicId }, token).ConfigureAwait(false)).IsError, "nothing to change");

                // Automatic audio and schedules: both questions an assistant is asked are answered with tools.
                Assert.True(!updated.Structured.GetProperty("audioEnabled").GetBoolean(), "automatic audio starts off");
                McpToolOutcome spoken = await c.ToolAsync("update_topic", new { topicId = topicId, audioEnabled = true }, token).ConfigureAwait(false);
                Assert.True(!spoken.IsError && spoken.Structured.GetProperty("audioEnabled").GetBoolean(), "update_topic turns automatic audio on: " + spoken.Text);
                Assert.True((await c.ToolAsync("set_schedule", new { intervalType = "DaysOfWeek" }, token).ConfigureAwait(false)).IsError, "a new schedule needs a topic or an edition");
                Assert.True((await c.ToolAsync("set_schedule", new { topicId = topicId }, token).ConfigureAwait(false)).IsError, "a new schedule needs an interval type");
                Assert.True((await c.ToolAsync("set_schedule", new { topicId = topicId, intervalType = "DaysOfWeek", daysOfWeek = new[] { "Someday" }, timesOfDay = new[] { "07:00" } }, token).ConfigureAwait(false)).IsError, "unknown day name");
                Assert.True((await c.ToolAsync("set_schedule", new { topicId = topicId, intervalType = "DaysOfWeek", daysOfWeek = new[] { "Monday" }, timesOfDay = new[] { "25:00" } }, token).ConfigureAwait(false)).IsError, "the API's validation reaches the model");
                McpToolOutcome schedule = await c.ToolAsync("set_schedule", new { topicId = topicId, name = "Twice a week", intervalType = "DaysOfWeek", daysOfWeek = new[] { "Monday", "Friday" }, timesOfDay = new[] { "07:00" }, timeZone = "America/New_York" }, token).ConfigureAwait(false);
                Assert.True(!schedule.IsError && schedule.Str("ownerType") == "Topic" && schedule.Str("ownerId") == topicId && schedule.Str("timeZone") == "America/New_York" && schedule.Str("nextRunUtc") != null, "set_schedule adds: " + schedule.Text);
                Assert.True(schedule.Structured.GetProperty("daysOfWeek").EnumerateArray().Select(x => x.GetString()).SequenceEqual(new[] { "Monday", "Friday" }), "days are spelled out: " + schedule.Text);
                McpToolOutcome monthly = await c.ToolAsync("set_schedule", new { scheduleId = schedule.Str("id"), intervalType = "DaysOfMonth", daysOfMonth = new[] { 1, -1 }, timesOfDay = new[] { "06:30", "18:00" } }, token).ConfigureAwait(false);
                Assert.True(!monthly.IsError && monthly.Str("intervalType") == "DaysOfMonth" && monthly.Structured.GetProperty("daysOfMonth").GetArrayLength() == 2 && monthly.Structured.GetProperty("timesOfDay").GetArrayLength() == 2 && monthly.Str("name") == "Twice a week",
                    "set_schedule changes only what is sent: " + monthly.Text);
                McpToolOutcome listed = await c.ToolAsync("list_schedules", new { topicId = topicId }, token).ConfigureAwait(false);
                Assert.True(!listed.IsError && listed.Structured.GetProperty("totalCount").GetInt64() == 1 && listed.Structured.GetProperty("items")[0].GetProperty("id").GetString() == schedule.Str("id"), "list_schedules: " + listed.Text);
                McpToolOutcome paused = await c.ToolAsync("set_schedule", new { scheduleId = schedule.Str("id"), enabled = false }, token).ConfigureAwait(false);
                Assert.True(!paused.IsError && !paused.Structured.GetProperty("enabled").GetBoolean() && paused.Str("nextRunUtc") == null, "a paused schedule has no next run: " + paused.Text);
                McpToolOutcome removed = await c.ToolAsync("remove_schedule", new { scheduleId = schedule.Str("id") }, token).ConfigureAwait(false);
                Assert.True(!removed.IsError && removed.Structured.GetProperty("removed").GetBoolean(), "remove_schedule: " + removed.Text);
                Assert.Equal(0L, (await c.ToolAsync("list_schedules", new { topicId = topicId }, token).ConfigureAwait(false)).Structured.GetProperty("totalCount").GetInt64(), "the schedule is gone");
                McpToolOutcome dim = await c.ToolAsync("set_topic_dimension", new { topicId = topicId, name = "Pricing changes", weight = "High" }, token).ConfigureAwait(false);
                Assert.True(!dim.IsError && dim.Str("weight") == "High", "add dimension: " + dim.Text);
                McpToolOutcome renamed = await c.ToolAsync("set_topic_dimension", new { topicId = topicId, dimensionId = dim.Str("id"), name = "Pricing" }, token).ConfigureAwait(false);
                Assert.True(!renamed.IsError && renamed.Str("name") == "Pricing", "change dimension");
                McpToolOutcome con = await c.ToolAsync("add_topic_constraint", new { topicId = topicId, kind = "BlockedDomain", value = "lowquality.test", note = "Low quality" }, token).ConfigureAwait(false);
                Assert.True(!con.IsError && con.Str("kind") == "BlockedDomain", "add constraint: " + con.Text);
                Assert.True(!(await c.ToolAsync("remove_topic_constraint", new { topicId = topicId, constraintId = con.Str("id") }, token).ConfigureAwait(false)).IsError, "remove constraint");
                Assert.True(!(await c.ToolAsync("remove_topic_dimension", new { topicId = topicId, dimensionId = dim.Str("id") }, token).ConfigureAwait(false)).IsError, "remove dimension");
                TopicDetailResponse d = (await ta.Client.GetAsync("/v1.0/api/topics/" + topicId, token).ConfigureAwait(false)).Json<TopicDetailResponse>();
                Assert.True(d.Topic.Name == "AI coding tools" && d.Dimensions.All(x => x.Id != dim.Str("id")) && d.Constraints.All(x => x.Value != "lowquality.test"), "changes persisted: " + d.Topic.Name + "; " + String.Join(",", d.Dimensions.Select(x => x.Id + "=" + x.Name)) + "; " + String.Join(",", d.Constraints.Select(x => x.Value)) + "; dim " + dim.Str("id"));
                Assert.True(!String.IsNullOrWhiteSpace(d.Topic.Brief), "brief drafted");
                EnumerationResult<LlmCall> calls = (await ta.Client.GetAsync("/v1.0/api/llm-calls?topicId=" + topicId, token).ConfigureAwait(false)).Json<EnumerationResult<LlmCall>>();
                Assert.True(calls.Items.Count >= 2 && calls.Items.All(x => x.UserId == ta.User.Id), "wizard calls attributed to the caller");
            }
        }

        private static async Task StdioAsync(CancellationToken token)
        {
            if (OperatingSystem.IsWindows()) return;
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            string dll = typeof(McpHost).Assembly.Location;
            Assert.True(File.Exists(Path.ChangeExtension(dll, ".runtimeconfig.json")), "MCP server runtime config next to " + dll);
            using (McpClient client = new McpClient())
            {
                string[] args = { "MYMORNINGNEWS_TOKEN=" + f.User.Client.Token, "MYMORNINGNEWS_MCP_TELEMETRY=false", "dotnet", dll, "--stdio", "--api-url", await SharedTestServer.GetBaseUrlAsync(token).ConfigureAwait(false) };
                Assert.True(await client.LaunchServerAsync("/usr/bin/env", args, token).ConfigureAwait(false), "stdio server launched");
                JsonElement tools = McpTestClient.Parse(await client.CallAsync<JsonElement>("tools/list", null, 30000, token).ConfigureAwait(false));
                Assert.Equal(McpSurface.AllTools().Count, tools.GetProperty("tools").GetArrayLength(), "same tools over stdio");
                JsonElement result = McpTestClient.Parse(await client.CallAsync<JsonElement>("tools/call", new Dictionary<string, object> { { "name", "list_topics" }, { "arguments", new { } } }, 30000, token).ConfigureAwait(false));
                McpToolOutcome topics = McpTestClient.ToOutcome(result);
                Assert.True(!topics.IsError && topics.Structured.GetProperty("items").EnumerateArray().Any(t => t.GetProperty("id").GetString() == f.Topic.Id), "list_topics over stdio: " + topics.Text);
            }
        }
    }
}
