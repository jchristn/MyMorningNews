namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Issued session token.
    /// </summary>
    public class TokenResponse
    {
        /// <summary>
        /// Opaque token for Authorization: Bearer or x-token.
        /// </summary>
        public string Token { get; set; } = String.Empty;

        /// <summary>
        /// Expiration.
        /// </summary>
        public DateTime ExpiresUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Tenant.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Session.
        /// </summary>
        public string SessionId { get; set; } = String.Empty;

        /// <summary>
        /// User.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Credential.
        /// </summary>
        public string? CredentialId { get; set; } = null;

        /// <summary>
        /// Principal type.
        /// </summary>
        public PrincipalTypeEnum PrincipalType { get; set; } = PrincipalTypeEnum.User;

        /// <summary>
        /// User profile, for user sessions.
        /// </summary>
        public UserResponse? User { get; set; } = null;

        /// <summary>
        /// Rotating refresh token (mobile clients only, sent with x-client: mobile). Store it securely; each use returns a new one.
        /// </summary>
        public string? RefreshToken { get; set; } = null;

        /// <summary>
        /// Refresh token expiration (UTC), when a refresh token was issued.
        /// </summary>
        public DateTime? RefreshExpiresUtc { get; set; } = null;
    }
}
