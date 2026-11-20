namespace MyMorningNews.Sdk.Options
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;

    /// <summary>
    /// Filters for usage and cost reports. The server defaults to the last 30 days, grouped by model.
    /// </summary>
    public class UsageOptions
    {
        /// <summary>
        /// Start of the window. Null uses 30 days before ToUtc.
        /// </summary>
        public DateTime? FromUtc { get; set; } = null;

        /// <summary>
        /// End of the window. Null uses now.
        /// </summary>
        public DateTime? ToUtc { get; set; } = null;

        /// <summary>
        /// Grouping (User, Model, Task, Provider, Topic, PromptKey, Day, or Tenant with AllTenants). Null uses the route default.
        /// </summary>
        public UsageGroupByEnum? GroupBy { get; set; } = null;

        /// <summary>
        /// Only this user. Null by default.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Only this topic. Null by default.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Only this model endpoint. Null by default.
        /// </summary>
        public string? ModelEndpointId { get; set; } = null;

        /// <summary>
        /// Only this task. Null by default.
        /// </summary>
        public ModelTaskEnum? Task { get; set; } = null;

        /// <summary>
        /// Include dry-run calls. Null by default.
        /// </summary>
        public bool? IncludeDryRun { get; set; } = null;

        /// <summary>
        /// Maximum rows (server default 50, range 1 to 1000). Null by default.
        /// </summary>
        public int? Top { get; set; } = null;

        /// <summary>
        /// Time series bucket size in minutes (server default 1440, range 1 to 43200). Null by default.
        /// </summary>
        public int? BucketMinutes { get; set; } = null;

        /// <summary>
        /// Global administrators: report across every tenant. Null by default.
        /// </summary>
        public bool? AllTenants { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public UsageOptions()
        {
        }

        /// <summary>
        /// Build the query string values.
        /// </summary>
        /// <returns>Query values.</returns>
        public Dictionary<string, string> ToQuery()
        {
            Dictionary<string, string> query = new Dictionary<string, string>(StringComparer.Ordinal);
            QueryFormat.Add(query, "from", FromUtc);
            QueryFormat.Add(query, "to", ToUtc);
            QueryFormat.Add(query, "groupBy", GroupBy);
            QueryFormat.Add(query, "userId", UserId);
            QueryFormat.Add(query, "topicId", TopicId);
            QueryFormat.Add(query, "modelEndpointId", ModelEndpointId);
            QueryFormat.Add(query, "task", Task);
            QueryFormat.Add(query, "includeDryRun", IncludeDryRun);
            QueryFormat.Add(query, "top", Top);
            QueryFormat.Add(query, "bucketMinutes", BucketMinutes);
            QueryFormat.Add(query, "allTenants", AllTenants);
            return query;
        }
    }
}
