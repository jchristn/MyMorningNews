namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Audio status.
    /// </summary>
    public enum MediaStatusEnum
    {
        /// <summary>
        /// Waiting.
        /// </summary>
        Queued,

        /// <summary>
        /// Writing the script.
        /// </summary>
        Scripting,

        /// <summary>
        /// Synthesizing speech.
        /// </summary>
        Synthesizing,

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
