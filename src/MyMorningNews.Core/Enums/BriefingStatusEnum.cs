namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Briefing status.
    /// </summary>
    public enum BriefingStatusEnum
    {
        /// <summary>
        /// Waiting to compose.
        /// </summary>
        Queued,

        /// <summary>
        /// Composing.
        /// </summary>
        Composing,

        /// <summary>
        /// Ready.
        /// </summary>
        Ready,

        /// <summary>
        /// Failed.
        /// </summary>
        Failed
    }
}
