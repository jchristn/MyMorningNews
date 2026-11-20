namespace MyMorningNews.Core.Entitlements
{
    using System;

    /// <summary>
    /// A promotional code cannot be redeemed. The REST layer returns 409 PromoCodeInvalid with the reason.
    /// </summary>
    public class PromoCodeException : Exception
    {
        /// <summary>
        /// Reason: NotFound, Disabled, NotStarted, Expired, Exhausted, AlreadyRedeemed, or NewAccountsOnly.
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="reason">Reason code.</param>
        /// <param name="message">Message for people.</param>
        public PromoCodeException(string reason, string message) : base(message)
        {
            Reason = reason ?? "Invalid";
        }
    }
}
