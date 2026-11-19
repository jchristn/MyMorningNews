namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// One-time request status.
    /// </summary>
    public enum ResearchRequestStatusEnum
    {
        /// <summary>
        /// Waiting for answers or confirmation.
        /// </summary>
        Clarifying,

        /// <summary>
        /// Confirmed; waiting for its run time.
        /// </summary>
        Scheduled,

        /// <summary>
        /// Researching.
        /// </summary>
        Running,

        /// <summary>
        /// Delivered.
        /// </summary>
        Completed,

        /// <summary>
        /// Failed.
        /// </summary>
        Failed,

        /// <summary>
        /// Cancelled.
        /// </summary>
        Cancelled,

        /// <summary>
        /// Not a research request.
        /// </summary>
        Declined
    }
}
