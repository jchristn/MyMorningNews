namespace MyMorningNews.McpServer
{
    using System;

    /// <summary>
    /// A validated caller remembered by <see cref="CallerAuthenticator"/>.
    /// </summary>
    public class CachedCaller
    {
        /// <summary>
        /// Principal (user or credential ID).
        /// </summary>
        public string Principal { get; }

        /// <summary>
        /// When the entry must be checked again (UTC).
        /// </summary>
        public DateTime ExpiresUtc { get; }

        /// <summary>
        /// Create an entry.
        /// </summary>
        /// <param name="principal">Principal.</param>
        /// <param name="expiresUtc">Expiry.</param>
        /// <exception cref="ArgumentNullException">Principal is null.</exception>
        public CachedCaller(string principal, DateTime expiresUtc)
        {
            Principal = principal ?? throw new ArgumentNullException(nameof(principal));
            ExpiresUtc = expiresUtc;
        }
    }
}
