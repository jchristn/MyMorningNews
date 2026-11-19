namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Which findings an edition covers.
    /// </summary>
    public enum LookbackModeEnum
    {
        /// <summary>
        /// Since the last composed edition, capped at the lookback hours.
        /// </summary>
        SinceLastEdition,

        /// <summary>
        /// The last N hours.
        /// </summary>
        FixedHours
    }
}
