namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Prompt lab output.
    /// </summary>
    public class PromptLabResponse
    {
        /// <summary>
        /// Rendered system prompt.
        /// </summary>
        public string RenderedSystem { get; set; } = String.Empty;

        /// <summary>
        /// Rendered user prompt.
        /// </summary>
        public string RenderedUser { get; set; } = String.Empty;

        /// <summary>
        /// Success.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Model output.
        /// </summary>
        public string? Text { get; set; } = null;

        /// <summary>
        /// Error.
        /// </summary>
        public string? Error { get; set; } = null;

        /// <summary>
        /// Model.
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
        /// Logged call.
        /// </summary>
        public string? LlmCallId { get; set; } = null;

        /// <summary>
        /// Variables referenced but not declared.
        /// </summary>
        public List<string> UnknownVariables { get; set; } = new List<string>();
    }
}
