namespace MyMorningNews.Core.Mcp
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Services;
    using Voltaic.Core;
    using Voltaic.Mcp;
    using McpServer = MyMorningNews.Core.Models.McpServer;

    /// <summary>
    /// Talks to tenant-registered MCP servers with the Voltaic client: test a connection, discover tools, and call a
    /// tool. The credential, when set, is read from the vault for each connection and sent in the configured header.
    /// Thread safe (a new client per operation).
    /// </summary>
    public class McpClientService
    {
        #region Public-Members

        /// <summary>
        /// Timeout for one MCP request in milliseconds. Default 30000. Minimum 1000, maximum 300000.
        /// </summary>
        public int RequestTimeoutMs
        {
            get { return _RequestTimeoutMs; }
            set { _RequestTimeoutMs = Math.Clamp(value, 1000, 300000); }
        }

        #endregion

        #region Private-Members

        private readonly VaultService _Vault;
        private int _RequestTimeoutMs = 30000;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the service.
        /// </summary>
        /// <param name="vault">Vault, for the server credential.</param>
        /// <exception cref="ArgumentNullException">Vault is null.</exception>
        public McpClientService(VaultService vault)
        {
            _Vault = vault ?? throw new ArgumentNullException(nameof(vault));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// List the server's tools.
        /// </summary>
        /// <param name="server">Server.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tools.</returns>
        /// <exception cref="ArgumentNullException">Server is null.</exception>
        /// <exception cref="McpConnectionException">The server could not be reached or refused the request.</exception>
        public async Task<List<McpDiscoveredTool>> DiscoverAsync(McpServer server, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(server);
            using (McpHttpClient client = await ConnectAsync(server, token).ConfigureAwait(false))
            {
                List<McpDiscoveredTool> tools = new List<McpDiscoveredTool>();
                string? cursor = null;
                for (int page = 0; page < 20; page++)
                {
                    object? args = cursor == null ? null : new Dictionary<string, object> { { "cursor", cursor } };
                    JsonElement result = Result(await client.CallAsync("tools/list", args, _RequestTimeoutMs, token).ConfigureAwait(false), "tools/list");
                    if (result.TryGetProperty("tools", out JsonElement list) && list.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement t in list.EnumerateArray())
                        {
                            string? name = t.TryGetProperty("name", out JsonElement n) ? n.GetString() : null;
                            if (String.IsNullOrWhiteSpace(name)) continue;
                            tools.Add(new McpDiscoveredTool
                            {
                                Name = name,
                                Description = t.TryGetProperty("description", out JsonElement d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null,
                                InputSchema = t.TryGetProperty("inputSchema", out JsonElement s) ? s.GetRawText() : null
                            });
                        }
                    }

                    cursor = result.TryGetProperty("nextCursor", out JsonElement c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
                    if (String.IsNullOrEmpty(cursor)) break;
                }

                return tools;
            }
        }

        /// <summary>
        /// Call a tool and return its text content (and structured content as JSON when there is no text).
        /// </summary>
        /// <param name="server">Server.</param>
        /// <param name="toolName">Tool name.</param>
        /// <param name="argumentsJson">Arguments as a JSON object.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Text for the model. A tool error comes back as text starting with "Error:".</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="McpConnectionException">The server could not be reached.</exception>
        public async Task<string> CallToolAsync(McpServer server, string toolName, string argumentsJson, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(server);
            if (String.IsNullOrWhiteSpace(toolName)) throw new ArgumentNullException(nameof(toolName));
            JsonElement arguments;
            using (JsonDocument doc = JsonDocument.Parse(String.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson))
            {
                arguments = doc.RootElement.Clone();
            }

            using (McpHttpClient client = await ConnectAsync(server, token).ConfigureAwait(false))
            {
                JsonElement result = Result(await client.CallAsync("tools/call", new Dictionary<string, object> { { "name", toolName }, { "arguments", arguments } }, _RequestTimeoutMs, token).ConfigureAwait(false), "tools/call");
                StringBuilder sb = new StringBuilder();
                if (result.TryGetProperty("content", out JsonElement content) && content.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement part in content.EnumerateArray())
                    {
                        string type = part.TryGetProperty("type", out JsonElement ty) ? ty.GetString() ?? String.Empty : String.Empty;
                        if (type == "text" && part.TryGetProperty("text", out JsonElement text)) sb.AppendLine(text.GetString());
                        else if (type == "resource_link" && part.TryGetProperty("uri", out JsonElement uri)) sb.AppendLine("[resource] " + uri.GetString());
                        else if (type.Length > 0) sb.AppendLine("[" + type + " content omitted]");
                    }
                }

                if (sb.Length == 0 && result.TryGetProperty("structuredContent", out JsonElement structured)) sb.Append(structured.GetRawText());
                bool isError = result.TryGetProperty("isError", out JsonElement err) && err.ValueKind == JsonValueKind.True;
                string body = sb.ToString().Trim();
                return isError ? "Error: " + (body.Length > 0 ? body : "the tool reported an error.") : body;
            }
        }

        #endregion

        #region Private-Methods

        private async Task<McpHttpClient> ConnectAsync(McpServer server, CancellationToken token)
        {
            if (!Uri.TryCreate(server.Url, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new McpConnectionException("The MCP server URL must be an absolute http or https URL.");
            McpHttpClient client = new McpHttpClient();
            try
            {
                client.RequestTimeoutMs = _RequestTimeoutMs;
                if (!String.IsNullOrEmpty(server.VaultCredentialId))
                {
                    string? secret;
                    try
                    {
                        secret = await _Vault.ResolveAsync(server.TenantId, server.VaultCredentialId, token).ConfigureAwait(false);
                    }
                    catch (VaultSecretUnavailableException ex)
                    {
                        throw new McpConnectionException("The MCP server credential is unavailable: " + ex.Message, ex);
                    }

                    if (!String.IsNullOrEmpty(secret)) client.SetRequestHeader(String.IsNullOrWhiteSpace(server.HeaderName) ? "Authorization" : server.HeaderName, (server.HeaderPrefix ?? String.Empty) + secret);
                }

                string baseUrl = uri.GetLeftPart(UriPartial.Authority);
                string path = String.IsNullOrEmpty(uri.PathAndQuery) || uri.PathAndQuery == "/" ? "/mcp" : uri.PathAndQuery;
                if (!await client.ConnectStreamableAsync(baseUrl, path, token).ConfigureAwait(false))
                    throw new McpConnectionException("Could not connect to the MCP server at " + server.Url + ".");
                return client;
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        private static JsonElement Result(JsonRpcResponse response, string method)
        {
            if (response.Error != null) throw new McpConnectionException("The MCP server refused " + method + ": " + response.Error.Message);
            if (response.Result is JsonElement e) return e.Clone();
            using (JsonDocument doc = JsonDocument.Parse(JsonSerializer.Serialize(response.Result)))
            {
                return doc.RootElement.Clone();
            }
        }

        #endregion
    }
}
