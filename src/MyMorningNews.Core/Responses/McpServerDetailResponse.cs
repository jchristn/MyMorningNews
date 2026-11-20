namespace MyMorningNews.Core.Responses
{
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// A registered MCP server with its discovered tools.
    /// </summary>
    public class McpServerDetailResponse
    {
        /// <summary>
        /// Server.
        /// </summary>
        public McpServer Server { get; set; } = new McpServer();

        /// <summary>
        /// Discovered tools, by name.
        /// </summary>
        public List<McpServerTool> Tools { get; set; } = new List<McpServerTool>();
    }
}
