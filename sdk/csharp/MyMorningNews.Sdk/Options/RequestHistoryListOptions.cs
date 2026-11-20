namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;

    /// <summary>
    /// Filters for listing, summarizing, or deleting captured requests.
    /// </summary>
    public class RequestHistoryListOptions : ListOptions
    {
        /// <summary>
        /// Global administrators: only this tenant. Null by default.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Global administrators: only this user. Null by default.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Only this HTTP method. Null by default.
        /// </summary>
        public string? Method { get; set; } = null;

        /// <summary>
        /// Only this status code. Null by default.
        /// </summary>
        public int? StatusCode { get; set; } = null;

        /// <summary>
        /// Minimum status code. Null by default.
        /// </summary>
        public int? MinStatusCode { get; set; } = null;

        /// <summary>
        /// Maximum status code. Null by default.
        /// </summary>
        public int? MaxStatusCode { get; set; } = null;

        /// <summary>
        /// Status class such as 2xx, 4xx, or 5xx. Null by default.
        /// </summary>
        public string? StatusClass { get; set; } = null;

        /// <summary>
        /// Only paths containing this text. Null by default.
        /// </summary>
        public string? PathContains { get; set; } = null;

        /// <summary>
        /// Bucket size in minutes for the summary. Null by default.
        /// </summary>
        public int? BucketMinutes { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RequestHistoryListOptions()
        {
        }

        /// <summary>
        /// Add this resource's filters.
        /// </summary>
        /// <param name="query">Query to add to.</param>
        protected override void AddFilters(Dictionary<string, string> query)
        {
            base.AddFilters(query);
            QueryFormat.Add(query, "tenantId", TenantId);
            QueryFormat.Add(query, "userId", UserId);
            QueryFormat.Add(query, "method", Method);
            QueryFormat.Add(query, "statusCode", StatusCode);
            QueryFormat.Add(query, "minStatusCode", MinStatusCode);
            QueryFormat.Add(query, "maxStatusCode", MaxStatusCode);
            QueryFormat.Add(query, "statusClass", StatusClass);
            QueryFormat.Add(query, "pathContains", PathContains);
            QueryFormat.Add(query, "bucketMinutes", BucketMinutes);
        }
    }
}
