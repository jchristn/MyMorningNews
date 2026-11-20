namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// MCP transport.
    /// </summary>
    public enum McpTransportEnum
    {
        /// <summary>
        /// Streamable HTTP.
        /// </summary>
        StreamableHttp,

        /// <summary>
        /// HTTP with server-sent events (older servers).
        /// </summary>
        Sse
    }
}
