namespace MyMorningNews.Benchmark.Client
{
    using System;
    using System.Net.Http;
    using MyMorningNews.Benchmark.Reporting;

    /// <summary>
    /// One HTTP exchange with the server: status, body, content type, and client-side duration.
    /// </summary>
    public class ApiResult
    {
        /// <summary>
        /// HTTP method.
        /// </summary>
        public string Method { get; set; } = String.Empty;

        /// <summary>
        /// Path and query.
        /// </summary>
        public string Path { get; set; } = String.Empty;

        /// <summary>
        /// HTTP status, or 0 when no response arrived.
        /// </summary>
        public int Status { get; set; } = 0;

        /// <summary>
        /// Response body.
        /// </summary>
        public string Body { get; set; } = String.Empty;

        /// <summary>
        /// Response content type, or empty.
        /// </summary>
        public string ContentType { get; set; } = String.Empty;

        /// <summary>
        /// Client-side duration in milliseconds.
        /// </summary>
        public double DurationMs { get; set; } = 0;

        /// <summary>
        /// Transport error message when no response arrived, or null.
        /// </summary>
        public string? Error { get; set; } = null;

        /// <summary>
        /// Whether the status is 2xx.
        /// </summary>
        public bool Success
        {
            get { return Status >= 200 && Status < 300; }
        }

        /// <summary>
        /// Whether the body is JSON (the content type says so).
        /// </summary>
        public bool IsJson
        {
            get { return ContentType.Contains("json", StringComparison.OrdinalIgnoreCase); }
        }

        /// <summary>
        /// Deserialize the body.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <returns>Value.</returns>
        /// <exception cref="System.Text.Json.JsonException">The body is not valid JSON for the type.</exception>
        public T Json<T>()
        {
            return Reporting.Json.Deserialize<T>(Body);
        }

        /// <summary>
        /// Throw unless the status is the expected one.
        /// </summary>
        /// <param name="status">Expected status.</param>
        /// <returns>This result.</returns>
        /// <exception cref="HttpRequestException">The status differs.</exception>
        public ApiResult Expect(int status)
        {
            if (Status != status)
                throw new HttpRequestException(Method + " " + Path + " returned " + Status + " (expected " + status + "): " + (Error ?? (Body.Length > 400 ? Body.Substring(0, 400) : Body)));
            return this;
        }
    }
}
