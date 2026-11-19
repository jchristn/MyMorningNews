namespace MyMorningNews.Core.Responses
{
    using System;

    /// <summary>
    /// Result of a model test call.
    /// </summary>
    public class ModelTestResponse
    {
        /// <summary>
        /// Whether the call succeeded.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Response text.
        /// </summary>
        public string? Text { get; set; } = null;

        /// <summary>
        /// Model used.
        /// </summary>
        public string? Model { get; set; } = null;

        /// <summary>
        /// Input tokens.
        /// </summary>
        public int InputTokens { get; set; } = 0;

        /// <summary>
        /// Output tokens.
        /// </summary>
        public int OutputTokens { get; set; } = 0;

        /// <summary>
        /// Duration.
        /// </summary>
        public double DurationMs { get; set; } = 0;

        /// <summary>
        /// Error.
        /// </summary>
        public string? Error { get; set; } = null;

        /// <summary>
        /// Tool-call probe result, when probed.
        /// </summary>
        public bool? SupportsTools { get; set; } = null;
    }
}
