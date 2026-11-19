namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A push notification. Payload data carries identifiers only, never briefing content.
    /// </summary>
    public class PushMessage
    {
        /// <summary>
        /// Title.
        /// </summary>
        public string Title { get; set; } = String.Empty;

        /// <summary>
        /// Body text.
        /// </summary>
        public string Body { get; set; } = String.Empty;

        /// <summary>
        /// Android notification channel: briefings, predictions, or account. Default account.
        /// </summary>
        public string ChannelId { get; set; } = "account";

        /// <summary>
        /// Deep-link data, for example kind and briefingId.
        /// </summary>
        public Dictionary<string, string> Data { get; set; } = new Dictionary<string, string>();
    }
}
