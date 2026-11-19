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
        All
    }
}
