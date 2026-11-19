namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for TopicRecipient records. Null properties are not applied.
    /// </summary>
    public class TopicRecipientFilter : EnumerationFilter
    {
        /// <summary>
        /// Match TopicId exactly when set.
        /// </summary>
        public string? TopicId { get; set; } = null;
    }
}
