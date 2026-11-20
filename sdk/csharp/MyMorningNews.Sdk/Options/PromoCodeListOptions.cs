namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;

    /// <summary>
    /// Filters for listing promotional codes.
    /// </summary>
    public class PromoCodeListOptions : ListOptions
    {
        /// <summary>
        /// active, disabled, notstarted, expired, or exhausted. Null by default.
        /// </summary>
        public string? Status { get; set; } = null;

        /// <summary>
        /// Only enabled (true) or disabled (false) codes. Null by default.
        /// </summary>
        public bool? Enabled { get; set; } = null;

        /// <summary>
        /// Only codes for this plan. Null by default.
        /// </summary>
        public string? PlanId { get; set; } = null;

        /// <summary>
        /// Only codes generated in this batch. Null by default.
        /// </summary>
        public string? BatchId { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public PromoCodeListOptions()
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
            QueryFormat.Add(query, "enabled", Enabled);
            QueryFormat.Add(query, "planId", PlanId);
            QueryFormat.Add(query, "batchId", BatchId);
        }
    }
}
