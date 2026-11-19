namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// What feedback is about.
    /// </summary>
    public enum FeedbackTargetTypeEnum
    {
        /// <summary>
        /// Finding.
        /// </summary>
        Finding,

        /// <summary>
        /// Prediction.
        /// </summary>
        Prediction,

        /// <summary>
        /// Proposed prediction resolution.
        /// </summary>
        PredictionReview,

        /// <summary>
        /// Suggestion.
        /// </summary>
        Suggestion,

        /// <summary>
        /// Source.
        /// </summary>
        SourceItem,

        /// <summary>
        /// Source domain.
        /// </summary>
        Domain,

        /// <summary>
        /// Briefing.
        /// </summary>
        Briefing,

        /// <summary>
        /// Briefing section.
        /// </summary>
        BriefingSection,

        /// <summary>
        /// Learned preference.
        /// </summary>
        LearnedPreference
    }
}
