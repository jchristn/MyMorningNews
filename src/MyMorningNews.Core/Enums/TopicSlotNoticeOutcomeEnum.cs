namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// What one over-limit check of an account did.
    /// </summary>
    public enum TopicSlotNoticeOutcomeEnum
    {
        /// <summary>
        /// The account is within its custom topic limit and had no recorded notice.
        /// </summary>
        WithinLimit,

        /// <summary>
        /// The account went over its limit and the owner was notified (on the channels the owner wants).
        /// </summary>
        Notified,

        /// <summary>
        /// The account is still over its limit and was already notified for this over-limit period.
        /// </summary>
        AlreadyNotified,

        /// <summary>
        /// The account is back within its limit; the recorded notice was removed, so going over again notifies again.
        /// </summary>
        Cleared
    }
}
