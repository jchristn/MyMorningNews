namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A proposed resolution of an open prediction.
    /// </summary>
    public class ProposedReview
    {
        /// <summary>
        /// Prediction ID.
        /// </summary>
        [Documentation("The prediction ID shown in brackets.")]
        public string PredictionId { get; set; } = String.Empty;

        /// <summary>
        /// Proposed status.
        /// </summary>
        [Documentation("Accurate, PartiallyAccurate, Inaccurate, or Unresolvable.")]
        public PredictionStatusEnum Status { get; set; } = PredictionStatusEnum.Unresolvable;

        /// <summary>
        /// Rationale.
        /// </summary>
        [Documentation("Why.")]
        public string Rationale { get; set; } = String.Empty;

        /// <summary>
        /// Confidence 0 to 1.
        /// </summary>
        [Documentation("Confidence from 0 to 1.")]
        public double Confidence { get; set; } = 0.5;

        /// <summary>
        /// Finding titles.
        /// </summary>
        [Documentation("Titles of findings that support the call.")]
        public List<string> Findings { get; set; } = new List<string>();
    }
}
