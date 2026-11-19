namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Error body returned for failed requests.
    /// </summary>
    public class ErrorResponse
    {
        /// <summary>
        /// Stable error code, for example NotFound or ValidationFailed.
        /// </summary>
        public string Error { get; set; } = "Error";

        /// <summary>
        /// Human-readable message.
        /// </summary>
        public string Message { get; set; } = String.Empty;

        /// <summary>
        /// Request identifier for support and log correlation.
        /// </summary>
        public string? RequestId { get; set; } = null;

        /// <summary>
        /// Per-field validation errors or extra detail.
        /// </summary>
        public Dictionary<string, string>? Details { get; set; } = null;
    }
}
