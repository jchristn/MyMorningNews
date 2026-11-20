namespace MyMorningNews.Core.Analytics
{
    using System.Collections.Generic;

    /// <summary>
    /// Email delivery for a window: totals, delivery time, a breakdown by template and day, feedback link use, and
    /// unsubscribes.
    /// </summary>
    public class EmailReport
    {
        /// <summary>
        /// Messages queued in the window.
        /// </summary>
        public long Total { get; set; } = 0;

        /// <summary>
        /// Messages sent.
        /// </summary>
        public long Sent { get; set; } = 0;

        /// <summary>
        /// Messages that failed for good.
        /// </summary>
        public long Failed { get; set; } = 0;

        /// <summary>
        /// Messages still queued or sending.
        /// </summary>
        public long Pending { get; set; } = 0;

        /// <summary>
        /// Messages skipped (suppressed, disabled, or preferences).
        /// </summary>
        public long Skipped { get; set; } = 0;

        /// <summary>
        /// Share of finished messages that were sent, from 0 to 1. Null when none finished.
        /// </summary>
        public double? DeliveryRate { get; set; } = null;

        /// <summary>
        /// Mean seconds from queueing to sending. Null when none were sent.
        /// </summary>
        public double? MeanSecondsToSend { get; set; } = null;

        /// <summary>
        /// By template: Count is messages, Positive is sent, Rate is the delivery rate.
        /// </summary>
        public List<BreakdownRow> ByTemplate { get; set; } = new List<BreakdownRow>();

        /// <summary>
        /// By day (yyyy-MM-dd): Count is messages, Positive is sent, Rate is the delivery rate.
        /// </summary>
        public List<BreakdownRow> ByDay { get; set; } = new List<BreakdownRow>();

        /// <summary>
        /// By status: Count is messages.
        /// </summary>
        public List<BreakdownRow> ByStatus { get; set; } = new List<BreakdownRow>();

        /// <summary>
        /// Feedback link opens from emails in the window.
        /// </summary>
        public int FeedbackLinkOpens { get; set; } = 0;

        /// <summary>
        /// Feedback submitted through email links in the window.
        /// </summary>
        public int FeedbackLinkSubmits { get; set; } = 0;

        /// <summary>
        /// Unsubscribes recorded in the window.
        /// </summary>
        public long Unsubscribes { get; set; } = 0;
    }
}
