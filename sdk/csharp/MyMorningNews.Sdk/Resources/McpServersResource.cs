namespace MyMorningNews.Sdk.Resources
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// MCP server registry (/v1.0/api/mcp-servers): register remote MCP servers (Streamable HTTP or SSE URLs; stdio is not
    /// supported), test them, discover their tools, and choose which tools agentic research may use. Tenant administrators
    /// manage the registry; members can read it.
    /// Thread safety: stateless; safe to share.
    /// </summary>
    public class McpServersResource : ResourceBase
    {
        private const string BasePath = "/v1.0/api/mcp-servers";

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public McpServersResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List registered MCP servers.
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of McpServer items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<McpServer>> ListAsync(McpServerListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<McpServer>>(HttpMethod.Get, BasePath, (options ?? new McpServerListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read an MCP server with its discovered tools (sorted by name).
        /// </summary>
        /// <param name="id">Server identifier (mcs_...).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The McpServerDetailResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error (404 when not found).</exception>
        public Task<McpServerDetailResponse> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<McpServerDetailResponse>(HttpMethod.Get, BasePath + "/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Register an MCP server (HTTP 201). Name and Url are required; the credential, when set, comes from the vault
        /// (VaultCredentialId) and is sent in HeaderName (default Authorization) with HeaderPrefix (default "Bearer ").
        /// 400 ValidationFailed for a missing name or a URL that is not absolute http or https.
        /// </summary>
        /// <param name="request">Server settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The McpServer.</returns>
        /// <exception cref="ArgumentNullException">request is null.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<McpServer> CreateAsync(McpServerUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<McpServer>(HttpMethod.Post, BasePath, null, request, token);
        }

        /// <summary>
        /// Change an MCP server. Only the fields you set change; set VaultCredentialId to an empty string to remove the
        /// credential.
        /// </summary>
        /// <param name="id">Server identifier.</param>
        /// <param name="request">Changes.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The McpServer.</returns>
        /// <exception cref="ArgumentNullException">id or request is null.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<McpServer> UpdateAsync(string id, McpServerUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<McpServer>(HttpMethod.Put, BasePath + "/" + MyMorningNewsClient.Segment(id, nameof(id)), null, request, token);
        }

        /// <summary>
        /// Remove an MCP server and its tools (HTTP 204).
        /// </summary>
        /// <param name="id">Server identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, BasePath + "/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Connect to the server and list its tools without saving them. A failed connection is HTTP 200 with Success false.
        /// </summary>
        /// <param name="id">Server identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The McpServerTestResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<McpServerTestResponse> TestAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<McpServerTestResponse>(HttpMethod.Post, BasePath + "/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/test", null, null, token);
        }

        /// <summary>
        /// Discover the server's tools and save them. New tools start disabled, existing tools keep their setting, and tools
        /// the server no longer lists are removed. 502 McpServerUnavailable when the server cannot be reached (the error is
        /// also saved in LastError).
        /// </summary>
        /// <param name="id">Server identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The McpServerDetailResponse with the saved tools.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<McpServerDetailResponse> DiscoverAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<McpServerDetailResponse>(HttpMethod.Post, BasePath + "/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/discover", null, null, token);
        }

        /// <summary>
        /// Turn a discovered tool on or off for agentic research.
        /// </summary>
        /// <param name="id">Server identifier.</param>
        /// <param name="toolId">Tool identifier (mct_...).</param>
        /// <param name="enabled">Whether agentic research may call the tool.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The McpServerTool.</returns>
        /// <exception cref="ArgumentNullException">id or toolId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error (404 when the tool is not on this server).</exception>
        public Task<McpServerTool> SetToolEnabledAsync(string id, string toolId, bool enabled, CancellationToken token = default)
        {
            return Client.SendAsync<McpServerTool>(HttpMethod.Put, BasePath + "/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/tools/" + MyMorningNewsClient.Segment(toolId, nameof(toolId)),
                null, new McpToolUpdateRequest { Enabled = enabled }, token);
        }
    }
}
