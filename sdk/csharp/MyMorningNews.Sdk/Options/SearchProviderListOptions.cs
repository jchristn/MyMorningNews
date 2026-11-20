namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;

    /// <summary>
    /// Filters for listing search providers.
    /// </summary>
    public class SearchProviderListOptions : ListOptions
    {
        /// <summary>
        /// Only providers of this kind. Null by default.
        /// </summary>
        public SearchProviderKindEnum? Kind { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SearchProviderListOptions()
        {
        }

        /// <summary>
        /// Add this resource's filters.
        /// </summary>
        /// <param name="query">Query to add to.</param>
        protected override void AddFilters(Dictionary<string, string> query)
        {
            base.AddFilters(query);
            QueryFormat.Add(query, "kind", Kind);
        }
    }
}
