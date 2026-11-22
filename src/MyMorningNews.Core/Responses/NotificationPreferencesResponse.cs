namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Requests;

    /// <summary>
    /// My notification preferences: every kind on every channel, and whether each channel can deliver.
    /// </summary>
    public class NotificationPreferencesResponse
    {
        /// <summary>
        /// Every kind on every channel, in kind order and then channel order.
        /// </summary>
        public List<NotificationPreferenceCell> Preferences { get; set; } = new List<NotificationPreferenceCell>();

        /// <summary>
        /// Whether each channel can deliver right now, in channel order.
        /// </summary>
        public List<NotificationChannelStatus> Channels { get; set; } = new List<NotificationChannelStatus>();

        /// <summary>
        /// My email address, or empty when the account has none.
        /// </summary>
        public string Address { get; set; } = String.Empty;

        /// <summary>
        /// Topics whose run emails I unsubscribed from.
        /// </summary>
        public List<string> UnsubscribedTopicIds { get; set; } = new List<string>();
    }
}
