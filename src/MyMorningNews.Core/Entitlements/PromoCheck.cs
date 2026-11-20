namespace MyMorningNews.Core.Entitlements
{
    using System;

    /// <summary>
    /// Whether a code can be redeemed.
    /// </summary>
    public class PromoCheck
    {
        /// <summary>
        /// Can be redeemed.
        /// </summary>
        public bool Valid { get; set; } = false;

        /// <summary>
        /// Why not: NotFound, Disabled, NotStarted, Expired, Exhausted, AlreadyRedeemed, or NewAccountsOnly.
        /// </summary>
        public string? Reason { get; set; } = null;

        /// <summary>
        /// Plan it gives.
        /// </summary>
        public string? PlanName { get; set; } = null;

        /// <summary>
        /// Days, or null for no end.
        /// </summary>
        public int? DurationDays { get; set; } = null;

        /// <summary>
        /// Message for people.
        /// </summary>
        public string Message { get; set; } = String.Empty;
    }
}
