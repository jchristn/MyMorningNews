namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for TopicConstraint records. Null properties are not applied.
    /// </summary>
    public class TopicConstraintFilter : EnumerationFilter
    {
        /// <summary>
        /// Match TopicId exactly when set.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Match DimensionId exactly when set.
        /// </summary>
        public string? DimensionId { get; set; } = null;

        /// <summary>
        /// Match Kind exactly when set.
        /// </summary>
        public ConstraintKindEnum? Kind { get; set; } = null;

        /// <summary>
        /// Match Origin exactly when set.
        /// </summary>
        public OriginEnum? Origin { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
