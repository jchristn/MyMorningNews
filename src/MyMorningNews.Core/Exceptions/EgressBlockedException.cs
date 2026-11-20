namespace MyMorningNews.Core.Exceptions
{
    using System;
    using System.Net.Http;

    /// <summary>
    /// An outbound request was refused because its target is a private or internal network address
    /// (Hosting.RestrictPrivateNetworks). Derives from <see cref="HttpRequestException"/> so HTTP callers treat it as a
    /// failed request.
    /// </summary>
    public class EgressBlockedException : HttpRequestException
    {
        /// <summary>
        /// Create the exception.
        /// </summary>
        /// <param name="message">Message.</param>
        public EgressBlockedException(string message) : base(message)
        {
        }
    }
}
