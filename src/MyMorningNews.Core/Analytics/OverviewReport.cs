namespace MyMorningNews.Core.Analytics
{
    using System;

    /// <summary>
    /// Headline numbers for a window: activity counts plus the key figures from the relevance, accuracy, feedback,
    /// suggestion, quality, and usage reports. Built for dashboards, the mobile home screen, and the MCP summary tool.
    /// </summary>
    public class OverviewReport
    {
        /// <summary>
        /// Window start (UTC).
        /// </summary>
        public DateTime FromUtc { get; set; } = DateTime.UtcNow.AddDays(-90);

        /// <summary>
        /// Window end (UTC).
        /// </summary>
        public DateTime ToUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Active topics in scope (not windowed).
        /// </summary>
        public long ActiveTopics { get; set; } = 0;

        /// <summary>
        /// Runs created in the window, excluding dry runs.
        /// </summary>
        public long Runs { get; set; } = 0;

        /// <summary>
        /// Runs that completed in the window.
        /// </summary>
        public long RunsCompleted { get; set; } = 0;

        /// <summary>
        /// Runs that partially succeeded in the window.
        /// </summary>
        public long RunsPartiallySucceeded { get; set; } = 0;

        /// <summary>
        /// Runs that failed in the window.
        /// </summary>
        public long RunsFailed { get; set; } = 0;

        /// <summary>
        /// Findings shown in the window (not duplicates, not below threshold, not dry runs).
        /// </summary>
        public int FindingsShown { get; set; } = 0;

        /// <summary>
        /// Findings rated in the window.
        /// </summary>
        public int FindingsRated { get; set; } = 0;

        /// <summary>
        /// Share of rated findings judged relevant, from 0 to 1. Null when nothing was rated.
        /// </summary>
        public double? RelevancePrecision { get; set; } = null;

        /// <summary>
        /// Briefings that reached Ready in the window, excluding dry runs and previews.
        /// </summary>
        public long BriefingsReady { get; set; } = 0;

        /// <summary>
        /// Emails sent in the window.
        /// </summary>
        public long EmailsSent { get; set; } = 0;

        /// <summary>
        /// Emails that failed in the window.
        /// </summary>
        public long EmailsFailed { get; set; } = 0;

        /// <summary>
        /// Open predictions (not windowed by resolution).
        /// </summary>
        public int PredictionsOpen { get; set; } = 0;

        /// <summary>
        /// Predictions resolved in the window.
        /// </summary>
        public int PredictionsResolved { get; set; } = 0;

        /// <summary>
        /// Share of resolved predictions judged accurate, from 0 to 1. Null when none resolved.
        /// </summary>
        public double? PredictionAccuracy { get; set; } = null;

        /// <summary>
        /// Mean Brier score of resolved predictions (lower is better). Null when none resolved.
        /// </summary>
        public double? MeanBrier { get; set; } = null;

        /// <summary>
        /// Feedback submitted in the window.
        /// </summary>
        public int FeedbackTotal { get; set; } = 0;

        /// <summary>
        /// Feedback in the window that carried a written reason.
        /// </summary>
        public int FeedbackWithReason { get; set; } = 0;

        /// <summary>
        /// Suggestions still New.
        /// </summary>
        public int SuggestionsNew { get; set; } = 0;

        /// <summary>
        /// Suggestions accepted or done.
        /// </summary>
        public int SuggestionsAccepted { get; set; } = 0;

        /// <summary>
        /// Share of quotes that failed verification, from 0 to 1. Null when no quotes were checked.
        /// </summary>
        public double? QuoteFailureRate { get; set; } = null;

        /// <summary>
        /// Model calls in the window.
        /// </summary>
        public long ModelCalls { get; set; } = 0;

        /// <summary>
        /// Model cost in the window (USD).
        /// </summary>
        public decimal ModelCostUsd { get; set; } = 0;
    }
}
