namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// Credential as returned by the API. The secret is never included after creation.
    /// </summary>
    public class CredentialResponse
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
        /// Owning user.
        /// </summary>
        public string UserId { get; set; } = String.Empty;

        /// <summary>
        /// Name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Access key.
        /// </summary>
        public string AccessKey { get; set; } = String.Empty;

        /// <summary>
        /// Redacted secret preview.
        /// </summary>
        public string SecretKeyPreview { get; set; } = String.Empty;

        /// <summary>
        /// Authentication mode.
        /// </summary>
        public CredentialAuthModeEnum AuthMode { get; set; } = CredentialAuthModeEnum.DirectHeader;

        /// <summary>
        /// Coarse scopes.
        /// </summary>
        public List<string> Scopes { get; set; } = new List<string>();

        /// <summary>
        /// Resource allow-list.
        /// </summary>
        public List<string> ResourceIds { get; set; } = new List<string>();

        /// <summary>
        /// Last used.
        /// </summary>
        public DateTime? LastUsedUtc { get; set; } = null;

        /// <summary>
        /// Expiration.
        /// </summary>
        public DateTime? ExpiresUtc { get; set; } = null;

        /// <summary>
        /// Protected.
        /// </summary>
        public bool IsProtected { get; set; } = false;

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
        /// <param name="c">Credential.</param>
        /// <returns>Response.</returns>
        /// <exception cref="ArgumentNullException">c is null.</exception>
        public static CredentialResponse FromModel(Credential c)
        {
            ArgumentNullException.ThrowIfNull(c);
            return new CredentialResponse
            {
                Id = c.Id, TenantId = c.TenantId, UserId = c.UserId, Name = c.Name, AccessKey = c.AccessKey,
                SecretKeyPreview = "secret_****" + c.SecretKeyLast4, AuthMode = c.AuthMode, Scopes = c.Scopes, ResourceIds = c.ResourceIds,
                LastUsedUtc = c.LastUsedUtc, ExpiresUtc = c.ExpiresUtc, IsProtected = c.IsProtected, Active = c.Active, CreatedUtc = c.CreatedUtc, LastUpdateUtc = c.LastUpdateUtc
            };
        }
    }
}
