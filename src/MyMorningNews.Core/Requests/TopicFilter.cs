namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for Topic records. Null properties are not applied.
    /// </summary>
    public class TopicFilter : EnumerationQuery
    {
        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match Slug exactly when set.
        /// </summary>
        public string? Slug { get; set; } = null;

        /// <summary>
        /// Match Status exactly when set.
        /// </summary>
        public TopicStatusEnum? Status { get; set; } = null;

        /// <summary>
        /// Match IsOneTime exactly when set.
        /// </summary>
        public bool? IsOneTime { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
