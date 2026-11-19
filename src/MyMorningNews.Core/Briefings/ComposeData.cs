namespace MyMorningNews.Core.Briefings
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// Everything a composition can draw on.
    /// </summary>
    public class ComposeData
    {
        /// <summary>
        /// Topics by ID.
        /// </summary>
        public Dictionary<string, Topic> TopicsById { get; set; } = new Dictionary<string, Topic>(StringComparer.Ordinal);

        /// <summary>
        /// Eligible findings.
        /// </summary>
        public List<Finding> Findings { get; set; } = new List<Finding>();

        /// <summary>
        /// Predictions.
        /// </summary>
        public List<Prediction> Predictions { get; set; } = new List<Prediction>();

        /// <summary>
        /// Suggestions.
        /// </summary>
        public List<Suggestion> Suggestions { get; set; } = new List<Suggestion>();

        /// <summary>
        /// Runs in the period.
        /// </summary>
        public List<Run> Runs { get; set; } = new List<Run>();

        /// <summary>
        /// Dimensions by ID.
        /// </summary>
        public Dictionary<string, TopicDimension> Dimensions { get; set; } = new Dictionary<string, TopicDimension>(StringComparer.Ordinal);

        /// <summary>
        /// Sources by ID.
        /// </summary>
        public Dictionary<string, SourceItem> Sources { get; set; } = new Dictionary<string, SourceItem>(StringComparer.Ordinal);
    }
}
