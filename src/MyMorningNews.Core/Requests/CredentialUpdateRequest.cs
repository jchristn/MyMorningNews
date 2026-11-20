namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Update a credential. Null properties are unchanged.
    /// </summary>
    public class CredentialUpdateRequest
    {
        /// <summary>
        /// Name.
        /// </summary>
        public string? Name { get; set; } = null;

        /// <summary>
        /// Scopes.
        /// </summary>
        public List<string>? Scopes { get; set; } = null;

        /// <summary>
        /// Resource allow-list.
        /// </summary>
        public List<string>? ResourceIds { get; set; } = null;

        /// <summary>
        /// Expiration.
        /// </summary>
        public DateTime? ExpiresUtc { get; set; } = null;

        /// <summary>
        /// Active.
        /// </summary>
        public bool? Active { get; set; } = null;

        /// <summary>
        /// New auth mode, or null to leave it.
        /// </summary>
        public CredentialAuthModeEnum? AuthMode { get; set; } = null;
    }
}
