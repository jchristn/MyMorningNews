namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for TopicDimension records. Null properties are not applied.
    /// </summary>
    public class TopicDimensionFilter : EnumerationFilter
    {
        /// <summary>
        /// Match TopicId exactly when set.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Match Origin exactly when set.
        /// </summary>
        public OriginEnum? Origin { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
