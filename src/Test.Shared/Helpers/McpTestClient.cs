namespace Test.Shared.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Voltaic.Core;
    using Voltaic.Mcp;

    /// <summary>
    /// A Streamable HTTP MCP client for tests, with result parsing helpers.
    /// </summary>
    public class McpTestClient : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// The underlying Voltaic client.
        /// </summary>
        public McpHttpClient Client { get; } = new McpHttpClient();

        #endregion

        #region Private-Members

        private bool _Disposed = false;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Connect with request headers (for example Authorization).
        /// </summary>
        /// <param name="baseUrl">MCP server base URL.</param>
        /// <param name="headers">Headers sent on every request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when connected.</returns>
        public async Task<bool> ConnectAsync(string baseUrl, Dictionary<string, string> headers, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(headers);
            foreach (KeyValuePair<string, string> h in headers) Client.SetRequestHeader(h.Key, h.Value);
            return await Client.ConnectStreamableAsync(baseUrl, "/mcp", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Call a method and return its result as JSON.
        /// </summary>
        /// <param name="method">Method.</param>
        /// <param name="parameters">Parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result element.</returns>
        /// <exception cref="InvalidOperationException">The server returned a JSON-RPC error.</exception>
        public async Task<JsonElement> CallAsync(string method, object? parameters, CancellationToken token)
        {
            JsonRpcResponse r = await Client.CallAsync(method, parameters, 60000, token).ConfigureAwait(false);
            if (r.Error != null) throw new InvalidOperationException("JSON-RPC error " + r.Error.Code + ": " + r.Error.Message);
            return Parse(r.Result);
        }

        /// <summary>
        /// Call a method and return its JSON-RPC error code, or 0 on success.
        /// </summary>
        /// <param name="method">Method.</param>
        /// <param name="parameters">Parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Error code or 0.</returns>
        public async Task<int> ErrorCodeAsync(string method, object? parameters, CancellationToken token)
        {
            JsonRpcResponse r = await Client.CallAsync(method, parameters, 60000, token).ConfigureAwait(false);
            return r.Error?.Code ?? 0;
        }

        /// <summary>
        /// Call a tool.
        /// </summary>
        /// <param name="name">Tool name.</param>
        /// <param name="arguments">Arguments.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Outcome.</returns>
        public async Task<McpToolOutcome> ToolAsync(string name, object arguments, CancellationToken token)
        {
            JsonElement result = await CallAsync("tools/call", new Dictionary<string, object> { { "name", name }, { "arguments", arguments } }, token).ConfigureAwait(false);
            return ToOutcome(result);
        }

        /// <summary>
        /// Convert a tools/call result to an outcome.
        /// </summary>
        /// <param name="result">Result element.</param>
        /// <returns>Outcome.</returns>
        public static McpToolOutcome ToOutcome(JsonElement result)
        {
            McpToolOutcome o = new McpToolOutcome();
            if (result.TryGetProperty("isError", out JsonElement err) && err.ValueKind == JsonValueKind.True) o.IsError = true;
            if (result.TryGetProperty("content", out JsonElement content) && content.ValueKind == JsonValueKind.Array && content.GetArrayLength() > 0 && content[0].TryGetProperty("text", out JsonElement text))
                o.Text = text.GetString() ?? String.Empty;
            if (result.TryGetProperty("structuredContent", out JsonElement s)) o.Structured = s.Clone();
            return o;
        }

        /// <summary>
        /// Parse a JSON-RPC result object into an element.
        /// </summary>
        /// <param name="result">Result.</param>
        /// <returns>Element.</returns>
        public static JsonElement Parse(object? result)
        {
            if (result is JsonElement e) return e.Clone();
            using (JsonDocument doc = JsonDocument.Parse(JsonSerializer.Serialize(result)))
            {
                return doc.RootElement.Clone();
            }
        }

        /// <summary>
        /// Disconnect.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Disconnect.
        /// </summary>
        /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing) Client.Dispose();
            _Disposed = true;
        }

        #endregion
    }
}
