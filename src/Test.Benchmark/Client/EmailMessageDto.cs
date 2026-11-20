namespace MyMorningNews.Benchmark.Client
{
    using System;

    /// <summary>
    /// An email message in the outbox.
    /// </summary>
    public class EmailMessageDto
    {
        /// <summary>
        /// Identifier.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Template.
        /// </summary>
        public string Template { get; set; } = String.Empty;

        /// <summary>
        /// Run, or null.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// Recipient.
        /// </summary>
        public string Recipient { get; set; } = String.Empty;

        /// <summary>
        /// Status.
        /// </summary>
        public string Status { get; set; } = String.Empty;

        /// <summary>
        /// Rendered HTML, or null.
        /// </summary>
        public string? RenderedHtml { get; set; } = null;

        /// <summary>
        /// Plain text, or null.
        /// </summary>
        public string? RenderedText { get; set; } = null;

        /// <summary>
        /// Last delivery error, or null.
        /// </summary>
        public string? LastError { get; set; } = null;

        /// <summary>
        /// Creation time.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Send time, or null.
        /// </summary>
        public DateTime? SentUtc { get; set; } = null;
    }
}
