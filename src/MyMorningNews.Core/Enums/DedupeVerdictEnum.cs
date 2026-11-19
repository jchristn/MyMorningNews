namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Duplicate judgment.
    /// </summary>
    public enum DedupeVerdictEnum
    {
        /// <summary>
        /// Same thing.
        /// </summary>
        Duplicate,

        /// <summary>
        /// Adds a material fact to an earlier finding.
        /// </summary>
        Update,

        /// <summary>
        /// Different.
        /// </summary>
        Different
    }
}
