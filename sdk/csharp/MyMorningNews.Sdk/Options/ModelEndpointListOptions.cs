namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;

    /// <summary>
    /// Filters for listing model endpoints.
    /// </summary>
    public class ModelEndpointListOptions : ListOptions
    {
        /// <summary>
        /// Only active (true) or inactive (false) endpoints. Null by default.
        /// </summary>
        public bool? Active { get; set; } = null;

        /// <summary>
        /// Only endpoints that do (true) or do not (false) serve chat. Null by default.
        /// </summary>
        public bool? SupportsChat { get; set; } = null;

        /// <summary>
        /// Only endpoints that do (true) or do not (false) serve embeddings. Null by default.
        /// </summary>
        public bool? SupportsEmbeddings { get; set; } = null;

        /// <summary>
        /// Only endpoints of this provider. Null by default.
        /// </summary>
        public ModelProviderEnum? Provider { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ModelEndpointListOptions()
        {
        }

        /// <summary>
        /// Add this resource's filters.
        /// </summary>
        /// <param name="query">Query to add to.</param>
        protected override void AddFilters(Dictionary<string, string> query)
        {
            base.AddFilters(query);
            QueryFormat.Add(query, "active", Active);
            QueryFormat.Add(query, "supportsChat", SupportsChat);
            QueryFormat.Add(query, "supportsEmbeddings", SupportsEmbeddings);
            QueryFormat.Add(query, "provider", Provider);
        }
    }
}
