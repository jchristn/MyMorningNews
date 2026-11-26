namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// An edition with its topics and schedules.
    /// </summary>
    public class EditionResponse
    {
        /// <summary>
        /// Edition.
        /// </summary>
        public Edition Edition { get; set; } = new Edition();

        /// <summary>
        /// Listed topics (empty means all active recurring topics).
        /// </summary>
        public List<EditionTopic> Topics { get; set; } = new List<EditionTopic>();

        /// <summary>
        /// Schedules.
        /// </summary>
        public List<Schedule> Schedules { get; set; } = new List<Schedule>();

        /// <summary>
        /// Topics it covers right now.
        /// </summary>
        public List<string> CoveredTopicIds { get; set; } = new List<string>();

        /// <summary>
        /// Catalog publications the edition includes, in order, and whether each can be read now.
        /// </summary>
        public List<EditionPublicationResponse> Publications { get; set; } = new List<EditionPublicationResponse>();
    }
}
