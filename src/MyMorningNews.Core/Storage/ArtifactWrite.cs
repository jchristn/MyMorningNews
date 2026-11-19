namespace MyMorningNews.Core.Storage
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Content and links for a new artifact.
    /// </summary>
    public class ArtifactWrite
    {
        /// <summary>
        /// Tenant.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Owner.
        /// </summary>
        public string UserId { get; set; } = String.Empty;

        /// <summary>
        /// Kind.
        /// </summary>
        public ArtifactKindEnum Kind { get; set; } = ArtifactKindEnum.BriefingMarkdown;

        /// <summary>
        /// Topic, or null.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Briefing, or null.
        /// </summary>
        public string? BriefingId { get; set; } = null;

        /// <summary>
        /// One-time request, or null.
        /// </summary>
        public string? ResearchRequestId { get; set; } = null;

        /// <summary>
        /// Suggested download file name.
        /// </summary>
        public string FileName { get; set; } = "artifact.bin";

        /// <summary>
        /// Media type.
        /// </summary>
        public string ContentType { get; set; } = "application/octet-stream";

        /// <summary>
        /// File extension without the dot.
        /// </summary>
        public string Extension { get; set; } = "bin";

        /// <summary>
        /// Content.
        /// </summary>
        public byte[] Content { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Pinned from the start (inherits the briefing pin).
        /// </summary>
        public bool Pinned { get; set; } = false;
    }
}
