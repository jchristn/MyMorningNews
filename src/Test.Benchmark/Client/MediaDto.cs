namespace MyMorningNews.Benchmark.Client
{
    using System;

    /// <summary>
    /// An audio episode.
    /// </summary>
    public class MediaDto
    {
        /// <summary>
        /// Identifier.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Briefing.
        /// </summary>
        public string BriefingId { get; set; } = String.Empty;

        /// <summary>
        /// Status: Queued, Scripting, Synthesizing, Ready, or Failed.
        /// </summary>
        public string Status { get; set; } = String.Empty;

        /// <summary>
        /// Audio media type, or null.
        /// </summary>
        public string? ContentType { get; set; } = null;

        /// <summary>
        /// Audio size in bytes.
        /// </summary>
        public long Bytes { get; set; } = 0;

        /// <summary>
        /// Segments synthesized.
        /// </summary>
        public int SegmentCount { get; set; } = 0;

        /// <summary>
        /// Failure reason, or null.
        /// </summary>
        public string? ErrorMessage { get; set; } = null;

        /// <summary>
        /// Creation time.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Completion time, or null.
        /// </summary>
        public DateTime? CompletedUtc { get; set; } = null;
    }
}
