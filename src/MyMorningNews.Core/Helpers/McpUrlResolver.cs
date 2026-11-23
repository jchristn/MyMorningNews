namespace MyMorningNews.Core.Helpers
{
    using System;
    using System.Net;
    using MyMorningNews.Core.Settings;

    /// <summary>
    /// Works out the MCP endpoint address to publish to clients (the public client configuration). Stateless and
    /// thread safe.
    /// </summary>
    public static class McpUrlResolver
    {
        /// <summary>
        /// Path of the MCP server's Streamable HTTP endpoint.
        /// </summary>
        public const string McpPath = "/mcp";

        /// <summary>
        /// The MCP endpoint address clients should use, or null when none is known. In order:
        /// <list type="number">
        /// <item><see cref="RestSettings.McpPublicUrl"/> when it is not a loopback address (an explicit public address).</item>
        /// <item><see cref="RestSettings.PublicBaseUrl"/> plus <c>/mcp</c> when <see cref="RestSettings.McpOnPublicBaseUrl"/> is true.</item>
        /// <item><see cref="RestSettings.McpPublicUrl"/> when both it and <see cref="RestSettings.PublicBaseUrl"/> are loopback (a local install).</item>
        /// <item>Null: the server is public and its MCP address is loopback, which no remote client could reach.</item>
        /// </list>
        /// </summary>
        /// <param name="rest">REST settings. Not null.</param>
        /// <returns>Absolute http or https URL, or null.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="rest"/> is null.</exception>
        public static string? Resolve(RestSettings rest)
        {
            ArgumentNullException.ThrowIfNull(rest);
            string configured = rest.McpPublicUrl;
            bool configuredValid = IsHttpUrl(configured);
            bool configuredLoopback = configuredValid && IsLoopback(configured);
            if (configuredValid && !configuredLoopback) return configured;
            if (rest.McpOnPublicBaseUrl && IsHttpUrl(rest.PublicBaseUrl)) return rest.PublicBaseUrl.TrimEnd('/') + McpPath;
            if (configuredLoopback && IsLoopback(rest.PublicBaseUrl)) return configured;
            return null;
        }

        /// <summary>
        /// Whether a URL's host is a loopback address (127.0.0.0/8, ::1) or the name localhost.
        /// </summary>
        /// <param name="url">Absolute URL. Null, empty, or malformed answers false.</param>
        /// <returns>True for a loopback host.</returns>
        public static bool IsLoopback(string? url)
        {
            if (String.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri? uri)) return false;
            if (uri.IsLoopback) return true;
            string host = uri.Host.Trim('[', ']');
            if (host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)) return true;
            return IPAddress.TryParse(host, out IPAddress? address) && IPAddress.IsLoopback(address);
        }

        private static bool IsHttpUrl(string? url)
        {
            if (String.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri? uri)) return false;
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }
    }
}
