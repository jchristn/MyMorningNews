namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for BriefingItem records. Null properties are not applied.
    /// </summary>
    public class BriefingItemFilter : EnumerationQuery
    {
        /// <summary>
        /// Match BriefingId exactly when set.
        /// </summary>
        public string? BriefingId { get; set; } = null;

        /// <summary>
        /// Match SectionId exactly when set.
        /// </summary>
        public string? SectionId { get; set; } = null;

        /// <summary>
        /// Match ItemType exactly when set.
        /// </summary>
        public BriefingItemTypeEnum? ItemType { get; set; } = null;

        /// <summary>
        /// Match ItemId exactly when set.
        /// </summary>
        public string? ItemId { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
