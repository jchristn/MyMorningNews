namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Create or update a vault credential. Secret is required on create and replaces the stored value when supplied on update.
    /// </summary>
    public class VaultCredentialUpsertRequest
    {
        /// <summary>
        /// Name (required on create).
        /// </summary>
        [TextLimit(128, AllowBlank = false)]
        public string? Name { get; set; } = null;

        /// <summary>
        /// Kind.
        /// </summary>
        public VaultCredentialKindEnum? Kind { get; set; } = null;

        /// <summary>
        /// Provider.
        /// </summary>
        public VaultProviderEnum? Provider { get; set; } = null;

        /// <summary>
        /// Header name for CustomHeader secrets.
        /// </summary>
        [TextLimit(128)]
        public string? HeaderName { get; set; } = null;

        /// <summary>
        /// Username for BasicAuth secrets.
        /// </summary>
        [TextLimit(256)]
        public string? Username { get; set; } = null;

        /// <summary>
        /// Secret value; write-only.
        /// </summary>
        public string? Secret { get; set; } = null;

        /// <summary>
        /// Description.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Expiration.
        /// </summary>
        public DateTime? ExpiresUtc { get; set; } = null;

        /// <summary>
        /// Active.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
