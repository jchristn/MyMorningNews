namespace MyMorningNews.Core.Requests
{
    using System.Collections.Generic;

    /// <summary>
    /// Change my notification preferences. Only the cells that are sent change.
    /// </summary>
    public class NotificationPreferencesRequest
    {
        /// <summary>
        /// Cells to change. Null or empty changes none.
        /// </summary>
        public List<NotificationPreferenceCell>? Preferences { get; set; } = null;

        /// <summary>
        /// Topics to stop run emails for. May be null.
        /// </summary>
        public List<string>? UnsubscribeTopicIds { get; set; } = null;

        /// <summary>
        /// Topics to get run emails for again. May be null.
        /// </summary>
        public List<string>? ResubscribeTopicIds { get; set; } = null;
    }
}
