namespace MyMorningNews.Core.Models
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Captured HTTP request and response. Header and body values are diagnostic data; secrets are redacted before storage.
    /// </summary>
    public class RequestHistoryEntry
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix req_).
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Id
        {
            get
            {
                return _Id;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Id));
                _Id = value;
            }
        }

        /// <summary>
        /// Tenant identifier. Null for unauthenticated requests.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// User identifier. Null for unauthenticated requests.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Principal display name, when resolved.
        /// </summary>
        public string? PrincipalName { get; set; } = null;

        /// <summary>
        /// HTTP method.
        /// </summary>
        public string Method { get; set; } = String.Empty;

        /// <summary>
        /// URL path without the query string.
        /// </summary>
        public string Path { get; set; } = String.Empty;

        /// <summary>
        /// Matched route template, when known.
        /// </summary>
        public string? RouteTemplate { get; set; } = null;

        /// <summary>
        /// Full request URL including the query string.
        /// </summary>
        public string Url { get; set; } = String.Empty;

        /// <summary>
        /// Response status code.
        /// </summary>
        public int StatusCode { get; set; } = 0;

        /// <summary>
        /// Request duration in milliseconds.
        /// </summary>
        public double DurationMs { get; set; } = 0;

        /// <summary>
        /// Client source IP.
        /// </summary>
        public string? SourceIp { get; set; } = null;

        /// <summary>
        /// Request headers with secrets redacted.
        /// </summary>
        public Dictionary<string, string> RequestHeaders { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Request body, possibly truncated.
        /// </summary>
        public string? RequestBody { get; set; } = null;

        /// <summary>
        /// Original request body length in bytes.
        /// </summary>
        public long RequestBodyBytes { get; set; } = 0;

        /// <summary>
        /// Whether the request body was truncated.
        /// </summary>
        public bool RequestBodyTruncated { get; set; } = false;

        /// <summary>
        /// Response headers.
        /// </summary>
        public Dictionary<string, string> ResponseHeaders { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Response body, possibly truncated.
        /// </summary>
        public string? ResponseBody { get; set; } = null;

        /// <summary>
        /// Original response body length in bytes.
        /// </summary>
        public long ResponseBodyBytes { get; set; } = 0;

        /// <summary>
        /// Whether the response body was truncated.
        /// </summary>
        public bool ResponseBodyTruncated { get; set; } = false;

        /// <summary>
        /// UTC time the request began.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC time the response was sent.
        /// </summary>
        public DateTime? CompletedUtc { get; set; } = null;

        #endregion

        #region Private-Members

        private string _Id = IdGenerator.GenerateRequestHistoryId();

        #endregion
    }
}
