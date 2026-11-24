namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for LearnedPreference records. Null properties are not applied.
    /// </summary>
    public class LearnedPreferenceFilter : EnumerationQuery
    {
        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match TopicId exactly when set.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Match Kind exactly when set.
        /// </summary>
        public PreferenceKindEnum? Kind { get; set; } = null;

        /// <summary>
        /// Match Status exactly when set.
        /// </summary>
        public PreferenceStatusEnum? Status { get; set; } = null;

        /// <summary>
        /// Match Origin exactly when set.
        /// </summary>
        public PreferenceOriginEnum? Origin { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
