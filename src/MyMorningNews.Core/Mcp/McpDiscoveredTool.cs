namespace MyMorningNews.Core.Mcp
{
    using System;

    /// <summary>
    /// A tool listed by a remote MCP server.
    /// </summary>
    public class McpDiscoveredTool
    {
        /// <summary>
        /// Tool name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Description, or null.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// JSON Schema of the arguments as raw JSON, or null.
        /// </summary>
        public string? InputSchema { get; set; } = null;
    }
}
