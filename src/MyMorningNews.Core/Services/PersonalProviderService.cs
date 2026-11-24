namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Audio;
    using MyMorningNews.Core.Billing;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Entitlements;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Security;
    using MyMorningNews.Core.Settings;

    /// <summary>Private per-user API credentials and BYOK routing. Personal requests never fall back to platform credentials. Thread safe.</summary>
    public class PersonalProviderService
    {
        private readonly DatabaseDriverBase _Database;
        private readonly EntitlementService _Entitlements;
        private readonly Func<ServerSettings> _Settings;
        private readonly CryptoProtector _Protector;

        /// <summary>Construct with database, entitlements, live settings and the encryption root.</summary>
        public PersonalProviderService(DatabaseDriverBase database, EntitlementService entitlements, Func<ServerSettings> settings, string signingKey)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Entitlements = entitlements ?? throw new ArgumentNullException(nameof(entitlements));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Protector = new CryptoProtector(signingKey, "personal-provider-key");
        }

        /// <summary>List only the signed-in account's connections. Encrypted secrets are JsonIgnored.</summary>
        public async Task<List<ProviderConnection>> ListAsync(string tenantId, string userId, CancellationToken token = default)
        {
            return (await _Database.ProviderConnections.EnumerateAsync(new ProviderConnectionFilter { TenantId = tenantId, UserId = userId, PageSize = 10 }, token).ConfigureAwait(false)).Items;
        }

        /// <summary>Save one connection per kind. Existing credentials are retained only for an unchanged provider and URL.</summary>
        public async Task<ProviderConnection> SaveAsync(string tenantId, string userId, ProviderConnectionRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            RequireEnabled(await _Entitlements.GetEffectiveAsync(tenantId, userId, token).ConfigureAwait(false));
            string provider = request.Provider == "Codex" ? "OpenAi" : request.Provider == "Claude" ? "Anthropic" : request.Provider;
            if (request.Kind != "Chat" && request.Kind != "Embedding" && request.Kind != "Speech") throw new ArgumentException("Choose Chat, Embedding, or Speech.");
            bool local = provider == "Local" && request.Kind == "Speech";
            if (!local && provider != "Anthropic" && provider != "OpenAi" && provider != "Gemini" && provider != "XAi" && provider != "OpenAiCompatible") throw new ArgumentException("Choose a supported API provider. Server CLI sign-ins cannot fund a personal connection.");
            if (request.Kind == "Speech" && !local && provider != "OpenAi" && provider != "OpenAiCompatible") throw new ArgumentException("Speech requires a speech API key or an available local speech engine.");
            if (request.Kind == "Embedding" && provider != "OpenAi" && provider != "OpenAiCompatible" && provider != "Gemini") throw new ArgumentException("Select an embedding API provider.");
            if (local && (!_Settings().Tts.Enabled || !_Settings().Tts.AllowLocalForByok)) throw new InvalidOperationException("Local speech is not available for BYOK on this server.");
            string model = local ? _Settings().Tts.Model : request.Model.Trim();
            if (model.Length == 0 || model.Length > 256) throw new ArgumentException("Enter a model identifier of 1 to 256 characters.");
            if ((request.ApiKey?.Length ?? 0) > 8192) throw new ArgumentException("The key is too long.");
            string? endpoint = String.IsNullOrWhiteSpace(request.Endpoint) ? null : request.Endpoint.Trim().TrimEnd('/');
            if (local) endpoint = null;
            if (endpoint != null)
            {
                if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri) || (uri.Scheme != "https" && (_Settings().Hosting.RestrictPrivateNetworks || uri.Scheme != "http")) || !String.IsNullOrEmpty(uri.UserInfo) || !String.IsNullOrEmpty(uri.Query) || !String.IsNullOrEmpty(uri.Fragment))
                    throw new ArgumentException("Enter an HTTPS API base URL without credentials, query, or fragment.");
                await NetworkGuard.EnsureAllowedAsync(endpoint, token).ConfigureAwait(false);
            }
            if (provider == "OpenAiCompatible" && endpoint == null) throw new ArgumentException("This provider requires an API base URL.");
            if (request.Kind == "Speech" && !local && (String.IsNullOrWhiteSpace(request.Voice) || request.Voice.Length > 128)) throw new ArgumentException("Enter a speech voice identifier of 1 to 128 characters.");
            ProviderConnection? previous = (await ListAsync(tenantId, userId, token).ConfigureAwait(false)).FirstOrDefault(c => c.Kind == request.Kind);
            string? secret = String.IsNullOrWhiteSpace(request.ApiKey) ? null : request.ApiKey.Trim();
            if (!local && secret == null && (previous?.SecretEncrypted == null || previous.Provider != provider || previous.Endpoint != endpoint)) throw new ArgumentException("Enter your API key for this provider and URL.");
            return await _Database.ProviderConnections.SaveAsync(new ProviderConnection
            {
                TenantId = tenantId, UserId = userId, Kind = request.Kind, Provider = provider, Model = model, Endpoint = endpoint,
                Voice = local ? _Settings().Tts.Voice : request.Voice?.Trim(), SecretEncrypted = local || secret == null ? null : _Protector.Protect(secret),
                SecretLast4 = secret == null ? null : secret.Length > 4 ? secret.Substring(secret.Length - 4) : String.Empty
            }, token).ConfigureAwait(false);
        }

        /// <summary>Read a connection only for its owning account; other identifiers behave as missing.</summary>
        public async Task<ProviderConnection> ReadAsync(string tenantId, string userId, string id, CancellationToken token = default)
        {
            ProviderConnection? connection = await _Database.ProviderConnections.ReadAsync(tenantId, id, token).ConfigureAwait(false);
            if (connection == null || connection.UserId != userId || !connection.Active) throw new KeyNotFoundException("Provider connection not found.");
            return connection;
        }

        /// <summary>Delete a key. BYOK remains selected and subsequent calls fail closed until a replacement is connected.</summary>
        public async Task DeleteAsync(string tenantId, string userId, string id, CancellationToken token = default)
        {
            ProviderConnection connection = await ReadAsync(tenantId, userId, id, token).ConfigureAwait(false);
            await _Database.ProviderConnections.DeleteAsync(tenantId, connection.Id, token).ConfigureAwait(false);
        }

        /// <summary>Switch the account between its captured package and BYOK. A validated chat key is required; active paid subscriptions must first be managed through their provider.</summary>
        public async Task<EffectivePlan> SetFundingAsync(string tenantId, string userId, string mode, CancellationToken token = default)
        {
            if (mode != "Personal" && mode != "Platform") throw new ArgumentException("Choose Personal or Platform funding.");
            EffectivePlan before = await _Entitlements.GetEffectiveAsync(tenantId, userId, token).ConfigureAwait(false);
            if (before.Assignment?.Source == PlanSourceEnum.Admin) throw new InvalidOperationException("An administrator has assigned this account's plan. Ask the administrator to change that assignment.");
            if (mode == "Personal")
            {
                RequireEnabled(before);
                if (! (await ListAsync(tenantId, userId, token).ConfigureAwait(false)).Any(c => c.Kind == "Chat" && c.Active && c.ValidatedUtc.HasValue)) throw new InvalidOperationException("Connect and test a personal model key first.");
                List<BillingSubscription> paid = (await _Database.BillingSubscriptions.EnumerateAsync(new BillingSubscriptionFilter { TenantId = tenantId, UserId = userId, Active = true, PageSize = 1000 }, token).ConfigureAwait(false)).Items;
                if (paid.Any(p => BillingService.Entitled(p, DateTime.UtcNow))) throw new InvalidOperationException("Manage your existing paid subscription before switching to BYOK. You can switch when its paid period ends.");
            }
            AccountPlanPolicy? policy = (await _Database.AccountPlanPolicies.EnumerateAsync(new AccountPlanPolicyFilter { TenantId = tenantId, UserId = userId, PageSize = 1 }, token).ConfigureAwait(false)).Items.FirstOrDefault();
            if (policy == null)
            {
                await _Entitlements.CaptureSignupPolicyAsync(tenantId, userId, token).ConfigureAwait(false);
                policy = (await _Database.AccountPlanPolicies.EnumerateAsync(new AccountPlanPolicyFilter { TenantId = tenantId, UserId = userId, PageSize = 1 }, token).ConfigureAwait(false)).Items.Single();
            }
            policy.FundingMode = mode;
            await _Database.AccountPlanPolicies.UpdateAsync(policy, token).ConfigureAwait(false);
            return await _Entitlements.GetEffectiveAsync(tenantId, userId, token).ConfigureAwait(false);
        }

        /// <summary>Resolve private model calls, including explicit account-bound probes. Returns null only for platform-funded calls.</summary>
        public async Task<ResolvedModel?> ResolveModelAsync(ModelCallRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            bool explicitPersonal = request.ModelEndpointId?.StartsWith("pco_", StringComparison.Ordinal) == true;
            if (String.IsNullOrWhiteSpace(request.UserId))
            {
                if (explicitPersonal) throw new ModelCallException("Personal models require an owning account.");
                return null;
            }
            EffectivePlan plan = await _Entitlements.GetEffectiveAsync(request.TenantId, request.UserId, token).ConfigureAwait(false);
            if (plan.FundingMode != "Personal" && !explicitPersonal) return null;
            RequireEnabled(plan);
            string kind = request.Task == ModelTaskEnum.Embedding ? "Embedding" : "Chat";
            ProviderConnection? connection = explicitPersonal ? await ReadAsync(request.TenantId, request.UserId, request.ModelEndpointId!, token).ConfigureAwait(false)
                : (await ListAsync(request.TenantId, request.UserId, token).ConfigureAwait(false)).FirstOrDefault(c => c.Kind == kind && c.Active);
            if (connection == null && kind == "Embedding" && !explicitPersonal)
            {
                ModelEndpoint? local = (await _Database.ModelEndpoints.EnumerateAsync(new ModelEndpointFilter { TenantId = request.TenantId, Active = true, PageSize = 1000 }, token).ConfigureAwait(false)).Items
                    .FirstOrDefault(e => e.Provider == ModelProviderEnum.Ollama && e.SupportsEmbeddings && String.IsNullOrEmpty(e.CredentialId) && !String.IsNullOrWhiteSpace(e.EmbeddingModel));
                if (local != null) return new ResolvedModel(local, local.EmbeddingModel!, null);
            }
            if (connection == null) throw new ModelCallException("Connect your personal " + kind.ToLowerInvariant() + " API key in Account. Platform keys are not used for BYOK.");
            if (connection.Kind != kind) throw new ModelCallException("This connection does not serve the requested task.");
            if (!request.IsDryRun && !connection.ValidatedUtc.HasValue) throw new ModelCallException("Test the personal provider connection before using it.");
            ModelEndpoint endpoint = new ModelEndpoint
            {
                Id = connection.Id, TenantId = connection.TenantId, Name = "Personal " + connection.Provider, Provider = Enum.Parse<ModelProviderEnum>(connection.Provider),
                Endpoint = connection.Endpoint, CredentialId = connection.Id, Model = connection.Model, EmbeddingModel = connection.Model,
                SupportsChat = kind == "Chat", SupportsEmbeddings = kind == "Embedding", ReasoningEffort = ReasoningEffortEnum.High
            };
            return new ResolvedModel(endpoint, connection.Model, endpoint.ReasoningEffort);
        }

        /// <summary>Resolve the private key after ownership checks. Regular vault identifiers return null.</summary>
        public async Task<string?> ModelSecretAsync(ResolvedModel model, ModelCallRequest request, CancellationToken token = default)
        {
            if (!model.Endpoint.Id.StartsWith("pco_", StringComparison.Ordinal)) return null;
            ProviderConnection connection = await ReadAsync(request.TenantId, request.UserId ?? String.Empty, model.Endpoint.Id, token).ConfigureAwait(false);
            if (connection.Endpoint != model.Endpoint.Endpoint || connection.Provider != model.Endpoint.Provider.ToString() || connection.Model != model.Model)
                throw new ModelCallException("The personal connection changed while this call was starting. Retry with the updated connection.");
            return Secret(connection);
        }

        /// <summary>Resolve speech using the account's speech key or explicitly shared local engine. Returns null for platform-funded accounts.</summary>
        public async Task<TtsRequest?> SpeechAsync(string tenantId, string? userId, string voice, string text, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(userId)) return null;
            EffectivePlan plan = await _Entitlements.GetEffectiveAsync(tenantId, userId, token).ConfigureAwait(false);
            if (plan.FundingMode != "Personal") return null;
            RequireEnabled(plan);
            ProviderConnection? connection = (await ListAsync(tenantId, userId, token).ConfigureAwait(false)).FirstOrDefault(c => c.Kind == "Speech" && c.Active);
            return SpeechRequest(connection, voice, text);
        }

        /// <summary>Resolve a fixed-text speech test before BYOK activation. Ownership is required; only this connection or an explicitly enabled local engine is used.</summary>
        public async Task<TtsRequest> SpeechProbeAsync(string tenantId, string userId, string connectionId, CancellationToken token = default)
        {
            RequireEnabled(await _Entitlements.GetEffectiveAsync(tenantId, userId, token).ConfigureAwait(false));
            ProviderConnection connection = await ReadAsync(tenantId, userId, connectionId, token).ConfigureAwait(false);
            if (connection.Kind != "Speech") throw new ArgumentException("Select a speech connection to test.");
            return SpeechRequest(connection, connection.Voice ?? String.Empty, "Your private speech connection is ready.");
        }

        private TtsRequest SpeechRequest(ProviderConnection? connection, string voice, string text)
        {
            if (connection == null || connection.Provider == "Local")
            {
                if (!_Settings().Tts.Enabled || !_Settings().Tts.AllowLocalForByok) throw new InvalidOperationException("BYOK audio needs your speech API key or an available local speech engine.");
                return new TtsRequest { Endpoint = _Settings().Tts.Endpoint, Model = _Settings().Tts.Model, Voice = _Settings().Tts.Voice, Text = text, Format = "wav", TimeoutSeconds = _Settings().Tts.TimeoutSeconds };
            }
            return new TtsRequest { Endpoint = connection.Endpoint ?? "https://api.openai.com/v1", ApiKey = Secret(connection), Model = connection.Model, Voice = connection.Voice ?? voice, Text = text, Format = "wav", TimeoutSeconds = _Settings().Tts.TimeoutSeconds };
        }

        private static void RequireEnabled(EffectivePlan plan)
        {
            if (!EntitlementService.Allowed(plan, "models.personalConnections.enabled")) throw new CapReachedException("models.personalConnections.enabled", "false", null, "Personal API connections are disabled for this account. Contact your administrator.");
        }

        private string Secret(ProviderConnection connection)
        {
            return _Protector.Unprotect(connection.SecretEncrypted ?? String.Empty) ?? throw new ModelCallException("The personal API key cannot be decrypted. Reconnect the provider.");
        }
    }
}
