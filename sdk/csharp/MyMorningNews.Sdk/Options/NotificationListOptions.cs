namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;

    /// <summary>
    /// Filters for listing the caller's in-app notifications.
    /// </summary>
    public class NotificationListOptions : ListOptions
    {
        /// <summary>
        /// True for unread only, false for read only. Null (both) by default.
        /// </summary>
        public bool? Unread { get; set; } = null;

        /// <summary>
        /// Only notifications of this kind. Null by default.
        /// </summary>
        public NotificationKindEnum? Kind { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public NotificationListOptions()
        {
        }

        /// <summary>
        /// Add this resource's filters.
        /// </summary>
        /// <param name="query">Query to add to.</param>
        protected override void AddFilters(Dictionary<string, string> query)
        {
            base.AddFilters(query);
            QueryFormat.Add(query, "unread", Unread);
            QueryFormat.Add(query, "kind", Kind);
        }
    }
}
