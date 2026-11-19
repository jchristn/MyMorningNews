namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Prediction status.
    /// </summary>
    public enum PredictionStatusEnum
    {
        /// <summary>
        /// Open.
        /// </summary>
        Open,

        /// <summary>
        /// A resolution is proposed.
        /// </summary>
        ProposedResolved,

        /// <summary>
        /// Came true.
        /// </summary>
        Accurate,

        /// <summary>
        /// Partly came true.
        /// </summary>
        PartiallyAccurate,

        /// <summary>
        /// Did not come true.
        /// </summary>
        Inaccurate,

        /// <summary>
        /// Cannot be judged.
        /// </summary>
        Unresolvable,

        /// <summary>
        /// Withdrawn.
        /// </summary>
        Withdrawn
    }
}
