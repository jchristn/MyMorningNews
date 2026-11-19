namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Model usage and cost report.
    /// </summary>
    public class UsageReport
    {
        /// <summary>
        /// Range start.
        /// </summary>
        public DateTime FromUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Range end.
        /// </summary>
        public DateTime ToUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Grouping.
        /// </summary>
        public UsageGroupByEnum GroupBy { get; set; } = UsageGroupByEnum.Model;

        /// <summary>
        /// Totals across the whole range and filter.
        /// </summary>
        public UsageRow Totals { get; set; } = new UsageRow { Key = "total" };

        /// <summary>
        /// Rows ordered by cost descending.
        /// </summary>
        public List<UsageRow> Rows { get; set; } = new List<UsageRow>();
    }
}
