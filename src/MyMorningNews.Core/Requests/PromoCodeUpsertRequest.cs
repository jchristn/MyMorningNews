namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Create or update a promotional code. Null fields are unchanged on update.
    /// </summary>
    public class PromoCodeUpsertRequest
    {
        /// <summary>
        /// Code (4 to 40 letters, digits, and dashes; stored uppercase). Required on create.
        /// </summary>
        public string? Code { get; set; } = null;

        /// <summary>
        /// What the code is for.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Plan it gives (default: the complimentary plan).
        /// </summary>
        public string? PlanId { get; set; } = null;

        /// <summary>
        /// Days the plan lasts (1 to 3650); 0 means no end.
        /// </summary>
        public int? DurationDays { get; set; } = null;

        /// <summary>
        /// Most redemptions in total; 0 means unlimited.
        /// </summary>
        public int? MaxRedemptions { get; set; } = null;

        /// <summary>
        /// Redemptions per user (1 to 100).
        /// </summary>
        public int? PerUserLimit { get; set; } = null;

        /// <summary>
        /// Usable from.
        /// </summary>
        public DateTime? StartsUtc { get; set; } = null;

        /// <summary>
        /// Usable until.
        /// </summary>
        public DateTime? ExpiresUtc { get; set; } = null;

        /// <summary>
        /// Can be redeemed.
        /// </summary>
        public bool? Enabled { get; set; } = null;

        /// <summary>
        /// Only at sign-up.
        /// </summary>
        public bool? NewAccountsOnly { get; set; } = null;

        /// <summary>
        /// Remove the start date.
        /// </summary>
        public bool? ClearStartsUtc { get; set; } = null;

        /// <summary>
        /// Remove the expiry.
        /// </summary>
        public bool? ClearExpiresUtc { get; set; } = null;
    }
}
