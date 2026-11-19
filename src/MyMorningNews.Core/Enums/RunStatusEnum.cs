namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Run status.
    /// </summary>
    public enum RunStatusEnum
    {
        /// <summary>
        /// Waiting for a worker.
        /// </summary>
        Queued,

        /// <summary>
        /// Running.
        /// </summary>
        Running,

        /// <summary>
        /// Finished.
        /// </summary>
        Completed,

        /// <summary>
        /// Finished with non-critical stage failures.
        /// </summary>
        PartiallySucceeded,

        /// <summary>
        /// Failed.
        /// </summary>
        Failed,

        /// <summary>
        /// Cancelled.
        /// </summary>
        Cancelled,

        /// <summary>
        /// Skipped.
        /// </summary>
        Skipped
    }
}
