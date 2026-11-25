namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Email category for preferences and unsubscribing.
    /// </summary>
    public enum EmailCategoryEnum
    {
        /// <summary>
        /// Run digests.
        /// </summary>
        RunEmails,

        /// <summary>
        /// Edition and one-time briefings.
        /// </summary>
        BriefingEmails,

        /// <summary>
        /// Run failure notices.
        /// </summary>
        FailureEmails,

        /// <summary>
        /// Every category.
        /// </summary>
        All,

        /// <summary>
        /// Audio ready notices.
        /// </summary>
        AudioEmails,

        /// <summary>
        /// Prediction due notices.
        /// </summary>
        PredictionEmails,

        /// <summary>
        /// Keep-or-delete prompts and notices for topics without new findings.
        /// </summary>
        TopicReviewEmails
    }
}
