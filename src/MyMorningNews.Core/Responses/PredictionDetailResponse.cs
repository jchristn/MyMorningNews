namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// A prediction with its proposed reviews.
    /// </summary>
    public class PredictionDetailResponse
    {
        /// <summary>
        /// Prediction.
        /// </summary>
        public Prediction Prediction { get; set; } = new Prediction();

        /// <summary>
        /// Reviews, newest first.
        /// </summary>
        public List<PredictionReview> Reviews { get; set; } = new List<PredictionReview>();
    }
}
