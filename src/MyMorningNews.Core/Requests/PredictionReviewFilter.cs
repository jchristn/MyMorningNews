namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for PredictionReview records. Null properties are not applied.
    /// </summary>
    public class PredictionReviewFilter : EnumerationQuery
    {
        /// <summary>
        /// Match PredictionId exactly when set.
        /// </summary>
        public string? PredictionId { get; set; } = null;

        /// <summary>
        /// Match RunId exactly when set.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// Match AcceptedByUser exactly when set.
        /// </summary>
        public bool? AcceptedByUser { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
