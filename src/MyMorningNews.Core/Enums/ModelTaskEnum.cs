namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Pipeline task role used to pick a model.
    /// </summary>
    public enum ModelTaskEnum
    {
        /// <summary>
        /// Topic refinement wizard.
        /// </summary>
        Refinement,

        /// <summary>
        /// Search query planning.
        /// </summary>
        QueryPlanning,

        /// <summary>
        /// Finding and evidence extraction.
        /// </summary>
        Extraction,

        /// <summary>
        /// Relevance scoring.
        /// </summary>
        Scoring,

        /// <summary>
        /// Run synthesis.
        /// </summary>
        Synthesis,

        /// <summary>
        /// Predictions and reviews.
        /// </summary>
        Prediction,

        /// <summary>
        /// Dig-into suggestions.
        /// </summary>
        Suggestion,

        /// <summary>
        /// Briefing composition.
        /// </summary>
        Composition,

        /// <summary>
        /// Audio script.
        /// </summary>
        PodcastScript,

        /// <summary>
        /// Feedback tagging and preference distillation.
        /// </summary>
        FeedbackAnalysis,

        /// <summary>
        /// Embeddings.
        /// </summary>
        Embedding
    }
}
