namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for Prediction records. Null properties are not applied.
    /// </summary>
    public class PredictionFilter : EnumerationQuery
    {
        /// <summary>
        /// Match RunId exactly when set.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// Match TopicId exactly when set.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match Status exactly when set.
        /// </summary>
        public PredictionStatusEnum? Status { get; set; } = null;

        /// <summary>
        /// Match IsDryRun exactly when set.
        /// </summary>
        public bool? IsDryRun { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
