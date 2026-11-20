namespace MyMorningNews.Core.Entitlements
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// Usage of one code.
    /// </summary>
    public class PromoCodeStats
    {
        /// <summary>
        /// Code.
        /// </summary>
        public PromoCode Code { get; set; } = new PromoCode();

        /// <summary>
        /// Plan name.
        /// </summary>
        public string? PlanName { get; set; } = null;

        /// <summary>
        /// Redemptions left, or null for unlimited.
        /// </summary>
        public int? Remaining { get; set; } = null;

        /// <summary>
        /// Plan assignments from this code still in effect.
        /// </summary>
        public int ActiveAssignments { get; set; } = 0;

        /// <summary>
        /// Redemptions revoked.
        /// </summary>
        public int Revoked { get; set; } = 0;

        /// <summary>
        /// Last redemption.
        /// </summary>
        public DateTime? LastRedeemedUtc { get; set; } = null;
    }
}
