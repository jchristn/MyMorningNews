namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Entitlements;
    using MyMorningNews.Core.Models;

    /// <summary>A server-selected billing redirect. The URL alone never grants paid access.</summary>
    public class BillingRedirectResponse
    {
        /// <summary>Checkout operation identifier, or null for portal.</summary>
        public string? Id { get; set; } = null;

        /// <summary>HTTPS provider destination.</summary>
        public string Url { get; set; } = String.Empty;

        /// <summary>Checkout expiry if applicable.</summary>
        public DateTime? ExpiresUtc { get; set; } = null;

    }
}
