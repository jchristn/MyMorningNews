namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Mcp;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// MCP server registry (plan section 15.1): register remote MCP servers, test them, discover their tools, and choose
    /// which tools agentic research may use. Tenant administrators manage it; members can read it.
    /// </summary>
    public class McpServerRoutes : RouteBase
    {
        private const string B = "/v1.0/api/mcp-servers";

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public McpServerRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, B, ListAsync, null, openApiMetadata: Paged(Doc("List registered MCP servers", "MCP servers").WithParameter(QueryParam("enabled", typeof(bool)))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, B, CreateAsync, null, openApiMetadata: Doc("Register an MCP server", "MCP servers",
                "Streamable HTTP or SSE URL; the credential comes from the vault and is sent in HeaderName with HeaderPrefix. stdio servers are not supported from the container.").WithRequestBody(Body<McpServerUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, B + "/{id}", ReadAsync, null, openApiMetadata: Doc("Read an MCP server with its tools", "MCP servers"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, B + "/{id}", UpdateAsync, null, openApiMetadata: Doc("Change an MCP server", "MCP servers").WithRequestBody(Body<McpServerUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, B + "/{id}", DeleteAsync, null, openApiMetadata: Doc("Remove an MCP server and its tools", "MCP servers"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, B + "/{id}/test", TestAsync, null, openApiMetadata: Doc("Connect and list tools without saving them", "MCP servers"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, B + "/{id}/discover", DiscoverAsync, null, openApiMetadata: Doc("Discover the server's tools and save them", "MCP servers",
                "New tools start disabled; existing tools keep their setting; tools the server no longer lists are removed."));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, B + "/{id}/tools/{toolId}", ToolAsync, null, openApiMetadata: Doc("Turn a tool on or off for agentic research", "MCP servers").WithRequestBody(Body<McpToolUpdateRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/runs/{id}/tool-calls", RunToolCallsAsync, null, openApiMetadata: Doc("A run's agentic tool calls, in order", "Runs"));
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.McpServerRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            McpServerFilter filter = new McpServerFilter { TenantId = tenantId, Enabled = QueryBool(ctx, "enabled") };
            ApplyPaging(ctx, filter);
            await SendJsonAsync(ctx, 200, await Server.Database.McpServers.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task CreateAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.McpServerWrite).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            McpServerUpsertRequest? req = await ReadBodyAsync<McpServerUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            if (String.IsNullOrWhiteSpace(req.Name) || String.IsNullOrWhiteSpace(req.Url))
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "name and url are required.").ConfigureAwait(false);
                return;
            }

            McpServer server = new McpServer { TenantId = tenantId, Name = req.Name.Trim(), Url = req.Url.Trim() };
            if (!await ApplyAsync(ctx, server, req).ConfigureAwait(false)) return;
            await SendJsonAsync(ctx, 201, await Server.Database.McpServers.CreateAsync(server, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            McpServer? server = await LoadAsync(ctx, RequestTypeEnum.McpServerRead).ConfigureAwait(false);
            if (server == null) return;
            await SendJsonAsync(ctx, 200, new McpServerDetailResponse { Server = server, Tools = await ToolsAsync(server, ctx).ConfigureAwait(false) }).ConfigureAwait(false);
        }

        private async Task UpdateAsync(HttpContextBase ctx)
        {
            McpServer? server = await LoadAsync(ctx, RequestTypeEnum.McpServerWrite).ConfigureAwait(false);
            if (server == null) return;
            McpServerUpsertRequest? req = await ReadBodyAsync<McpServerUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            if (req.Name != null)
            {
                if (String.IsNullOrWhiteSpace(req.Name)) { await SendErrorAsync(ctx, 400, "ValidationFailed", "name cannot be empty.").ConfigureAwait(false); return; }
                server.Name = req.Name.Trim();
            }

            if (req.Url != null) server.Url = req.Url.Trim();
            if (!await ApplyAsync(ctx, server, req).ConfigureAwait(false)) return;
            await SendJsonAsync(ctx, 200, await Server.Database.McpServers.UpdateAsync(server, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            McpServer? server = await LoadAsync(ctx, RequestTypeEnum.McpServerWrite).ConfigureAwait(false);
            if (server == null) return;
            List<McpServerTool> removed = await ToolsAsync(server, ctx).ConfigureAwait(false);
            HashSet<string> removedIds = new HashSet<string>(removed.Select(x => x.Id), StringComparer.Ordinal);
            foreach (McpServerTool t in removed)
                await Server.Database.McpServerTools.DeleteAsync(server.TenantId, t.Id, ctx.Token).ConfigureAwait(false);
            if (removedIds.Count > 0)
            {
                foreach (Topic topic in (await Server.Database.Topics.EnumerateAsync(new TopicFilter { TenantId = server.TenantId, PageSize = 1000 }, ctx.Token).ConfigureAwait(false)).Items.Where(x => x.McpToolIds.Any(removedIds.Contains)))
                {
                    topic.McpToolIds = topic.McpToolIds.Where(id => !removedIds.Contains(id)).ToList();
                    await Server.Database.Topics.UpdateAsync(topic, ctx.Token).ConfigureAwait(false);
                }
            }
            await Server.Database.McpServers.DeleteAsync(server.TenantId, server.Id, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task TestAsync(HttpContextBase ctx)
        {
            McpServer? server = await LoadAsync(ctx, RequestTypeEnum.McpServerWrite).ConfigureAwait(false);
            if (server == null) return;
            try
            {
                List<McpDiscoveredTool> tools = await Server.Mcp.DiscoverAsync(server, ctx.Token).ConfigureAwait(false);
                await SendJsonAsync(ctx, 200, new Dictionary<string, object?> { { "success", true }, { "toolCount", tools.Count }, { "tools", tools.Select(t => t.Name).ToList() } }).ConfigureAwait(false);
            }
            catch (McpConnectionException ex)
            {
                await SendJsonAsync(ctx, 200, new Dictionary<string, object?> { { "success", false }, { "error", ex.Message } }).ConfigureAwait(false);
            }
        }

        private async Task DiscoverAsync(HttpContextBase ctx)
        {
            McpServer? server = await LoadAsync(ctx, RequestTypeEnum.McpServerWrite).ConfigureAwait(false);
            if (server == null) return;
            List<McpDiscoveredTool> found;
            try
            {
                found = await Server.Mcp.DiscoverAsync(server, ctx.Token).ConfigureAwait(false);
            }
            catch (McpConnectionException ex)
            {
                server.LastError = ex.Message;
                await Server.Database.McpServers.UpdateAsync(server, ctx.Token).ConfigureAwait(false);
                await SendErrorAsync(ctx, 502, "McpServerUnavailable", ex.Message).ConfigureAwait(false);
                return;
            }

            List<McpServerTool> existing = await ToolsAsync(server, ctx).ConfigureAwait(false);
            foreach (McpServerTool gone in existing.Where(e => found.All(f => f.Name != e.Name)))
                await Server.Database.McpServerTools.DeleteAsync(server.TenantId, gone.Id, ctx.Token).ConfigureAwait(false);
            foreach (McpDiscoveredTool f in found)
            {
                McpServerTool? have = existing.FirstOrDefault(e => e.Name == f.Name);
                if (have == null)
                {
                    await Server.Database.McpServerTools.CreateAsync(new McpServerTool
                    {
                        TenantId = server.TenantId, McpServerId = server.Id, Name = f.Name, Description = f.Description, InputSchema = f.InputSchema, Enabled = false
                    }, ctx.Token).ConfigureAwait(false);
                }
                else
                {
                    have.Description = f.Description;
                    have.InputSchema = f.InputSchema;
                    await Server.Database.McpServerTools.UpdateAsync(have, ctx.Token).ConfigureAwait(false);
                }
            }

            server.ToolCount = found.Count;
            server.LastDiscoveredUtc = DateTime.UtcNow;
            server.LastError = null;
            await Server.Database.McpServers.UpdateAsync(server, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, new McpServerDetailResponse { Server = server, Tools = await ToolsAsync(server, ctx).ConfigureAwait(false) }).ConfigureAwait(false);
        }

        private async Task ToolAsync(HttpContextBase ctx)
        {
            McpServer? server = await LoadAsync(ctx, RequestTypeEnum.McpServerWrite).ConfigureAwait(false);
            if (server == null) return;
            McpToolUpdateRequest? req = await ReadBodyAsync<McpToolUpdateRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            McpServerTool? tool = await Server.Database.McpServerTools.ReadAsync(server.TenantId, Param(ctx, "toolId") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (tool == null || tool.McpServerId != server.Id) { await SendNotFoundAsync(ctx, "Tool").ConfigureAwait(false); return; }
            tool.Enabled = req.Enabled;
            await SendJsonAsync(ctx, 200, await Server.Database.McpServerTools.UpdateAsync(tool, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task RunToolCallsAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.RunRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            Run? run = await Server.Database.Runs.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (run == null || !IsOwnerOrAdmin(ctx, run.UserId)) { await SendNotFoundAsync(ctx, "Run").ConfigureAwait(false); return; }
            List<RunToolCall> calls = (await Server.Database.RunToolCalls.EnumerateAsync(new RunToolCallFilter { TenantId = tenantId, RunId = run.Id, PageSize = 500 }, ctx.Token).ConfigureAwait(false)).Items
                .OrderBy(c => c.SortOrder).ToList();
            await SendJsonAsync(ctx, 200, calls).ConfigureAwait(false);
        }

        private async Task<bool> ApplyAsync(HttpContextBase ctx, McpServer server, McpServerUpsertRequest req)
        {
            if (server.Name.Length > 128) { await SendErrorAsync(ctx, 400, "ValidationFailed", "name must be at most 128 characters.").ConfigureAwait(false); return false; }
            if (!Uri.TryCreate(server.Url, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) || server.Url.Length > 2048)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "url must be an absolute http or https URL (stdio servers are not supported).").ConfigureAwait(false);
                return false;
            }

            await MyMorningNews.Core.Security.NetworkGuard.EnsureAllowedAsync(server.Url, ctx.Token).ConfigureAwait(false);
            if (req.Transport.HasValue && req.Transport.Value != McpTransportEnum.StreamableHttp)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "Only the Streamable HTTP transport is supported.").ConfigureAwait(false);
                return false;
            }

            if (req.Transport.HasValue) server.Transport = req.Transport.Value;
            if (req.HeaderName != null) server.HeaderName = String.IsNullOrWhiteSpace(req.HeaderName) ? "Authorization" : req.HeaderName.Trim();
            if (req.HeaderPrefix != null) server.HeaderPrefix = req.HeaderPrefix;
            if (req.Enabled.HasValue) server.Enabled = req.Enabled.Value;
            if (req.VaultCredentialId != null)
            {
                if (req.VaultCredentialId.Length == 0) server.VaultCredentialId = null;
                else
                {
                    VaultCredential? cred = await Server.Database.VaultCredentials.ReadAsync(server.TenantId, req.VaultCredentialId, ctx.Token).ConfigureAwait(false);
                    if (cred == null) { await SendErrorAsync(ctx, 400, "ValidationFailed", "vaultCredentialId was not found in this tenant's vault.").ConfigureAwait(false); return false; }
                    server.VaultCredentialId = cred.Id;
                }
            }

            return true;
        }

        private async Task<McpServer?> LoadAsync(HttpContextBase ctx, RequestTypeEnum type)
        {
            if (!await AuthorizeAsync(ctx, type).ConfigureAwait(false)) return null;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return null;
            McpServer? server = await Server.Database.McpServers.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (server == null) { await SendNotFoundAsync(ctx, "MCP server").ConfigureAwait(false); return null; }
            return server;
        }

        private async Task<List<McpServerTool>> ToolsAsync(McpServer server, HttpContextBase ctx)
        {
            return (await Server.Database.McpServerTools.EnumerateAsync(new McpServerToolFilter { TenantId = server.TenantId, McpServerId = server.Id, PageSize = 500 }, ctx.Token).ConfigureAwait(false)).Items
                .OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
        }
    }
}
