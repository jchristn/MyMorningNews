namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for MediaAsset records. Null properties are not applied.
    /// </summary>
    public class MediaAssetFilter : EnumerationQuery
    {
        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match BriefingId exactly when set.
        /// </summary>
        public string? BriefingId { get; set; } = null;

        /// <summary>
        /// Match EditionId exactly when set.
        /// </summary>
        public string? EditionId { get; set; } = null;

        /// <summary>
        /// Match Kind exactly when set.
        /// </summary>
        public MediaKindEnum? Kind { get; set; } = null;

        /// <summary>
        /// Match Status exactly when set.
        /// </summary>
        public MediaStatusEnum? Status { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
