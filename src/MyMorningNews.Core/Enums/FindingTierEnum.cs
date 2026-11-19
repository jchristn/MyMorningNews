namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Relevance tier.
    /// </summary>
    public enum FindingTierEnum
    {
        /// <summary>
        /// 80 and up.
        /// </summary>
        MustRead,

        /// <summary>
        /// 60 to 79.
        /// </summary>
        WorthALook,

        /// <summary>
        /// 40 to 59.
        /// </summary>
        Fyi,

        /// <summary>
        /// Under the display threshold.
        /// </summary>
        BelowThreshold
    }
}
