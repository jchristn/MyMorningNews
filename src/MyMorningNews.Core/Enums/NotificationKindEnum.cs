namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// What a notification is about.
    /// </summary>
    public enum NotificationKindEnum
    {
        /// <summary>
        /// New findings after a research run.
        /// </summary>
        RunResults,

        /// <summary>
        /// A briefing is ready to read.
        /// </summary>
        BriefingReady,

        /// <summary>
        /// The audio version of a briefing is ready.
        /// </summary>
        AudioReady,

        /// <summary>
        /// A research run failed.
        /// </summary>
        RunFailed,

        /// <summary>
        /// A plan limit stopped a scheduled run.
        /// </summary>
        CapReached,

        /// <summary>
        /// A prediction reached its date and is due for review.
        /// </summary>
        PredictionDue,

        /// <summary>
        /// A recurring topic had no new findings for a week; keep it or it is deleted after a grace period.
        /// </summary>
        TopicStale,

        /// <summary>
        /// A plan change left the account with more recurring custom topics than its limit; the topics past the limit
        /// do not run until the owner chooses which to keep, archives some, or upgrades.
        /// </summary>
        TopicsOverLimit
    }
}
