namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>Start web checkout using an operator-configured public offer key.</summary>
    public class BillingCheckoutRequest
    {
        /// <summary>Configured offer key; prices, customers, and redirect URLs cannot be supplied by the caller.</summary>
        public string OfferKey { get; set; } = String.Empty;
    }
}
