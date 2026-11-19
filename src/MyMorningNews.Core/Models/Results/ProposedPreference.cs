namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A preference distilled from feedback.
    /// </summary>
    public class ProposedPreference
    {
        /// <summary>
        /// Kind.
        /// </summary>
        [Documentation("Prefer, Avoid, PreferSource, AvoidSource, or StyleNote.")]
        public PreferenceKindEnum Kind { get; set; } = PreferenceKindEnum.Prefer;

        /// <summary>
        /// Statement.
        /// </summary>
        [Documentation("The preference as a short instruction for the research agent.")]
        public string Statement { get; set; } = String.Empty;

        /// <summary>
        /// Confidence.
        /// </summary>
        [Documentation("Confidence from 0 to 1.")]
        public double Confidence { get; set; } = 0.5;

        /// <summary>
        /// Supporting feedback IDs.
        /// </summary>
        [Documentation("Ids of the feedback items that support it (at least three).")]
        public List<string> FeedbackIds { get; set; } = new List<string>();
    }
}
