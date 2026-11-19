namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for EmailActionToken records. Null properties are not applied.
    /// </summary>
    public class EmailActionTokenFilter : EnumerationFilter
    {
        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match EmailMessageId exactly when set.
        /// </summary>
        public string? EmailMessageId { get; set; } = null;

        /// <summary>
        /// Match Kind exactly when set.
        /// </summary>
        public ActionTokenKindEnum? Kind { get; set; } = null;

        /// <summary>
        /// Match TargetType exactly when set.
        /// </summary>
        public FeedbackTargetTypeEnum? TargetType { get; set; } = null;

        /// <summary>
        /// Match TargetId exactly when set.
        /// </summary>
        public string? TargetId { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
