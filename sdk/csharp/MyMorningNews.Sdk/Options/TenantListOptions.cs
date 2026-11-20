namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;

    /// <summary>
    /// Filters for listing tenants.
    /// </summary>
    public class TenantListOptions : ListOptions
    {
        /// <summary>
        /// Only active (true) or inactive (false) tenants. Null by default.
        /// </summary>
        public bool? Active { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TenantListOptions()
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
        }
    }
}
