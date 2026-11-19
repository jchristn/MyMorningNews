namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Email status.
    /// </summary>
    public enum EmailStatusEnum
    {
        /// <summary>
        /// Waiting.
        /// </summary>
        Queued,

        /// <summary>
        /// Sending.
        /// </summary>
        Sending,

        /// <summary>
        /// Sent.
        /// </summary>
        Sent,

        /// <summary>
        /// Failed for good.
        /// </summary>
        Failed,

        /// <summary>
        /// Not sent: the recipient unsubscribed.
        /// </summary>
        Suppressed,

        /// <summary>
        /// Not sent: email is disabled.
        /// </summary>
        Skipped
    }
}
