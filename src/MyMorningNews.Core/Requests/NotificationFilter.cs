namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for Notification records. Null properties are not applied.
    /// </summary>
    public class NotificationFilter : EnumerationFilter
    {
        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match Kind exactly when set.
        /// </summary>
        public NotificationKindEnum? Kind { get; set; } = null;

        /// <summary>
        /// True for unread only, false for read only. Null for both.
        /// </summary>
        public bool? Unread { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
