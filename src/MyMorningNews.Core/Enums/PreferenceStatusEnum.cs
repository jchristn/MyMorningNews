namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Learned preference status.
    /// </summary>
    public enum PreferenceStatusEnum
    {
        /// <summary>
        /// Waiting for the reader.
        /// </summary>
        Proposed,

        /// <summary>
        /// In use.
        /// </summary>
        Active,

        /// <summary>
        /// Dismissed.
        /// </summary>
        Rejected,

        /// <summary>
        /// Turned off.
        /// </summary>
        Disabled
    }
}
