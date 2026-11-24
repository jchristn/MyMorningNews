namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for NotificationPreference records. Null properties are not applied.
    /// </summary>
    public class NotificationPreferenceFilter : EnumerationQuery
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
        /// Match Channel exactly when set.
        /// </summary>
        public NotificationChannelEnum? Channel { get; set; } = null;

        /// <summary>
        /// Match Enabled exactly when set.
        /// </summary>
        public bool? Enabled { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
