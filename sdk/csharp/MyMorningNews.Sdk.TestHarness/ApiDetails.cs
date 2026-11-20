namespace MyMorningNews.Sdk.TestHarness
{
    using System;

    /// <summary>
    /// One API step inside a case: its name, outcome, HTTP status when known, and timing.
    /// </summary>
    public class ApiDetails
    {
        /// <summary>
        /// Step name.
        /// </summary>
        public string Step { get; set; } = String.Empty;

        /// <summary>
        /// Whether the step completed without an exception.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// HTTP status of a failed call, or 0.
        /// </summary>
        public int StatusCode { get; set; } = 0;

        /// <summary>
        /// Error message of a failed step, or null.
        /// </summary>
        public string? Error { get; set; } = null;

        /// <summary>
        /// Start time (UTC).
        /// </summary>
        public DateTime StartUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// End time (UTC).
        /// </summary>
        public DateTime EndUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Elapsed time.
        /// </summary>
        public TimeSpan Runtime
        {
            get
            {
                return EndUtc - StartUtc;
            }
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ApiDetails()
        {
        }
    }
}
