namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;

    /// <summary>
    /// Filters for listing the email outbox. Members see only mail sent to their own address.
    /// </summary>
    public class EmailMessageListOptions : ListOptions
    {
        /// <summary>
        /// Only messages in this status. Null by default.
        /// </summary>
        public EmailStatusEnum? Status { get; set; } = null;

        /// <summary>
        /// Only messages of this template. Null by default.
        /// </summary>
        public EmailTemplateEnum? Template { get; set; } = null;

        /// <summary>
        /// Only messages for this run. Null by default.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// Only messages for this briefing. Null by default.
        /// </summary>
        public string? BriefingId { get; set; } = null;

        /// <summary>
        /// Only messages for this topic. Null by default.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Administrators: only messages to this address. Null by default.
        /// </summary>
        public string? Recipient { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public EmailMessageListOptions()
        {
        }

        /// <summary>
        /// Add this resource's filters.
        /// </summary>
        /// <param name="query">Query to add to.</param>
        protected override void AddFilters(Dictionary<string, string> query)
        {
            base.AddFilters(query);
            QueryFormat.Add(query, "status", Status);
            QueryFormat.Add(query, "template", Template);
            QueryFormat.Add(query, "runId", RunId);
            QueryFormat.Add(query, "briefingId", BriefingId);
            QueryFormat.Add(query, "topicId", TopicId);
            QueryFormat.Add(query, "recipient", Recipient);
        }
    }
}
