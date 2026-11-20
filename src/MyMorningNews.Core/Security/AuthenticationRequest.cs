namespace MyMorningNews.Core.Security
{
    using System;

    /// <summary>
    /// Transport-neutral view of the authentication material on a request.
    /// </summary>
    public class AuthenticationRequest
    {
        /// <summary>
        /// Header lookup (case-insensitive). Returns null when absent.
        /// </summary>
        public Func<string, string?> GetHeader { get; set; } = name => null;

        /// <summary>
        /// Source IP.
        /// </summary>
        public string? SourceIp { get; set; } = null;

        /// <summary>
        /// User agent.
        /// </summary>
        public string? UserAgent { get; set; } = null;

        /// <summary>
        /// HTTP method in upper case, for signed requests. Empty when unknown.
        /// </summary>
        public string Method { get; set; } = String.Empty;

        /// <summary>
        /// Path and query exactly as sent, for signed requests. Empty when unknown.
        /// </summary>
        public string PathAndQuery { get; set; } = String.Empty;

        /// <summary>
        /// Body reader for signed requests. Called only when a request carries x-signature. Returns an empty array for
        /// no body. Null when the transport cannot supply the body.
        /// </summary>
        public Func<byte[]>? GetBody { get; set; } = null;
    }
}
