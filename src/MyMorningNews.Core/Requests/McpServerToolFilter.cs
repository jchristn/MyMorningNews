namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for McpServerTool records. Null properties are not applied.
    /// </summary>
    public class McpServerToolFilter : EnumerationQuery
    {
        /// <summary>
        /// Match McpServerId exactly when set.
        /// </summary>
        public string? McpServerId { get; set; } = null;

        /// <summary>
        /// Match Enabled exactly when set.
        /// </summary>
        public bool? Enabled { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
