namespace MyMorningNews.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Feedback volume and coverage (plan section 20).
    /// </summary>
    public class FeedbackReport
    {
        /// <summary>
        /// Current ratings.
        /// </summary>
        public int Total { get; set; } = 0;

        /// <summary>
        /// Ratings with a reason.
        /// </summary>
        public int WithReason { get; set; } = 0;

        /// <summary>
        /// By channel.
        /// </summary>
        public List<BreakdownRow> ByChannel { get; set; } = new List<BreakdownRow>();

        /// <summary>
        /// By item type.
        /// </summary>
        public List<BreakdownRow> ByTargetType { get; set; } = new List<BreakdownRow>();

        /// <summary>
        /// By day.
        /// </summary>
        public List<BreakdownRow> ByDay { get; set; } = new List<BreakdownRow>();

        /// <summary>
        /// By day and channel: Key is the day (yyyy-MM-dd), Label is the channel, Count is feedback, Positive is with a reason.
        /// </summary>
        public List<BreakdownRow> ByChannelDay { get; set; } = new List<BreakdownRow>();

        /// <summary>
        /// Email feedback page views.
        /// </summary>
        public int EmailLinkOpens { get; set; } = 0;

        /// <summary>
        /// Email feedback submissions.
        /// </summary>
        public int EmailLinkSubmits { get; set; } = 0;
    }
}
