namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// A piece of evidence with where it came from.
    /// </summary>
    public class EvidenceView
    {
        /// <summary>
        /// Evidence.
        /// </summary>
        public Evidence Evidence { get; set; } = new Evidence();

        /// <summary>
        /// Source URL.
        /// </summary>
        public string? SourceUrl { get; set; } = null;

        /// <summary>
        /// Source title.
        /// </summary>
        public string? SourceTitle { get; set; } = null;

        /// <summary>
        /// Source domain.
        /// </summary>
        public string? SourceDomain { get; set; } = null;

        /// <summary>
        /// Source publication time.
        /// </summary>
        public DateTime? SourcePublishedUtc { get; set; } = null;

        /// <summary>
        /// Supports, Contradicts, or Context.
        /// </summary>
        public string Role { get; set; } = "Supports";
    }
}
