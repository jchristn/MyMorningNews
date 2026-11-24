namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>Personal delivery preferences for a shared briefing subscription.</summary>
    public class CatalogSubscriptionRequest
    {
        /// <summary>Email new issues.</summary>
        public bool EmailEnabled { get; set; } = true;

        /// <summary>Notify mobile devices about new issues.</summary>
        public bool PushEnabled { get; set; } = true;

    }
}
