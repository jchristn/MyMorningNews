namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;

    /// <summary>
    /// Filters for listing one-time research requests.
    /// </summary>
    public class ResearchRequestListOptions : OwnedListOptions
    {
        /// <summary>
        /// Only requests in this status. Null by default.
        /// </summary>
        public ResearchRequestStatusEnum? Status { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ResearchRequestListOptions()
        {
        }

        /// <summary>
        /// Add this resource's filters.
        /// </summary>
        /// <param name="query">Query to add to.</param>
        protected override void AddFilters(Dictionary<string, string> query)
        {
            base.AddFilters(query);
            QueryFormat.Add(query, "status", Status);
        }
    }
}
