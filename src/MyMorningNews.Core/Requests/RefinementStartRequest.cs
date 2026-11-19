namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Start the refinement wizard for a new topic.
    /// </summary>
    public class RefinementStartRequest
    {
        /// <summary>
        /// What you want to stay on top of.
        /// </summary>
        public string SeedText { get; set; } = String.Empty;

        /// <summary>
        /// Why (work decisions, investing, curiosity, hiring, building).
        /// </summary>
        public string? Goal { get; set; } = null;

        /// <summary>
        /// Depth, beginner to expert.
        /// </summary>
        public string? Depth { get; set; } = null;

        /// <summary>
        /// Horizon in your words.
        /// </summary>
        public string? Horizon { get; set; } = null;

        /// <summary>
        /// What you already know.
        /// </summary>
        public string? Known { get; set; } = null;

        /// <summary>
        /// Model endpoint override for this session.
        /// </summary>
        public string? ModelEndpointId { get; set; } = null;
    }
}
