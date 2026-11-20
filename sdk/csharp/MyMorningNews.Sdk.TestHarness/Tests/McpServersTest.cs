namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// MCP server registry: validation, create, read, list, update, test and discover against a fake MCP server the harness
    /// runs on 127.0.0.1, turn a tool on, use it on an agentic topic, an unreachable server (test fails softly, discover is a
    /// typed 502), a missing tool (404), run tool calls, and delete.
    /// </summary>
    public class McpServersTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "MCP servers CRUD, test, discover, tools, agentic topic, run tool calls";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public McpServersTest()
        {
        }

        /// <summary>
        /// Run the case.
        /// </summary>
        /// <param name="context">Shared state.</param>
        /// <param name="result">Result.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public override async Task RunAsync(TestContext context, TestResult result, CancellationToken token)
        {
            MyMorningNewsClient c = context.RequireAdmin();
            await StepAsync(context, result, "POST mcp-servers without a url (400)", () => Helpers.ExpectErrorAsync(() => c.McpServers.CreateAsync(new McpServerUpsertRequest { Name = context.Name("mcp") }, token), 400, "ValidationFailed")).ConfigureAwait(false);
            await StepAsync(context, result, "POST mcp-servers with a non-http url (400)", () => Helpers.ExpectErrorAsync(() => c.McpServers.CreateAsync(new McpServerUpsertRequest { Name = context.Name("mcp"), Url = "ftp://127.0.0.1/mcp" }, token), 400, "ValidationFailed")).ConfigureAwait(false);
            await StepAsync(context, result, "GET runs/{missing}/tool-calls (404)", () => Helpers.ExpectErrorAsync(() => c.Runs.ListToolCallsAsync("run_missing_" + Helpers.Unique(), token), 404)).ConfigureAwait(false);
            EnumerationResult<Run> runs = await StepAsync(context, result, "GET runs (for tool calls)", () => c.Runs.ListAsync(new RunListOptions { PageSize = 1 }, token)).ConfigureAwait(false);
            if (runs.Items.Count > 0)
            {
                List<RunToolCall> calls = await StepAsync(context, result, "GET runs/{id}/tool-calls", () => c.Runs.ListToolCallsAsync(runs.Items[0].Id, token)).ConfigureAwait(false);
                Helpers.True(calls.All(x => x.RunId == runs.Items[0].Id), "tool calls belong to the run");
            }

            McpServer dead = await StepAsync(context, result, "POST mcp-servers (unreachable)", () => c.McpServers.CreateAsync(new McpServerUpsertRequest
            {
                Name = context.Name("mcp-dead"),
                Url = "http://127.0.0.1:9/mcp",
                Enabled = false
            }, token)).ConfigureAwait(false);
            context.Cleanup.Add("mcp:" + dead.Id, ct => c.McpServers.DeleteAsync(dead.Id, ct));
            Helpers.True(dead.Id.StartsWith("mcs_", StringComparison.Ordinal), "server ID prefix");
            Helpers.True(!dead.Enabled, "created disabled");
            Helpers.Equal("Authorization", dead.HeaderName, "default header name");
            McpServerTestResponse failed = await StepAsync(context, result, "POST mcp-servers/{id}/test (unreachable, 200)", () => c.McpServers.TestAsync(dead.Id, token)).ConfigureAwait(false);
            Helpers.True(!failed.Success && !String.IsNullOrEmpty(failed.Error), "a failed test reports success false with an error");
            await StepAsync(context, result, "POST mcp-servers/{id}/discover (unreachable, 502)", () => Helpers.ExpectErrorAsync(() => c.McpServers.DiscoverAsync(dead.Id, token), 502, "McpServerUnavailable")).ConfigureAwait(false);
            McpServerDetailResponse deadRead = await StepAsync(context, result, "GET mcp-servers/{id} records the error", () => c.McpServers.GetAsync(dead.Id, token)).ConfigureAwait(false);
            Helpers.True(!String.IsNullOrEmpty(deadRead.Server.LastError), "lastError is saved");
            await StepAsync(context, result, "PUT mcp-servers/{id}/tools/{missing} (404)", () => Helpers.ExpectErrorAsync(() => c.McpServers.SetToolEnabledAsync(dead.Id, "mct_missing_" + Helpers.Unique(), true, token), 404)).ConfigureAwait(false);
            EnumerationResult<McpServer> disabled = await StepAsync(context, result, "GET mcp-servers?enabled=false", () => c.McpServers.ListAsync(new McpServerListOptions { Enabled = false, PageSize = 100 }, token)).ConfigureAwait(false);
            Helpers.Contains(disabled.Items, s => s.Id == dead.Id, "the disabled server");
            await StepAsync(context, result, "DELETE mcp-servers/{id}", () => c.McpServers.DeleteAsync(dead.Id, token)).ConfigureAwait(false);
            context.Cleanup.Remove("mcp:" + dead.Id);
            await StepAsync(context, result, "GET deleted mcp server (404)", () => Helpers.ExpectErrorAsync(() => c.McpServers.GetAsync(dead.Id, token), 404)).ConfigureAwait(false);

            using (FakeMcpServer fake = new FakeMcpServer())
            {
                McpServer server = await StepAsync(context, result, "POST mcp-servers (fake)", () => c.McpServers.CreateAsync(new McpServerUpsertRequest
                {
                    Name = context.Name("mcp"),
                    Url = fake.Url,
                    Transport = McpTransportEnum.StreamableHttp
                }, token)).ConfigureAwait(false);
                context.Cleanup.Add("mcp:" + server.Id, ct => c.McpServers.DeleteAsync(server.Id, ct));
                Helpers.Equal(fake.Url, server.Url, "url");
                Helpers.True(server.Enabled, "enabled by default");

                McpServer renamed = await StepAsync(context, result, "PUT mcp-servers/{id}", () => c.McpServers.UpdateAsync(server.Id, new McpServerUpsertRequest { Name = server.Name + "-renamed", HeaderPrefix = "Token " }, token)).ConfigureAwait(false);
                Helpers.Equal(server.Name + "-renamed", renamed.Name, "renamed");
                Helpers.Equal("Token ", renamed.HeaderPrefix, "header prefix");
                await StepAsync(context, result, "PUT mcp-servers/{id} with an empty name (400)", () => Helpers.ExpectErrorAsync(() => c.McpServers.UpdateAsync(server.Id, new McpServerUpsertRequest { Name = " " }, token), 400, "ValidationFailed")).ConfigureAwait(false);
                EnumerationResult<McpServer> enabled = await StepAsync(context, result, "GET mcp-servers?enabled=true", () => c.McpServers.ListAsync(new McpServerListOptions { Enabled = true, PageSize = 100 }, token)).ConfigureAwait(false);
                Helpers.Contains(enabled.Items, s => s.Id == server.Id, "the server");

                McpServerTestResponse ok = await StepAsync(context, result, "POST mcp-servers/{id}/test (fake)", () => c.McpServers.TestAsync(server.Id, token)).ConfigureAwait(false);
                if (!ok.Success)
                    throw new SkipTestException("the server cannot reach the harness's fake MCP server at " + fake.Url + " (is the server in a container?): " + ok.Error);
                Helpers.Equal(fake.ToolNames.Count, ok.ToolCount, "tool count");
                Helpers.True(fake.ToolNames.All(n => ok.Tools.Contains(n)), "tool names");
                McpServerDetailResponse before = await StepAsync(context, result, "GET mcp-servers/{id} (test saves nothing)", () => c.McpServers.GetAsync(server.Id, token)).ConfigureAwait(false);
                Helpers.Equal(0, before.Tools.Count, "no tools saved by test");

                McpServerDetailResponse found = await StepAsync(context, result, "POST mcp-servers/{id}/discover (fake)", () => c.McpServers.DiscoverAsync(server.Id, token)).ConfigureAwait(false);
                Helpers.Equal(fake.ToolNames.Count, found.Tools.Count, "discovered tools");
                Helpers.Equal(fake.ToolNames.Count, found.Server.ToolCount, "server tool count");
                Helpers.True(found.Server.LastDiscoveredUtc.HasValue, "lastDiscoveredUtc");
                Helpers.True(found.Tools.All(t => !t.Enabled && t.McpServerId == server.Id && t.Id.StartsWith("mct_", StringComparison.Ordinal)), "new tools start disabled");
                McpServerTool echo = found.Tools.First(t => t.Name == "harness_echo");
                Helpers.True(!String.IsNullOrEmpty(echo.InputSchema), "input schema saved");

                McpServerTool on = await StepAsync(context, result, "PUT mcp-servers/{id}/tools/{toolId} on", () => c.McpServers.SetToolEnabledAsync(server.Id, echo.Id, true, token)).ConfigureAwait(false);
                Helpers.True(on.Enabled, "tool enabled");
                McpServerDetailResponse again = await StepAsync(context, result, "POST discover again keeps the setting", () => c.McpServers.DiscoverAsync(server.Id, token)).ConfigureAwait(false);
                Helpers.True(again.Tools.Single(t => t.Name == "harness_echo").Enabled, "rediscovery keeps the enabled tool on");

                Topic topic = await StepAsync(context, result, "POST topic with agenticSearch and mcpToolIds", async () =>
                {
                    Topic t = await c.Topics.CreateAsync(new TopicUpsertRequest
                    {
                        Name = context.Name("agentic"),
                        Brief = "Harness data for agentic search fields; never run.",
                        AgenticSearch = true,
                        McpToolIds = new List<string> { echo.Id, "not-a-tool-id" }
                    }, token).ConfigureAwait(false);
                    context.Cleanup.Add("topic:" + t.Id, ct => c.Topics.DeleteAsync(t.Id, ct));
                    return t;
                }).ConfigureAwait(false);
                Helpers.True(topic.AgenticSearch, "agenticSearch saved");
                Helpers.True(topic.McpToolIds.Count == 1 && topic.McpToolIds[0] == echo.Id, "mcpToolIds keeps only mct_ IDs");
                TopicDetailResponse detail = await StepAsync(context, result, "GET topic (agentic fields)", () => c.Topics.GetAsync(topic.Id, token)).ConfigureAwait(false);
                Helpers.True(detail.Topic.AgenticSearch && detail.Topic.McpToolIds.Contains(echo.Id), "agentic fields read back");
                Topic cleared = await StepAsync(context, result, "PUT topic clears mcpToolIds", () => c.Topics.UpdateAsync(topic.Id, new TopicUpsertRequest { AgenticSearch = false, McpToolIds = new List<string>() }, token)).ConfigureAwait(false);
                Helpers.True(!cleared.AgenticSearch && cleared.McpToolIds.Count == 0, "agentic fields cleared");
                await StepAsync(context, result, "DELETE agentic topic", () => c.Topics.DeleteAsync(topic.Id, token)).ConfigureAwait(false);
                context.Cleanup.Remove("topic:" + topic.Id);

                await StepAsync(context, result, "DELETE mcp-servers/{id} (fake)", () => c.McpServers.DeleteAsync(server.Id, token)).ConfigureAwait(false);
                context.Cleanup.Remove("mcp:" + server.Id);
                Helpers.True(fake.RequestCount > 0, "the server talked to the fake MCP server");
            }

            using (MyMorningNewsClient anon = context.NewClient())
            {
                await StepAsync(context, result, "GET mcp-servers without credentials (401)", () => Helpers.ExpectErrorAsync(() => anon.McpServers.ListAsync(null, token), 401)).ConfigureAwait(false);
            }
        }
    }
}
