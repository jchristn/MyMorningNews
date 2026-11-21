namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// State of a setup check.
    /// </summary>
    public enum SetupCheckStateEnum
    {
        /// <summary>
        /// Configured and proven to work.
        /// </summary>
        Ready,

        /// <summary>
        /// Something it needs is not configured, such as an API key.
        /// </summary>
        Missing,

        /// <summary>
        /// Configured but not yet proven to work.
        /// </summary>
        Unverified,

        /// <summary>
        /// The last check failed.
        /// </summary>
        Failed
    }
}
