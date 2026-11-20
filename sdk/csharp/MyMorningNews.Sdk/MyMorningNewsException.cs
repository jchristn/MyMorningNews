namespace MyMorningNews.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The server answered with an error status. Carries the status, the server's error code, message, request ID, details,
    /// and the raw response body.
    /// </summary>
    public class MyMorningNewsException : Exception
    {
        /// <summary>
        /// HTTP status code, or 0 when no response was received.
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// Server error code such as NotFound, ValidationFailed, Conflict, or Forbidden. Null when the body had none.
        /// </summary>
        public string? ErrorCode { get; }

        /// <summary>
        /// Server request identifier (also in the X-Request-Id header), or null.
        /// </summary>
        public string? RequestId { get; }

        /// <summary>
        /// Error details (for example field validation messages). Never null; empty when the server sent none.
        /// For 403 responses this also carries reason, requiredPermission, and requestType.
        /// </summary>
        public IReadOnlyDictionary<string, string> Details { get; }

        /// <summary>
        /// Raw response body, or null.
        /// </summary>
        public string? ResponseBody { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="statusCode">HTTP status code.</param>
        /// <param name="errorCode">Server error code, or null.</param>
        /// <param name="message">Message.</param>
        /// <param name="requestId">Request ID, or null.</param>
        /// <param name="details">Details, or null.</param>
        /// <param name="responseBody">Raw body, or null.</param>
        /// <param name="innerException">Inner exception, or null.</param>
        public MyMorningNewsException(int statusCode, string? errorCode, string message, string? requestId, IReadOnlyDictionary<string, string>? details, string? responseBody, Exception? innerException = null)
            : base(String.IsNullOrWhiteSpace(message) ? "The server returned HTTP " + statusCode + "." : message, innerException)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
            RequestId = requestId;
            Details = details ?? new Dictionary<string, string>();
            ResponseBody = responseBody;
        }

        /// <summary>
        /// Summary including status, code, and request ID.
        /// </summary>
        /// <returns>Text.</returns>
        public override string ToString()
        {
            return "MyMorningNewsException (" + StatusCode + (ErrorCode != null ? " " + ErrorCode : String.Empty) + (RequestId != null ? ", request " + RequestId : String.Empty) + "): " + Message;
        }
    }
}
