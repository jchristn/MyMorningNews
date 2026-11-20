namespace MyMorningNews.Benchmark.Client
{
    using System;

    /// <summary>
    /// A briefing.
    /// </summary>
    public class BriefingDto
    {
        /// <summary>
        /// Identifier.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Run, or null.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// Status.
        /// </summary>
        public string Status { get; set; } = String.Empty;

        /// <summary>
        /// Markdown, or null.
        /// </summary>
        public string? Markdown { get; set; } = null;

        /// <summary>
        /// Items included.
        /// </summary>
        public int ItemCount { get; set; } = 0;

        /// <summary>
        /// Sections written without a model.
        /// </summary>
        public int ModelFallbacks { get; set; } = 0;

        /// <summary>
        /// Failure reason, or null.
        /// </summary>
        public string? ErrorMessage { get; set; } = null;

        /// <summary>
        /// Creation time.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Composition time, or null.
        /// </summary>
        public DateTime? ComposedUtc { get; set; } = null;
    }
}
