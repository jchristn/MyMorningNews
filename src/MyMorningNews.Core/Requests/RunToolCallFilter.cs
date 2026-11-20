namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for RunToolCall records. Null properties are not applied.
    /// </summary>
    public class RunToolCallFilter : EnumerationFilter
    {
        /// <summary>
        /// Match RunId exactly when set.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// Match TopicId exactly when set.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Match Tool exactly when set.
        /// </summary>
        public string? Tool { get; set; } = null;

        /// <summary>
        /// Match Succeeded exactly when set.
        /// </summary>
        public bool? Succeeded { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
