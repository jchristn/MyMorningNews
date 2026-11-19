namespace MyMorningNews.Core.Responses
{
    using System;

    /// <summary>
    /// Revealed secret. Returned only by the audited reveal action with Cache-Control no-store.
    /// </summary>
    public class VaultRevealResponse
    {
        /// <summary>
        /// Credential identifier.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Plaintext secret.
        /// </summary>
        public string Secret { get; set; } = String.Empty;

        /// <summary>
        /// Username for BasicAuth.
        /// </summary>
        public string? Username { get; set; } = null;

        /// <summary>
        /// Reveal time.
        /// </summary>
        public DateTime RevealedUtc { get; set; } = DateTime.UtcNow;
    }
}
