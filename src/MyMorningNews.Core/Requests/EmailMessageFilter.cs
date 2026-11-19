namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for EmailMessage records. Null properties are not applied.
    /// </summary>
    public class EmailMessageFilter : EnumerationFilter
    {
        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match Template exactly when set.
        /// </summary>
        public EmailTemplateEnum? Template { get; set; } = null;

        /// <summary>
        /// Match RunId exactly when set.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// Match BriefingId exactly when set.
        /// </summary>
        public string? BriefingId { get; set; } = null;

        /// <summary>
        /// Match TopicId exactly when set.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Match Recipient exactly when set.
        /// </summary>
        public string? Recipient { get; set; } = null;

        /// <summary>
        /// Match Status exactly when set.
        /// </summary>
        public EmailStatusEnum? Status { get; set; } = null;

        /// <summary>
        /// Match Provider exactly when set.
        /// </summary>
        public EmailProviderEnum? Provider { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
