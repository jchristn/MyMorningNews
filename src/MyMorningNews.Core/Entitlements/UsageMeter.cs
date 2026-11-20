namespace MyMorningNews.Core.Entitlements
{
    using System;

    /// <summary>
    /// The usage meter for the current user (GET /v1.0/api/account/usage): month-to-date cost against the cap, and
    /// counts against the plan's other limits. A null limit means unlimited.
    /// </summary>
    public class UsageMeter
    {
        /// <summary>
        /// Plan key.
        /// </summary>
        public string PlanKey { get; set; } = String.Empty;

        /// <summary>
        /// Plan name.
        /// </summary>
        public string PlanName { get; set; } = String.Empty;

        /// <summary>
        /// Model cost this calendar month (UTC), in USD.
        /// </summary>
        public decimal MonthToDateCostUsd { get; set; } = 0;

        /// <summary>
        /// Monthly cost cap in USD, or null for unlimited.
        /// </summary>
        public decimal? MonthlyCostCapUsd { get; set; } = null;

        /// <summary>
        /// Share of the cost cap used, from 0 to 1, or null without a cap.
        /// </summary>
        public double? CostUsedFraction { get; set; } = null;

        /// <summary>
        /// When the monthly counters reset (the first day of next month, UTC).
        /// </summary>
        public DateTime ResetUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Topics owned now.
        /// </summary>
        public long Topics { get; set; } = 0;

        /// <summary>
        /// Topic limit, or null for unlimited.
        /// </summary>
        public long? TopicsMax { get; set; } = null;

        /// <summary>
        /// Runs started today (UTC).
        /// </summary>
        public long RunsToday { get; set; } = 0;

        /// <summary>
        /// Daily run limit, or null for unlimited.
        /// </summary>
        public long? RunsDailyMax { get; set; } = null;

        /// <summary>
        /// Shortest schedule interval allowed, in minutes, or null for no limit.
        /// </summary>
        public long? ScheduleMinIntervalMinutes { get; set; } = null;

        /// <summary>
        /// Audio minutes generated this month.
        /// </summary>
        public double AudioMinutes { get; set; } = 0;

        /// <summary>
        /// Monthly audio minute limit, or null for unlimited.
        /// </summary>
        public long? AudioMinutesMax { get; set; } = null;

        /// <summary>
        /// Whether audio is included.
        /// </summary>
        public bool AudioEnabled { get; set; } = true;

        /// <summary>
        /// Whether the user may add their own model endpoints.
        /// </summary>
        public bool CustomEndpoints { get; set; } = true;
    }
}
