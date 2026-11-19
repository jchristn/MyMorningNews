namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// Everything the wizard needs to render a session.
    /// </summary>
    public class RefinementStateResponse
    {
        /// <summary>
        /// Session.
        /// </summary>
        public RefinementSession Session { get; set; } = new RefinementSession();

        /// <summary>
        /// Topic (Draft until completed).
        /// </summary>
        public Topic Topic { get; set; } = new Topic();

        /// <summary>
        /// Dimensions.
        /// </summary>
        public List<TopicDimension> Dimensions { get; set; } = new List<TopicDimension>();

        /// <summary>
        /// Constraints.
        /// </summary>
        public List<TopicConstraint> Constraints { get; set; } = new List<TopicConstraint>();

        /// <summary>
        /// Questions with options, all rounds.
        /// </summary>
        public List<RefinementQuestionResponse> Questions { get; set; } = new List<RefinementQuestionResponse>();

        /// <summary>
        /// Transcript.
        /// </summary>
        public List<RefinementMessage> Messages { get; set; } = new List<RefinementMessage>();
    }
}
