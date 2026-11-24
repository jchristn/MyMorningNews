namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for LearnedPreferenceFeedback records. Null properties are not applied.
    /// </summary>
    public class LearnedPreferenceFeedbackFilter : EnumerationQuery
    {
        /// <summary>
        /// Match LearnedPreferenceId exactly when set.
        /// </summary>
        public string? LearnedPreferenceId { get; set; } = null;

        /// <summary>
        /// Match FeedbackId exactly when set.
        /// </summary>
        public string? FeedbackId { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
