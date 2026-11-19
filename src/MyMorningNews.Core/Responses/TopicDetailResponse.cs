namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// A topic with its dimensions, constraints, and recipients.
    /// </summary>
    public class TopicDetailResponse
    {
        /// <summary>
        /// Topic.
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
        /// Recipients.
        /// </summary>
        public List<TopicRecipient> Recipients { get; set; } = new List<TopicRecipient>();
    }
}
