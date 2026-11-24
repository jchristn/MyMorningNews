namespace MyMorningNews.Core.Responses
{
    using System;

    /// <summary>Public catalog projection. Contains no canonical workspace, prompts, credentials, or research trace.</summary>
    public class CatalogEntryResponse
    {
        /// <summary>Publication identifier.</summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>Public title.</summary>
        public string Title { get; set; } = String.Empty;

        /// <summary>Public description.</summary>
        public string? Description { get; set; } = null;

        /// <summary>Category.</summary>
        public string Category { get; set; } = String.Empty;

        /// <summary>Language.</summary>
        public string Language { get; set; } = String.Empty;

        /// <summary>Publication state.</summary>
        public string Status { get; set; } = String.Empty;

        /// <summary>Available to free accounts.</summary>
        public bool FreeEligible { get; set; } = false;

        /// <summary>Caller may subscribe under their effective plan.</summary>
        public bool Eligible { get; set; } = false;

        /// <summary>Caller has an active subscription.</summary>
        public bool Subscribed { get; set; } = false;

        /// <summary>Subscription retained but suspended under the current allowance.</summary>
        public bool Suspended { get; set; } = false;

        /// <summary>Subscriber email preference; null when not subscribed.</summary>
        public bool? EmailEnabled { get; set; } = null;

        /// <summary>Subscriber push preference; null when not subscribed.</summary>
        public bool? PushEnabled { get; set; } = null;

        /// <summary>Display order.</summary>
        public int SortOrder { get; set; } = 0;

        /// <summary>First publication time.</summary>
        public DateTime? PublishedUtc { get; set; } = null;

    }
}
