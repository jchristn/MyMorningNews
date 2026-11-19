namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Output of research.score.
    /// </summary>
    public class ScoringResult : IStructuredResult
    {
        /// <summary>
        /// Relevance 0 to 100.
        /// </summary>
        [Documentation("Relevance from 0 to 100.")]
        public int Score { get; set; } = 0;

        /// <summary>
        /// Confidence 0 to 1.
        /// </summary>
        [Documentation("Confidence from 0 to 1.")]
        public double Confidence { get; set; } = 0.5;

        /// <summary>
        /// Rationale.
        /// </summary>
        [Documentation("One or two sentences citing the brief or feedback.")]
        public string Rationale { get; set; } = String.Empty;

        /// <inheritdoc />
        public List<string> Validate()
        {
            List<string> errors = new List<string>();
            Score = Math.Clamp(Score, 0, 100);
            Confidence = Math.Clamp(Confidence, 0, 1);
            if (String.IsNullOrWhiteSpace(Rationale)) errors.Add("rationale must not be empty.");
            return errors;
        }
    }
}
