namespace MyMorningNews.Core.Exceptions
{
    using System;

    /// <summary>
    /// A plan limit was reached (plan section 33.2). Maps to 402 CapReached with the limit key and reset time.
    /// </summary>
    public class CapReachedException : Exception
    {
        /// <summary>
        /// Entitlement key, for example usage.monthlyCostUsd.max or topics.max.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// The limit, as the plan states it.
        /// </summary>
        public string Limit { get; }

        /// <summary>
        /// When the limit resets (UTC), or null for limits that do not reset (such as topic count).
        /// </summary>
        public DateTime? ResetUtc { get; }

        /// <summary>
        /// Create the exception.
        /// </summary>
        /// <param name="key">Entitlement key.</param>
        /// <param name="limit">Limit value.</param>
        /// <param name="resetUtc">Reset time, or null.</param>
        /// <param name="message">Message for the user.</param>
        public CapReachedException(string key, string limit, DateTime? resetUtc, string message) : base(message)
        {
            Key = key ?? String.Empty;
            Limit = limit ?? String.Empty;
            ResetUtc = resetUtc;
        }
    }
}
