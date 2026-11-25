namespace MyMorningNews.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Platform-wide usage for one period (normally one UTC day), for the administrator summary email. Counts with
    /// "in period" in their description cover [FromUtc, ToUtc); the rest are totals at ToUtc.
    /// </summary>
    public class PlatformDailyStats
    {
        /// <summary>
        /// Period start (inclusive), UTC.
        /// </summary>
        public DateTime FromUtc { get; set; } = DateTime.UtcNow.Date.AddDays(-1);

        /// <summary>
        /// Period end (exclusive), UTC.
        /// </summary>
        public DateTime ToUtc { get; set; } = DateTime.UtcNow.Date;

        /// <summary>
        /// Active accounts that are not scheduled for deletion.
        /// </summary>
        public long TotalUsers { get; set; } = 0;

        /// <summary>
        /// Accounts created in period.
        /// </summary>
        public long NewUsers { get; set; } = 0;

        /// <summary>
        /// Self sign-ups in period.
        /// </summary>
        public long NewSignups { get; set; } = 0;

        /// <summary>
        /// Emails verified in period.
        /// </summary>
        public long EmailsVerified { get; set; } = 0;

        /// <summary>
        /// Account deletions requested in period.
        /// </summary>
        public long DeletionRequests { get; set; } = 0;

        /// <summary>
        /// Distinct users who used the product in period.
        /// </summary>
        public long DailyActiveUsers { get; set; } = 0;

        /// <summary>
        /// Distinct users active in the 7 days ending at ToUtc.
        /// </summary>
        public long WeeklyActiveUsers { get; set; } = 0;

        /// <summary>
        /// Distinct users active in the 30 days ending at ToUtc.
        /// </summary>
        public long MonthlyActiveUsers { get; set; } = 0;

        /// <summary>
        /// Users who signed in in period (password or token sign-in).
        /// </summary>
        public long SignIns { get; set; } = 0;

        /// <summary>
        /// Active recurring topics.
        /// </summary>
        public long ActiveTopics { get; set; } = 0;

        /// <summary>
        /// Topics created in period.
        /// </summary>
        public long NewTopics { get; set; } = 0;

        /// <summary>
        /// Real runs that completed (fully or partly) in period.
        /// </summary>
        public long RunsCompleted { get; set; } = 0;

        /// <summary>
        /// Real runs that failed in period.
        /// </summary>
        public long RunsFailed { get; set; } = 0;

        /// <summary>
        /// Real runs skipped in period (plan limits, schedule rules).
        /// </summary>
        public long RunsSkipped { get; set; } = 0;

        /// <summary>
        /// New findings from runs completed in period.
        /// </summary>
        public long NewFindings { get; set; } = 0;

        /// <summary>
        /// Average duration of completed runs in period, seconds.
        /// </summary>
        public double AverageRunSeconds { get; set; } = 0;

        /// <summary>
        /// Briefings composed in period.
        /// </summary>
        public long BriefingsReady { get; set; } = 0;

        /// <summary>
        /// Model calls in period.
        /// </summary>
        public long ModelCalls { get; set; } = 0;

        /// <summary>
        /// Failed model calls in period.
        /// </summary>
        public long ModelFailures { get; set; } = 0;

        /// <summary>
        /// Input tokens in period.
        /// </summary>
        public long InputTokens { get; set; } = 0;

        /// <summary>
        /// Output tokens in period.
        /// </summary>
        public long OutputTokens { get; set; } = 0;

        /// <summary>
        /// Estimated model cost in period, US dollars.
        /// </summary>
        public decimal ModelCostUsd { get; set; } = 0m;

        /// <summary>
        /// Total model call time in period, minutes.
        /// </summary>
        public double ModelMinutes { get; set; } = 0;

        /// <summary>
        /// Model cost in the 30 days ending at ToUtc, US dollars.
        /// </summary>
        public decimal ModelCostUsd30Days { get; set; } = 0m;

        /// <summary>
        /// Audio files finished in period.
        /// </summary>
        public long AudioFiles { get; set; } = 0;

        /// <summary>
        /// Audio generated in period, minutes.
        /// </summary>
        public double AudioMinutes { get; set; } = 0;

        /// <summary>
        /// Emails sent in period.
        /// </summary>
        public long EmailsSent { get; set; } = 0;

        /// <summary>
        /// Emails that failed in period.
        /// </summary>
        public long EmailsFailed { get; set; } = 0;

        /// <summary>
        /// Active push devices.
        /// </summary>
        public long PushDevices { get; set; } = 0;

        /// <summary>
        /// Push devices registered in period.
        /// </summary>
        public long NewPushDevices { get; set; } = 0;

        /// <summary>
        /// Active paid subscriptions.
        /// </summary>
        public long PaidSubscriptions { get; set; } = 0;

        /// <summary>
        /// Paid subscriptions started in period.
        /// </summary>
        public long NewPaidSubscriptions { get; set; } = 0;

        /// <summary>
        /// Active system briefing (catalog) subscriptions.
        /// </summary>
        public long CatalogSubscriptions { get; set; } = 0;

        /// <summary>
        /// Keep-or-delete prompts sent in period.
        /// </summary>
        public long StalePrompts { get; set; } = 0;

        /// <summary>
        /// Stale topics kept in period.
        /// </summary>
        public long StaleKept { get; set; } = 0;

        /// <summary>
        /// Stale topics deleted in period, by the owner or automatically.
        /// </summary>
        public long StaleDeleted { get; set; } = 0;

        /// <summary>
        /// Top models by cost in period. Never null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null.</exception>
        public List<PlatformModelUsage> TopModels
        {
            get { return _TopModels; }
            set { _TopModels = value ?? throw new ArgumentNullException(nameof(TopModels)); }
        }

        private List<PlatformModelUsage> _TopModels = new List<PlatformModelUsage>();
    }
}
