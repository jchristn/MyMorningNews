namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for PromoRedemption records. Null properties are not applied.
    /// </summary>
    public class PromoRedemptionFilter : EnumerationQuery
    {
        /// <summary>
        /// Match PromoCodeId exactly when set.
        /// </summary>
        public string? PromoCodeId { get; set; } = null;

        /// <summary>
        /// Match Code exactly when set.
        /// </summary>
        public string? Code { get; set; } = null;

        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match Channel exactly when set.
        /// </summary>
        public PromoChannelEnum? Channel { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
