namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Topic lifecycle.
    /// </summary>
    public enum TopicStatusEnum
    {
        /// <summary>
        /// Being refined.
        /// </summary>
        Draft,

        /// <summary>
        /// Researched on schedule.
        /// </summary>
        Active,

        /// <summary>
        /// Schedules paused.
        /// </summary>
        Paused,

        /// <summary>
        /// Kept for history only.
        /// </summary>
        Archived
    }
}
