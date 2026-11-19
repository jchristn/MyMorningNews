namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// A one-time request with its plan and clarifying questions.
    /// </summary>
    public class ResearchRequestResponse
    {
        /// <summary>
        /// Request.
        /// </summary>
        public ResearchRequest Request { get; set; } = new ResearchRequest();

        /// <summary>
        /// One-time topic with the brief.
        /// </summary>
        public Topic? Topic { get; set; } = null;

        /// <summary>
        /// Dimensions to cover.
        /// </summary>
        public List<TopicDimension> Dimensions { get; set; } = new List<TopicDimension>();

        /// <summary>
        /// Clarifying questions (at most three).
        /// </summary>
        public List<RefinementQuestionResponse> Questions { get; set; } = new List<RefinementQuestionResponse>();

        /// <summary>
        /// Session holding the questions.
        /// </summary>
        public string? RefinementSessionId { get; set; } = null;
    }
}
