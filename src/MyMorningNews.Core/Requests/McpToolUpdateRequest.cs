namespace MyMorningNews.Core.Requests
{
    /// <summary>
    /// Turn an MCP server tool on or off for agentic research.
    /// </summary>
    public class McpToolUpdateRequest
    {
        /// <summary>
        /// Offer the tool to agentic research.
        /// </summary>
        public bool Enabled { get; set; } = false;
    }
}
