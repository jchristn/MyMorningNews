namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Filter for model usage and cost analytics.
    /// </summary>
    public class UsageReportFilter
    {
        /// <summary>
        /// Tenant scope; null with AllTenants for platform-wide reports.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Report across every tenant (global administrators only).
        /// </summary>
        public bool AllTenants { get; set; } = false;

        /// <summary>
        /// Only calls attributed to this user.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Only calls for this topic.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Only this endpoint.
        /// </summary>
        public string? ModelEndpointId { get; set; } = null;

        /// <summary>
        /// Only this task role.
        /// </summary>
        public ModelTaskEnum? Task { get; set; } = null;

        /// <summary>
        /// Include prompt lab and dry-run calls (default excluded).
        /// </summary>
        public bool? IncludeDryRun { get; set; } = null;

        /// <summary>
        /// Range start (UTC, inclusive).
        /// </summary>
        public DateTime FromUtc { get; set; } = DateTime.UtcNow.AddDays(-30);

        /// <summary>
        /// Range end (UTC, exclusive).
        /// </summary>
        public DateTime ToUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Grouping.
        /// </summary>
        public UsageGroupByEnum GroupBy { get; set; } = UsageGroupByEnum.Model;

        /// <summary>
        /// Grouping for the time series, or null (the default) for an ungrouped series. Day is not accepted, because the series is already bucketed by time.
        /// </summary>
        public UsageGroupByEnum? SeriesGroupBy { get; set; } = null;

        /// <summary>
        /// Maximum rows. Default 50.
        /// </summary>
        public int Top { get; set; } = 50;

        /// <summary>
        /// Time series bucket width in minutes. Default 1440.
        /// </summary>
        public int BucketMinutes { get; set; } = 1440;
    }
}
