namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>Native purchase evidence. Server verification and immutable account binding are always required.</summary>
    public class BillingPurchaseRequest
    {
        /// <summary>Apple or Google.</summary>
        public string Provider { get; set; } = String.Empty;
        /// <summary>StoreKit signed transaction, required for Apple. Never logged.</summary>
        public string? SignedTransaction { get; set; } = null;
        /// <summary>Play purchase token, required for Google. Never logged.</summary>
        public string? PurchaseToken { get; set; } = null;
    }
}
