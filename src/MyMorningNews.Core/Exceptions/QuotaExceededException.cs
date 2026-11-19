namespace MyMorningNews.Core.Exceptions
{
    using System;

    /// <summary>
    /// A per-tenant or per-user limit was reached (for example pinned briefings). The REST layer returns 429 with the message.
    /// </summary>
    public class QuotaExceededException : Exception
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="message">Message shown to the caller.</param>
        public QuotaExceededException(string message) : base(message)
        {
        }
    }
}
