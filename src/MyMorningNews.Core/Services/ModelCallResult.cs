namespace MyMorningNews.Core.Services
{
    using System;

    /// <summary>
    /// Text result of a model call.
    /// </summary>
    public class ModelCallResult
    {
        /// <summary>
        /// Response text.
        /// </summary>
        public string Text { get; set; } = String.Empty;

        /// <summary>
        /// Model used.
        /// </summary>
        public string? Model { get; set; } = null;

        /// <summary>
        /// Endpoint used.
        /// </summary>
        public string? ModelEndpointId { get; set; } = null;

        /// <summary>
        /// Input tokens.
        /// </summary>
        public int InputTokens { get; set; } = 0;

        /// <summary>
        /// Output tokens.
        /// </summary>
        public int OutputTokens { get; set; } = 0;

        /// <summary>
        /// Duration in milliseconds.
        /// </summary>
        public double DurationMs { get; set; } = 0;

        /// <summary>
        /// Repair turns used for structured output.
        /// </summary>
        public int Repairs { get; set; } = 0;

        /// <summary>
        /// Logged call identifier.
        /// </summary>
        public string? LlmCallId { get; set; } = null;
    }
}
