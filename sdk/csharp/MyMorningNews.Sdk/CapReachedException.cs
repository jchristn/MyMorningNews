namespace MyMorningNews.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// A plan limit was reached (HTTP 402 CapReached). Key names the limit (for example topics.max, runs.daily.max,
    /// usage.monthlyCostUsd.max, audio.enabled, or endpoints.custom), Limit is the plan's value for it, and ResetUtc is when
    /// the counter resets, when it does.
    /// </summary>
    public class CapReachedException : MyMorningNewsException
    {
        /// <summary>
        /// Limit key from details.key, for example topics.max. Empty when the server sent none.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// The plan's value for the limit from details.limit, for example 1 or false. Empty when the server sent none.
        /// </summary>
        public string Limit { get; }

        /// <summary>
        /// When the limit resets (details.resetUtc), or null when it does not reset on a schedule.
        /// </summary>
        public DateTime? ResetUtc { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="requestId">Request ID, or null.</param>
        /// <param name="details">Details, or null.</param>
        /// <param name="responseBody">Raw body, or null.</param>
        public CapReachedException(string message, string? requestId, IReadOnlyDictionary<string, string>? details, string? responseBody)
            : base(402, "CapReached", message, requestId, details, responseBody)
        {
            Key = Details.TryGetValue("key", out string? key) ? key : String.Empty;
            Limit = Details.TryGetValue("limit", out string? limit) ? limit : String.Empty;
            ResetUtc = null;
            if (Details.TryGetValue("resetUtc", out string? reset)
                && DateTime.TryParse(reset, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime parsed))
            {
                ResetUtc = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            }
        }
    }
}
