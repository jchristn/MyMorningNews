namespace MyMorningNews.Sdk.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Result of testing an MCP server connection. Nothing is saved. A failed connection is still HTTP 200, with Success false and Error set.
    /// </summary>
    public class McpServerTestResponse
    {
        /// <summary>
        /// Whether the server connected and listed its tools. Default false.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Number of tools the server lists. Default 0.
        /// </summary>
        public int ToolCount { get; set; } = 0;

        /// <summary>
        /// Tool names the server lists. Never null; empty on failure.
        /// </summary>
        public List<string> Tools { get; set; } = new List<string>();

        /// <summary>
        /// Why the connection failed, or null on success.
        /// </summary>
        public string? Error { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public McpServerTestResponse()
        {
        }
    }
}
