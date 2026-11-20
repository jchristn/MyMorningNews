namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;

    /// <summary>
    /// Tenant credential vault. Secrets are AES-256-GCM encrypted with the vault-secret key purpose; only the last four
    /// characters are returned by ordinary reads.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class VaultService
    {
        /// <summary>
        /// Credential reference for the operator's Anthropic key on the hosted service. It is resolved from the server
        /// environment at call time, never stored in a tenant vault, and cannot be read or revealed by tenants.
        /// </summary>
        public const string PlatformAnthropicCredentialId = "platform:anthropic";

        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly CryptoProtector _Protector;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="signingKey">Root key material.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public VaultService(DatabaseDriverBase database, string signingKey)
        {
            ArgumentNullException.ThrowIfNull(database);
            if (String.IsNullOrEmpty(signingKey)) throw new ArgumentNullException(nameof(signingKey));
            _Database = database;
            _Protector = new CryptoProtector(signingKey, "vault-secret");
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create a credential.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">Creator, when known.</param>
        /// <param name="req">Request; Name and Secret are required.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created credential.</returns>
        /// <exception cref="ArgumentException">Name or secret is missing.</exception>
        public async Task<VaultCredential> CreateAsync(string tenantId, string? userId, VaultCredentialUpsertRequest req, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(req);
            if (String.IsNullOrWhiteSpace(req.Name)) throw new ArgumentException("Name is required.", nameof(req));
            if (String.IsNullOrEmpty(req.Secret)) throw new ArgumentException("Secret is required.", nameof(req));
            VaultCredential v = new VaultCredential
            {
                TenantId = tenantId,
                Name = req.Name.Trim(),
                Kind = req.Kind ?? Enums.VaultCredentialKindEnum.ApiKey,
                Provider = req.Provider ?? Enums.VaultProviderEnum.Custom,
                HeaderName = req.HeaderName,
                Username = req.Username,
                SecretEncrypted = _Protector.Protect(req.Secret),
                SecretLast4 = Last4(req.Secret),
                Description = req.Description,
                CreatedByUserId = userId,
                ExpiresUtc = req.ExpiresUtc,
                Active = req.Active ?? true
            };
            return await _Database.VaultCredentials.CreateAsync(v, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Update a credential; a supplied secret rotates the stored value.
        /// </summary>
        /// <param name="credential">Existing credential.</param>
        /// <param name="req">Request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated credential.</returns>
        public async Task<VaultCredential> UpdateAsync(VaultCredential credential, VaultCredentialUpsertRequest req, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(credential);
            ArgumentNullException.ThrowIfNull(req);
            if (!String.IsNullOrWhiteSpace(req.Name)) credential.Name = req.Name.Trim();
            if (req.Kind.HasValue) credential.Kind = req.Kind.Value;
            if (req.Provider.HasValue) credential.Provider = req.Provider.Value;
            if (req.HeaderName != null) credential.HeaderName = req.HeaderName;
            if (req.Username != null) credential.Username = req.Username;
            if (req.Description != null) credential.Description = req.Description;
            if (req.ExpiresUtc.HasValue) credential.ExpiresUtc = req.ExpiresUtc;
            if (req.Active.HasValue) credential.Active = req.Active.Value;
            if (!String.IsNullOrEmpty(req.Secret))
            {
                credential.SecretEncrypted = _Protector.Protect(req.Secret);
                credential.SecretLast4 = Last4(req.Secret);
            }

            return await _Database.VaultCredentials.UpdateAsync(credential, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Decrypt a credential for the reveal action and record the reveal time.
        /// </summary>
        /// <param name="credential">Credential.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Plaintext.</returns>
        /// <exception cref="VaultSecretUnavailableException">The value cannot be decrypted.</exception>
        public async Task<string> RevealAsync(VaultCredential credential, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(credential);
            string plain = _Protector.Unprotect(credential.SecretEncrypted)
                ?? throw new VaultSecretUnavailableException("The stored value for " + credential.Name + " cannot be decrypted. It may have been saved with a different signing key; rotate it.");
            credential.LastRevealedUtc = DateTime.UtcNow;
            await _Database.VaultCredentials.UpdateAsync(credential, token).ConfigureAwait(false);
            return plain;
        }

        /// <summary>
        /// Resolve a secret for server-side use (model calls, search providers). Updates last-used at most once a minute.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="credentialId">Credential, or null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Plaintext, or null when no credential is referenced.</returns>
        /// <exception cref="VaultSecretUnavailableException">The credential is missing, inactive, expired, or undecryptable.</exception>
        public async Task<string?> ResolveAsync(string tenantId, string? credentialId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(credentialId)) return null;
            if (String.Equals(credentialId, PlatformAnthropicCredentialId, StringComparison.Ordinal))
            {
                string? platformKey = Environment.GetEnvironmentVariable(Constants.EnvironmentPrefix + "ANTHROPIC_API_KEY");
                if (String.IsNullOrWhiteSpace(platformKey)) throw new VaultSecretUnavailableException("The platform model key is not configured on this server.");
                return platformKey.Trim();
            }

            VaultCredential? v = await _Database.VaultCredentials.ReadAsync(tenantId, credentialId, token).ConfigureAwait(false);
            if (v == null) throw new VaultSecretUnavailableException("Vault credential " + credentialId + " was not found.");
            if (!v.Active) throw new VaultSecretUnavailableException("Vault credential " + v.Name + " is inactive.");
            if (v.ExpiresUtc.HasValue && v.ExpiresUtc.Value < DateTime.UtcNow) throw new VaultSecretUnavailableException("Vault credential " + v.Name + " has expired.");
            string plain = _Protector.Unprotect(v.SecretEncrypted)
                ?? throw new VaultSecretUnavailableException("Vault credential " + v.Name + " cannot be decrypted; rotate it.");
            if (!v.LastUsedUtc.HasValue || (DateTime.UtcNow - v.LastUsedUtc.Value).TotalSeconds > 60)
            {
                v.LastUsedUtc = DateTime.UtcNow;
                await _Database.VaultCredentials.UpdateAsync(v, token).ConfigureAwait(false);
            }

            return plain;
        }

        /// <summary>
        /// Records that reference a credential.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="credentialId">Credential.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Referencing records.</returns>
        public async Task<List<VaultUsageItem>> WhereUsedAsync(string tenantId, string credentialId, CancellationToken token = default)
        {
            List<VaultUsageItem> items = new List<VaultUsageItem>();
            EnumerationResult<ModelEndpoint> endpoints = await _Database.ModelEndpoints.EnumerateAsync(new ModelEndpointFilter { TenantId = tenantId, CredentialId = credentialId, PageSize = 1000 }, token).ConfigureAwait(false);
            foreach (ModelEndpoint e in endpoints.Items) items.Add(new VaultUsageItem { ResourceType = "ModelEndpoint", ResourceId = e.Id, Name = e.Name });
            return items;
        }

        #endregion

        #region Private-Methods

        private static string Last4(string secret)
        {
            return secret.Length <= 4 ? String.Empty : secret.Substring(secret.Length - 4);
        }

        #endregion
    }
}
