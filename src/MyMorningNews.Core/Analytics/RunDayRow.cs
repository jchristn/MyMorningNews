namespace MyMorningNews.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Runs created on one day, by status.
    /// </summary>
    public class RunDayRow
    {
        /// <summary>
        /// Day (yyyy-MM-dd) in the report's time zone.
        /// </summary>
        public string Day { get; set; } = String.Empty;

        /// <summary>
        /// The UTC instant the day starts.
        /// </summary>
        public DateTime StartUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The UTC instant the day ends (the next day's start).
        /// </summary>
        public DateTime EndUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Runs created on the day.
        /// </summary>
        public int Total { get; set; } = 0;

        /// <summary>
        /// Estimated cost of the day's runs in US dollars. Null when none was priced.
        /// </summary>
        public decimal? CostUsd { get; set; } = null;

        /// <summary>
        /// Runs by status. Every run status is present.
        /// </summary>
        public Dictionary<string, int> Counts { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);
    }
}
