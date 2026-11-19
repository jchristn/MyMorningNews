namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for Artifact records. Null properties are not applied.
    /// </summary>
    public class ArtifactFilter : EnumerationFilter
    {
        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match Kind exactly when set.
        /// </summary>
        public ArtifactKindEnum? Kind { get; set; } = null;

        /// <summary>
        /// Match TopicId exactly when set.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Match BriefingId exactly when set.
        /// </summary>
        public string? BriefingId { get; set; } = null;

        /// <summary>
        /// Match ResearchRequestId exactly when set.
        /// </summary>
        public string? ResearchRequestId { get; set; } = null;

        /// <summary>
        /// Match StorageProvider exactly when set.
        /// </summary>
        public StorageProviderEnum? StorageProvider { get; set; } = null;

        /// <summary>
        /// Match Pinned exactly when set.
        /// </summary>
        public bool? Pinned { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
