namespace MyMorningNews.Core.Email
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// What a run digest shows.
    /// </summary>
    public class DigestContent
    {
        /// <summary>
        /// Lead paragraph.
        /// </summary>
        public string Lead { get; set; } = String.Empty;

        /// <summary>
        /// Top findings by rank.
        /// </summary>
        public List<Finding> Findings { get; set; } = new List<Finding>();

        /// <summary>
        /// Sources by finding ID.
        /// </summary>
        public Dictionary<string, SourceItem> Sources { get; set; } = new Dictionary<string, SourceItem>(StringComparer.Ordinal);

        /// <summary>
        /// Predictions made in this run.
        /// </summary>
        public List<Prediction> NewPredictions { get; set; } = new List<Prediction>();

        /// <summary>
        /// Predictions with a proposed resolution or past their horizon.
        /// </summary>
        public List<Prediction> ToGrade { get; set; } = new List<Prediction>();

        /// <summary>
        /// Latest proposed review by prediction ID.
        /// </summary>
        public Dictionary<string, PredictionReview> Reviews { get; set; } = new Dictionary<string, PredictionReview>(StringComparer.Ordinal);

        /// <summary>
        /// New suggestions from this run.
        /// </summary>
        public List<Suggestion> Suggestions { get; set; } = new List<Suggestion>();

        /// <summary>
        /// Learned preference proposals waiting for the reader.
        /// </summary>
        public List<LearnedPreference> Proposals { get; set; } = new List<LearnedPreference>();
    }
}
