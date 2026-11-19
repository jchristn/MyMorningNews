namespace MyMorningNews.Core.Exceptions
{
    using System;

    /// <summary>
    /// A model call failed after retries, or no model is configured for the task.
    /// </summary>
    public class ModelCallException : Exception
    {
        /// <summary>
        /// Provider HTTP status, when known.
        /// </summary>
        public int? StatusCode { get; }

        /// <summary>
        /// Whether the failure is worth retrying later (rate limit, timeout, server error).
        /// </summary>
        public bool Transient { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="statusCode">Provider status.</param>
        /// <param name="transient">Transient failure.</param>
        /// <param name="inner">Inner exception.</param>
        public ModelCallException(string message, int? statusCode = null, bool transient = false, Exception? inner = null) : base(message, inner)
        {
            StatusCode = statusCode;
            Transient = transient;
        }
    }
}
