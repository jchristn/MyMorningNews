namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;

    /// <summary>
    /// Filters for listing feedback. Only current ratings are listed unless IncludeHistory is true.
    /// </summary>
    public class FeedbackListOptions : OwnedListOptions
    {
        /// <summary>
        /// Only feedback on this kind of item. Null by default.
        /// </summary>
        public FeedbackTargetTypeEnum? TargetType { get; set; } = null;

        /// <summary>
        /// Only feedback on this item. Null by default.
        /// </summary>
        public string? TargetId { get; set; } = null;

        /// <summary>
        /// Only feedback within this topic. Null by default.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Only feedback given through this channel. Null by default.
        /// </summary>
        public FeedbackChannelEnum? Channel { get; set; } = null;

        /// <summary>
        /// Include superseded ratings. Null by default.
        /// </summary>
        public bool? IncludeHistory { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public FeedbackListOptions()
        {
        }

        /// <summary>
        /// Add this resource's filters.
        /// </summary>
        /// <param name="query">Query to add to.</param>
        protected override void AddFilters(Dictionary<string, string> query)
        {
            base.AddFilters(query);
            QueryFormat.Add(query, "targetType", TargetType);
            QueryFormat.Add(query, "targetId", TargetId);
            QueryFormat.Add(query, "topicId", TopicId);
            QueryFormat.Add(query, "channel", Channel);
            QueryFormat.Add(query, "includeHistory", IncludeHistory);
        }
    }
}
