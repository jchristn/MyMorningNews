namespace MyMorningNews.Core.Responses
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// Vault credential as returned by the API (never the secret).
    /// </summary>
    public class VaultCredentialResponse
    {
        /// <summary>
        /// Identifier.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Tenant.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Kind.
        /// </summary>
        public VaultCredentialKindEnum Kind { get; set; } = VaultCredentialKindEnum.ApiKey;

        /// <summary>
        /// Provider.
        /// </summary>
        public VaultProviderEnum Provider { get; set; } = VaultProviderEnum.Custom;

        /// <summary>
        /// Header name.
        /// </summary>
        public string? HeaderName { get; set; } = null;

        /// <summary>
        /// Username.
        /// </summary>
        public string? Username { get; set; } = null;

        /// <summary>
        /// Redacted preview.
        /// </summary>
        public string SecretPreview { get; set; } = String.Empty;

        /// <summary>
        /// Description.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Creator.
        /// </summary>
        public string? CreatedByUserId { get; set; } = null;

        /// <summary>
        /// Last used.
        /// </summary>
        public DateTime? LastUsedUtc { get; set; } = null;

        /// <summary>
        /// Last revealed.
        /// </summary>
        public DateTime? LastRevealedUtc { get; set; } = null;

        /// <summary>
        /// Expiration.
        /// </summary>
        public DateTime? ExpiresUtc { get; set; } = null;

        /// <summary>
        /// Active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last updated.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Map from the model.
        /// </summary>
        /// <param name="v">Vault credential.</param>
        /// <returns>Response.</returns>
        /// <exception cref="ArgumentNullException">v is null.</exception>
        public static VaultCredentialResponse FromModel(VaultCredential v)
        {
            ArgumentNullException.ThrowIfNull(v);
            return new VaultCredentialResponse
            {
                Id = v.Id, TenantId = v.TenantId, Name = v.Name, Kind = v.Kind, Provider = v.Provider, HeaderName = v.HeaderName, Username = v.Username,
                SecretPreview = String.IsNullOrEmpty(v.SecretLast4) ? "****" : "****" + v.SecretLast4, Description = v.Description, CreatedByUserId = v.CreatedByUserId,
                LastUsedUtc = v.LastUsedUtc, LastRevealedUtc = v.LastRevealedUtc, ExpiresUtc = v.ExpiresUtc, Active = v.Active, CreatedUtc = v.CreatedUtc, LastUpdateUtc = v.LastUpdateUtc
            };
        }
    }
}
