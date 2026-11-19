namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Run stage status.
    /// </summary>
    public enum StageStatusEnum
    {
        /// <summary>
        /// Not started.
        /// </summary>
        Pending,

        /// <summary>
        /// Running.
        /// </summary>
        Running,

        /// <summary>
        /// Succeeded.
        /// </summary>
        Succeeded,

        /// <summary>
        /// Failed.
        /// </summary>
        Failed,

        /// <summary>
        /// Skipped.
        /// </summary>
        Skipped
    }
}
