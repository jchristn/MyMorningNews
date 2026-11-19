namespace MyMorningNews.Core.Security
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Contents of an opaque session token. Only platform-controlled identifiers.
    /// </summary>
    public class SessionTokenPayload
    {
        /// <summary>
        /// Session identifier.
        /// </summary>
        public string SessionId { get; set; } = String.Empty;

        /// <summary>
        /// Random token identifier; must match the session row.
        /// </summary>
        public string TokenId { get; set; } = String.Empty;

        /// <summary>
        /// Principal type.
        /// </summary>
        public PrincipalTypeEnum PrincipalType { get; set; } = PrincipalTypeEnum.User;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// User identifier, for user sessions.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Credential identifier, for credential sessions.
        /// </summary>
        public string? CredentialId { get; set; } = null;

        /// <summary>
        /// Issuing scheme.
        /// </summary>
        public AuthSchemeEnum Scheme { get; set; } = AuthSchemeEnum.PasswordHeaders;

        /// <summary>
        /// UTC issue time.
        /// </summary>
        public DateTime IssuedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC expiration time.
        /// </summary>
        public DateTime ExpiresUtc { get; set; } = DateTime.UtcNow;
    }
}
