namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for MediaChapter records. Null properties are not applied.
    /// </summary>
    public class MediaChapterFilter : EnumerationQuery
    {
        /// <summary>
        /// Match MediaAssetId exactly when set.
        /// </summary>
        public string? MediaAssetId { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
