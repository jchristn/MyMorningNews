namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;

    /// <summary>
    /// Filters for listing registered MCP servers.
    /// </summary>
    public class McpServerListOptions : ListOptions
    {
        /// <summary>
        /// Only enabled (true) or disabled (false) servers. Null by default (both).
        /// </summary>
        public bool? Enabled { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public McpServerListOptions()
        {
        }

        /// <summary>
        /// Add this resource's filters.
        /// </summary>
        /// <param name="query">Query to add to.</param>
        protected override void AddFilters(Dictionary<string, string> query)
        {
            base.AddFilters(query);
            QueryFormat.Add(query, "enabled", Enabled);
        }
    }
}
