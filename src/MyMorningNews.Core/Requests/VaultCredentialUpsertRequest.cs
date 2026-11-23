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

        /// <summary>
        /// Names of optional properties to clear to null on update: headerName, username, description, expiresUtc.
        /// Names are matched without regard to case. A null or absent property means "leave unchanged", so this list is the
        /// only way to remove a value. Naming a property here and giving it a value in the same request is refused, as is any other name.
        /// Null or empty (the default) clears nothing.
        /// </summary>
        public List<string>? ClearFields { get; set; } = null;
    }
}
