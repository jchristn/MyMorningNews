namespace MyMorningNews.Core.Email
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// What an unsubscribe link stops.
    /// </summary>
    public class UnsubscribeScope
    {
        /// <summary>
        /// Category.
        /// </summary>
        public EmailCategoryEnum Category { get; set; } = EmailCategoryEnum.RunEmails;

        /// <summary>
        /// Topic, or null.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Human description.
        /// </summary>
        public string Description { get; set; } = String.Empty;
    }
}
