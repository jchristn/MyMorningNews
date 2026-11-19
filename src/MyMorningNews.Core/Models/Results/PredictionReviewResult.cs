namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Output of research.review-predictions.
    /// </summary>
    public class PredictionReviewResult : IStructuredResult
    {
        /// <summary>
        /// Reviews.
        /// </summary>
        [Documentation("One review per prediction you can judge; omit the rest.")]
        public List<ProposedReview> Reviews { get; set; } = new List<ProposedReview>();

        /// <inheritdoc />
        public List<string> Validate()
        {
            List<string> errors = new List<string>();
            if (Reviews == null) Reviews = new List<ProposedReview>();
            Reviews = Reviews.Where(r => r != null && !String.IsNullOrWhiteSpace(r.PredictionId)).ToList();
            foreach (ProposedReview r in Reviews) r.Confidence = Math.Clamp(r.Confidence, 0, 1);
            return errors;
        }
    }
}
