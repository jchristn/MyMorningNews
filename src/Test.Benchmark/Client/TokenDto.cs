namespace MyMorningNews.Benchmark.Client
{
    using System;

    /// <summary>
    /// A session token (POST /v1.0/token).
    /// </summary>
    public class TokenDto
    {
        /// <summary>
        /// Bearer token.
        /// </summary>
        public string Token { get; set; } = String.Empty;

        /// <summary>
        /// Tenant.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// User, or null for a credential.
        /// </summary>
        public string? UserId { get; set; } = null;
    }
}
