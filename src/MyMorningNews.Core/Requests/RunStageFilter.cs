namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for RunStage records. Null properties are not applied.
    /// </summary>
    public class RunStageFilter : EnumerationFilter
    {
        /// <summary>
        /// Match RunId exactly when set.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// Match Stage exactly when set.
        /// </summary>
        public string? Stage { get; set; } = null;

        /// <summary>
        /// Match Status exactly when set.
        /// </summary>
        public StageStatusEnum? Status { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
