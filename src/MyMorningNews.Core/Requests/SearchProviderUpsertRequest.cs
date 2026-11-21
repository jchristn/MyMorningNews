namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Create or update a search provider. Null fields are left unchanged on update.
    /// </summary>
    public class SearchProviderUpsertRequest
    {
        /// <summary>
        /// Name.
        /// </summary>
        [TextLimit(128, AllowBlank = false)]
        public string? Name { get; set; } = null;

        /// <summary>
        /// Kind.
        /// </summary>
        public SearchProviderKindEnum? Kind { get; set; } = null;

        /// <summary>
        /// Endpoint override (SearXNG base URL, or a full API URL).
        /// </summary>
        [TextLimit(512)]
        public string? Endpoint { get; set; } = null;

        /// <summary>
        /// Vault credential holding the API key.
        /// </summary>
        public string? CredentialId { get; set; } = null;

        /// <summary>
        /// API key to store in the vault (write only).
        /// </summary>
        public string? ApiKey { get; set; } = null;

        /// <summary>
        /// Used by topics that do not choose providers.
        /// </summary>
        public bool? IsDefault { get; set; } = null;

        /// <summary>
        /// Results per query, 1 to 50.
        /// </summary>
        public int? MaxResults { get; set; } = null;

        /// <summary>
        /// Timeout, 1000 to 120000.
        /// </summary>
        public int? TimeoutMs { get; set; } = null;

        /// <summary>
        /// Active.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
