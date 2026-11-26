namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// How one item of a briefing relates to its comparison baseline (plan section 6.2).
    /// </summary>
    public enum BriefingDeltaKindEnum
    {
        /// <summary>
        /// Not in the baseline: a new development.
        /// </summary>
        NewDevelopment,

        /// <summary>
        /// Updates a baseline item with a material new fact.
        /// </summary>
        ChangedFact,

        /// <summary>
        /// A prediction from the baseline was graded since.
        /// </summary>
        Resolved,

        /// <summary>
        /// Says a baseline item was wrong: a retraction, a corrected figure, or a denial.
        /// </summary>
        Correction,

        /// <summary>
        /// Found again with no material change.
        /// </summary>
        Unchanged,

        /// <summary>
        /// A baseline item whose evidence is no longer available, so it could not be compared.
        /// </summary>
        Unavailable
    }
}
