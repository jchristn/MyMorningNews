namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Create a credential.
    /// </summary>
    public class CredentialCreateRequest
    {
        /// <summary>
        /// Owning user; defaults to the caller.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Name.
        /// </summary>
        [TextLimit(128, AllowBlank = false)]
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Authentication mode.
        /// </summary>
        public CredentialAuthModeEnum AuthMode { get; set; } = CredentialAuthModeEnum.DirectHeader;

        /// <summary>
        /// Coarse scopes (read, write, admin). Empty means no coarse restriction.
        /// </summary>
        public List<string> Scopes { get; set; } = new List<string>();

        /// <summary>
        /// Resource allow-list.
        /// </summary>
        public List<string> ResourceIds { get; set; } = new List<string>();

        /// <summary>
        /// Expiration.
        /// </summary>
        public DateTime? ExpiresUtc { get; set; } = null;
    }
}
