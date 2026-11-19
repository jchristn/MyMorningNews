namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for EditionTopic records. Null properties are not applied.
    /// </summary>
    public class EditionTopicFilter : EnumerationFilter
    {
        /// <summary>
        /// Match EditionId exactly when set.
        /// </summary>
        public string? EditionId { get; set; } = null;

        /// <summary>
        /// Match TopicId exactly when set.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
