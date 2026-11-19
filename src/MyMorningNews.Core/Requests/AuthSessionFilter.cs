namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for AuthSession records. Null properties are not applied.
    /// </summary>
    public class AuthSessionFilter : EnumerationFilter
    {
        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match CredentialId exactly when set.
        /// </summary>
        public string? CredentialId { get; set; } = null;

        /// <summary>
        /// Match PrincipalType exactly when set.
        /// </summary>
        public PrincipalTypeEnum? PrincipalType { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
