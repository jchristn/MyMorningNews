namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for EmailSuppression records. Null properties are not applied.
    /// </summary>
    public class EmailSuppressionFilter : EnumerationQuery
    {
        /// <summary>
        /// Match Recipient exactly when set.
        /// </summary>
        public string? Recipient { get; set; } = null;

        /// <summary>
        /// Match Category exactly when set.
        /// </summary>
        public EmailCategoryEnum? Category { get; set; } = null;

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
