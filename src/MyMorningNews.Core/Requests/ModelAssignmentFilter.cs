namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for ModelAssignment records. Null properties are not applied.
    /// </summary>
    public class ModelAssignmentFilter : EnumerationQuery
    {
        /// <summary>
        /// Match Task exactly when set.
        /// </summary>
        public ModelTaskEnum? Task { get; set; } = null;

        /// <summary>
        /// Match TopicId exactly when set.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Match ModelEndpointId exactly when set.
        /// </summary>
        public string? ModelEndpointId { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
