namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Output of research.predict.
    /// </summary>
    public class PredictionResult : IStructuredResult
    {
        /// <summary>
        /// Predictions.
        /// </summary>
        [Documentation("0 to 5 predictions.")]
        public List<ProposedPrediction> Predictions { get; set; } = new List<ProposedPrediction>();

        /// <inheritdoc />
        public List<string> Validate()
        {
            List<string> errors = new List<string>();
            if (Predictions == null) Predictions = new List<ProposedPrediction>();
            Predictions = Predictions.Where(p => p != null && !String.IsNullOrWhiteSpace(p.Statement)).Take(5).ToList();
            foreach (ProposedPrediction p in Predictions) p.Confidence = Math.Clamp(p.Confidence, 0, 1);
            return errors;
        }
    }
}
