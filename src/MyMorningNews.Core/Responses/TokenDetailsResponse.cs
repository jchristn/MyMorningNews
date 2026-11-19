namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Details of the presented token and principal.
    /// </summary>
    public class TokenDetailsResponse
    {
        /// <summary>
        /// Whether the token authenticated.
        /// </summary>
        public bool Valid { get; set; } = false;

        /// <summary>
        /// Session.
        /// </summary>
        public string? SessionId { get; set; } = null;

        /// <summary>
        /// Principal type.
        /// </summary>
        public PrincipalTypeEnum? PrincipalType { get; set; } = null;

        /// <summary>
        /// Scheme.
        /// </summary>
        public AuthSchemeEnum? Scheme { get; set; } = null;

        /// <summary>
        /// Tenant.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Tenant name.
        /// </summary>
        public string? TenantName { get; set; } = null;

        /// <summary>
        /// User.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Credential.
        /// </summary>
        public string? CredentialId { get; set; } = null;

        /// <summary>
        /// Email.
        /// </summary>
        public string? Email { get; set; } = null;

        /// <summary>
        /// Display name.
        /// </summary>
        public string? DisplayName { get; set; } = null;

        /// <summary>
        /// Global administrator.
        /// </summary>
        public bool IsAdmin { get; set; } = false;

        /// <summary>
        /// Tenant administrator.
        /// </summary>
        public bool IsTenantAdmin { get; set; } = false;

        /// <summary>
        /// Issued.
        /// </summary>
        public DateTime? IssuedUtc { get; set; } = null;

        /// <summary>
        /// Expiration.
        /// </summary>
        public DateTime? ExpiresUtc { get; set; } = null;
    }
}
