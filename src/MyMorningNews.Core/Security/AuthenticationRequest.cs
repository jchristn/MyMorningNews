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
    }
}
