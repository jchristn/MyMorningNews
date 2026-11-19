namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A proposed prediction.
    /// </summary>
    public class ProposedPrediction
    {
        /// <summary>
        /// Falsifiable statement.
        /// </summary>
        [Documentation("A falsifiable statement.")]
        public string Statement { get; set; } = String.Empty;

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
        /// Horizon date.
        /// </summary>
        [Documentation("ISO 8601 date by which it can be judged.")]
        public string Horizon { get; set; } = String.Empty;

        /// <summary>
        /// Supporting finding titles.
        /// </summary>
        [Documentation("Titles of supporting findings.")]
        public List<string> Findings { get; set; } = new List<string>();
    }
}
