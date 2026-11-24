namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for McpServer records. Null properties are not applied.
    /// </summary>
    public class McpServerFilter : EnumerationQuery
    {
        /// <summary>
        /// Match Transport exactly when set.
        /// </summary>
        public McpTransportEnum? Transport { get; set; } = null;

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
