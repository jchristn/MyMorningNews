namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Create a one-time request.
    /// </summary>
    public class ResearchRequestCreateRequest
    {
        /// <summary>
        /// What you want, for example "Build me a podcast on the history of the shipping container".
        /// </summary>
        public string RequestText { get; set; } = String.Empty;

        /// <summary>
        /// Report, Podcast, and or Email. Default Report.
        /// </summary>
        public List<string> Deliverables { get; set; } = new List<string>();

        /// <summary>
        /// Length.
        /// </summary>
        public DeliverableLengthEnum Length { get; set; } = DeliverableLengthEnum.Standard;

        /// <summary>
        /// Run once at this time (UTC); null runs as soon as confirmed.
        /// </summary>
        public DateTime? RunAtUtc { get; set; } = null;

        /// <summary>
        /// Recipients when Email is a deliverable; defaults to you.
        /// </summary>
        public List<string> EmailRecipients { get; set; } = new List<string>();

        /// <summary>
        /// Also publish the podcast to your private feed.
        /// </summary>
        public bool PublishToFeed { get; set; } = false;

        /// <summary>
        /// Skip clarifying questions ("Just do it").
        /// </summary>
        public bool SkipQuestions { get; set; } = false;

        /// <summary>
        /// Model endpoint override.
        /// </summary>
        public string? ModelEndpointId { get; set; } = null;
    }
}
