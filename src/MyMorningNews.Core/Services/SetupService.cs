namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Search;
    using MyMorningNews.Core.Settings;
    using SyslogLogging;

    /// <summary>
    /// Works out whether a workspace has what it needs (a chat model with a working key, and web search) and proves it
    /// with live calls. The dashboard and mobile app keep people out of the product until every required check is ready.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class SetupService : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Chat tasks a workspace needs a working endpoint for.
        /// </summary>
        public static readonly IReadOnlyList<ModelTaskEnum> ChatTasks = Enum.GetValues<ModelTaskEnum>().Where(t => t != ModelTaskEnum.Embedding).ToList();

        /// <summary>
        /// Minimum time between automatic retries of a failed platform key or SearXNG check, in seconds. Default 120,
        /// minimum 10, maximum 86400.
        /// </summary>
        public int RetryAfterSeconds
        {
            get { return _RetryAfterSeconds; }
            set { _RetryAfterSeconds = Math.Clamp(value, 10, 86400); }
        }

        #endregion

        #region Private-Members

        private readonly string _Header = "[SetupService] ";
        private readonly DatabaseDriverBase _Database;
        private readonly ModelGateway _Models;
        private readonly SearchService _Search;
        private readonly VaultService _Vault;
        private readonly Func<ServerSettings> _Settings;
        private readonly LoggingModule _Logging;
        private readonly SemaphoreSlim _PlatformLock = new SemaphoreSlim(1, 1);
        private int _RetryAfterSeconds = 120;
        private DateTime? _PlatformVerifiedUtc = null;
        private DateTime? _PlatformFailedUtc = null;
        private string? _PlatformFailure = null;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="models">Model gateway.</param>
        /// <param name="search">Search service.</param>
        /// <param name="vault">Vault.</param>
        /// <param name="settings">Current server settings.</param>
        /// <param name="logging">Logging.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public SetupService(DatabaseDriverBase database, ModelGateway models, SearchService search, VaultService vault, Func<ServerSettings> settings, LoggingModule logging)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Models = models ?? throw new ArgumentNullException(nameof(models));
            _Search = search ?? throw new ArgumentNullException(nameof(search));
            _Vault = vault ?? throw new ArgumentNullException(nameof(vault));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Compute the workspace's setup status from stored results. The only live calls are a one-time check of the
        /// hosted service's own key and a SearXNG test query when SearXNG has not been tested yet; both are free to the user.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="canConfigure">Whether the caller is an administrator; optional checks are included only for administrators.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Status.</returns>
        /// <exception cref="ArgumentNullException">tenantId is null or empty.</exception>
        public async Task<SetupStatusResponse> GetStatusAsync(string tenantId, bool canConfigure, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            return await BuildAsync(tenantId, canConfigure, false, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Re-check everything with live calls: each chat endpoint gets a short probe prompt, each default web search
        /// provider gets a test query, and the embedding endpoint is asked for one vector.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Status.</returns>
        /// <exception cref="ArgumentNullException">tenantId is null or empty.</exception>
        public async Task<SetupStatusResponse> VerifyAsync(string tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            return await BuildAsync(tenantId, true, true, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Store an API key for an endpoint (rotating the endpoint's own credential when it has one), then verify the
        /// endpoint with a live call.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">Administrator storing the key, when known.</param>
        /// <param name="req">Request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Status after the check.</returns>
        /// <exception cref="ArgumentNullException">tenantId or req is null.</exception>
        /// <exception cref="ArgumentException">The endpoint ID or key is missing, or the endpoint uses the platform key.</exception>
        /// <exception cref="KeyNotFoundException">The endpoint does not exist.</exception>
        public async Task<SetupStatusResponse> SetModelKeyAsync(string tenantId, string? userId, SetupModelKeyRequest req, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            ArgumentNullException.ThrowIfNull(req);
            if (String.IsNullOrWhiteSpace(req.ModelEndpointId)) throw new ArgumentException("modelEndpointId is required.");
            string key = (req.ApiKey ?? String.Empty).Trim();
            if (key.Length == 0) throw new ArgumentException("apiKey is required.");
            ModelEndpoint? endpoint = await _Database.ModelEndpoints.ReadAsync(tenantId, req.ModelEndpointId, token).ConfigureAwait(false);
            if (endpoint == null) throw new KeyNotFoundException("Model endpoint " + req.ModelEndpointId + " was not found.");
            if (IsPlatform(endpoint)) throw new ArgumentException("This endpoint uses the hosted service's key, so there is nothing to enter.");

            VaultCredential? existing = null;
            if (!String.IsNullOrEmpty(endpoint.CredentialId))
                existing = await _Database.VaultCredentials.ReadAsync(tenantId, endpoint.CredentialId, token).ConfigureAwait(false);
            if (existing != null)
            {
                await _Vault.UpdateAsync(existing, new VaultCredentialUpsertRequest { Secret = key, Active = true }, token).ConfigureAwait(false);
            }
            else
            {
                VaultCredential created = await _Vault.CreateAsync(tenantId, userId, new VaultCredentialUpsertRequest
                {
                    Name = await FreeCredentialNameAsync(tenantId, endpoint.Name + " API key", token).ConfigureAwait(false),
                    Kind = VaultCredentialKindEnum.ApiKey,
                    Provider = VaultProviderFor(endpoint.Provider),
                    Secret = key,
                    Description = "Added on the Setup page."
                }, token).ConfigureAwait(false);
                endpoint.CredentialId = created.Id;
            }

            endpoint.LastValidationSucceeded = null;
            endpoint = await _Database.ModelEndpoints.UpdateAsync(endpoint, token).ConfigureAwait(false);
            await _Models.ValidateAsync(endpoint, token).ConfigureAwait(false);
            _Logging.Info(_Header + "stored a key for endpoint " + endpoint.Id + " in tenant " + tenantId);
            return await BuildAsync(tenantId, true, false, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Refuse model work when the endpoint for a task has no key or failed its last check. An endpoint that has a key
        /// but has not been checked yet is allowed, because a successful call also proves it works.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="task">Task about to run.</param>
        /// <param name="topicId">Topic, when the work belongs to one (its own model assignment wins).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">tenantId is null or empty.</exception>
        /// <exception cref="SetupRequiredException">The task's endpoint is missing, has no key, or failed its last check.</exception>
        public async Task RequireAsync(string tenantId, ModelTaskEnum task, string? topicId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            ResolvedModel? resolved = null;
            try
            {
                resolved = await _Models.ResolveAsync(tenantId, task, topicId, null, null, token).ConfigureAwait(false);
            }
            catch (ModelCallException ex)
            {
                throw new SetupRequiredException("model:none", ex.Message + " An administrator can finish this on the Setup page.");
            }

            SetupCheck check = await ModelCheckAsync(tenantId, resolved.Endpoint, resolved.Model, false, token).ConfigureAwait(false);
            if (check.State == SetupCheckStateEnum.Missing || check.State == SetupCheckStateEnum.Failed)
                throw new SetupRequiredException(check.Key, Explain(check));
        }

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        /// <param name="disposing">Disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing) _PlatformLock.Dispose();
            _Disposed = true;
        }

        private async Task<SetupStatusResponse> BuildAsync(string tenantId, bool canConfigure, bool live, CancellationToken token)
        {
            SetupStatusResponse status = new SetupStatusResponse { CanConfigure = canConfigure };

            Dictionary<string, ResolvedModel> byEndpoint = new Dictionary<string, ResolvedModel>(StringComparer.Ordinal);
            Dictionary<string, List<ModelTaskEnum>> tasks = new Dictionary<string, List<ModelTaskEnum>>(StringComparer.Ordinal);
            List<ModelTaskEnum> unserved = new List<ModelTaskEnum>();
            string? unservedMessage = null;
            foreach (ModelTaskEnum task in ChatTasks)
            {
                try
                {
                    ResolvedModel resolved = await _Models.ResolveAsync(tenantId, task, null, null, null, token).ConfigureAwait(false);
                    string id = resolved.Endpoint.Id;
                    if (!byEndpoint.ContainsKey(id))
                    {
                        byEndpoint[id] = resolved;
                        tasks[id] = new List<ModelTaskEnum>();
                    }

                    tasks[id].Add(task);
                }
                catch (ModelCallException ex)
                {
                    unserved.Add(task);
                    unservedMessage ??= ex.Message;
                }
            }

            if (unserved.Count > 0)
            {
                status.Checks.Add(new SetupCheck
                {
                    Key = "model:none",
                    Kind = SetupCheckKindEnum.Model,
                    Required = true,
                    State = SetupCheckStateEnum.Missing,
                    Message = unservedMessage,
                    Tasks = unserved
                });
            }

            foreach (KeyValuePair<string, ResolvedModel> pair in byEndpoint)
            {
                SetupCheck check = await ModelCheckAsync(tenantId, pair.Value.Endpoint, pair.Value.Model, live, token).ConfigureAwait(false);
                check.Tasks = tasks[pair.Key];
                status.Checks.Add(check);
            }

            status.Checks.Add(await SearchCheckAsync(tenantId, live, token).ConfigureAwait(false));

            if (canConfigure)
            {
                status.Checks.Add(await EmbeddingCheckAsync(tenantId, live, token).ConfigureAwait(false));
                status.Checks.Add(EmailCheck());
            }

            status.Ready = status.Checks.Where(c => c.Required).All(c => c.State == SetupCheckStateEnum.Ready);
            status.CheckedUtc = DateTime.UtcNow;
            return status;
        }

        private async Task<SetupCheck> ModelCheckAsync(string tenantId, ModelEndpoint endpoint, string model, bool live, CancellationToken token)
        {
            SetupCheck check = new SetupCheck
            {
                Key = "model:" + endpoint.Id,
                Kind = SetupCheckKindEnum.Model,
                Required = true,
                ResourceId = endpoint.Id,
                ResourceName = endpoint.Name,
                Provider = endpoint.Provider.ToString(),
                Model = model,
                NeedsApiKey = NeedsKey(endpoint.Provider),
                UsesPlatformKey = IsPlatform(endpoint),
                VerifiedUtc = endpoint.LastValidatedUtc
            };

            check.HasApiKey = await HasKeyAsync(tenantId, endpoint, token).ConfigureAwait(false);
            if (check.NeedsApiKey && !check.HasApiKey)
            {
                check.State = SetupCheckStateEnum.Missing;
                check.Message = check.UsesPlatformKey
                    ? "The hosted service's model key is not configured. The operator needs to set it."
                    : endpoint.Name + " needs an API key.";
                return check;
            }

            if (endpoint.Provider == ModelProviderEnum.OpenAiCompatible && String.IsNullOrWhiteSpace(endpoint.Endpoint))
            {
                check.State = SetupCheckStateEnum.Missing;
                check.Message = endpoint.Name + " needs a base URL.";
                return check;
            }

            if (live)
            {
                await _Models.ValidateAsync(endpoint, token).ConfigureAwait(false);
                if (check.UsesPlatformKey) RememberPlatform(endpoint.LastValidationSucceeded == true, endpoint.LastValidationResult);
            }
            else if (check.UsesPlatformKey && endpoint.LastValidationSucceeded != true)
            {
                await VerifyPlatformEndpointAsync(endpoint, token).ConfigureAwait(false);
            }

            check.VerifiedUtc = endpoint.LastValidatedUtc;
            if (endpoint.LastValidationSucceeded == true)
            {
                check.State = SetupCheckStateEnum.Ready;
            }
            else if (endpoint.LastValidationSucceeded == false)
            {
                check.State = SetupCheckStateEnum.Failed;
                check.Message = String.IsNullOrWhiteSpace(endpoint.LastValidationResult) ? endpoint.Name + " did not respond to a test prompt." : endpoint.LastValidationResult;
            }
            else
            {
                check.State = SetupCheckStateEnum.Unverified;
            }

            return check;
        }

        private async Task VerifyPlatformEndpointAsync(ModelEndpoint endpoint, CancellationToken token)
        {
            await _PlatformLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (_PlatformVerifiedUtc.HasValue)
                {
                    endpoint.LastValidationSucceeded = true;
                    endpoint.LastValidatedUtc = DateTime.UtcNow;
                    endpoint.LastValidationResult = "The hosted service's key was verified.";
                    await _Database.ModelEndpoints.UpdateAsync(endpoint, token).ConfigureAwait(false);
                    return;
                }

                if (_PlatformFailedUtc.HasValue && DateTime.UtcNow - _PlatformFailedUtc.Value < TimeSpan.FromSeconds(_RetryAfterSeconds))
                {
                    endpoint.LastValidationSucceeded = false;
                    endpoint.LastValidationResult = _PlatformFailure;
                    return;
                }

                await _Models.ValidateAsync(endpoint, token).ConfigureAwait(false);
                RememberPlatform(endpoint.LastValidationSucceeded == true, endpoint.LastValidationResult);
            }
            finally
            {
                _PlatformLock.Release();
            }
        }

        private void RememberPlatform(bool succeeded, string? message)
        {
            if (succeeded)
            {
                _PlatformVerifiedUtc = DateTime.UtcNow;
                _PlatformFailedUtc = null;
                _PlatformFailure = null;
            }
            else
            {
                _PlatformVerifiedUtc = null;
                _PlatformFailedUtc = DateTime.UtcNow;
                _PlatformFailure = message;
                _Logging.Warn(_Header + "the platform model key failed its check: " + message);
            }
        }

        private async Task<SetupCheck> SearchCheckAsync(string tenantId, bool live, CancellationToken token)
        {
            SetupCheck check = new SetupCheck { Key = "search", Kind = SetupCheckKindEnum.Search, Required = true };
            await _Search.EnsureDefaultsAsync(tenantId, token).ConfigureAwait(false);
            List<SearchProvider> providers = (await _Database.SearchProviders.EnumerateAsync(new SearchProviderFilter { TenantId = tenantId, PageSize = 100 }, token).ConfigureAwait(false))
                .Items.Where(p => p.Active && p.IsDefault && IsWebSearch(p.Kind)).ToList();
            if (providers.Count == 0)
            {
                check.State = SetupCheckStateEnum.Missing;
                check.Message = "No web search provider is turned on. Turn on SearXNG, or add Tavily or You.com, under Search providers.";
                return check;
            }

            List<string> failures = new List<string>();
            foreach (SearchProvider p in providers)
            {
                bool due = p.LastTestOk != true && (!p.LastTestedUtc.HasValue || DateTime.UtcNow - p.LastTestedUtc.Value >= TimeSpan.FromSeconds(_RetryAfterSeconds));
                bool needsKey = p.Kind == SearchProviderKindEnum.Tavily || p.Kind == SearchProviderKindEnum.YouCom;
                if (needsKey && String.IsNullOrEmpty(p.CredentialId))
                {
                    failures.Add(p.Name + " needs an API key.");
                    continue;
                }

                if (live || (due && p.Kind == SearchProviderKindEnum.Searxng))
                {
                    SearchOutcome outcome = await _Search.SearchAsync(p, new ResearchSearchRequest { Query = "weather forecast", MaxResults = 3 }, token).ConfigureAwait(false);
                    p.LastTestedUtc = DateTime.UtcNow;
                    p.LastTestOk = outcome.Success;
                    await _Database.SearchProviders.UpdateAsync(p, token).ConfigureAwait(false);
                    if (!outcome.Success) failures.Add(p.Name + ": " + (String.IsNullOrWhiteSpace(outcome.Error) ? "the test search failed." : outcome.Error));
                }
                else if (p.LastTestOk == false)
                {
                    failures.Add(p.Name + ": the last test search failed.");
                }

                if (p.LastTestOk == true)
                {
                    check.State = SetupCheckStateEnum.Ready;
                    check.ResourceId = p.Id;
                    check.ResourceName = p.Name;
                    check.Provider = p.Kind.ToString();
                    check.VerifiedUtc = p.LastTestedUtc;
                    check.HasApiKey = !String.IsNullOrEmpty(p.CredentialId);
                    check.NeedsApiKey = needsKey;
                    check.Message = null;
                    return check;
                }
            }

            SearchProvider first = providers[0];
            check.ResourceId = first.Id;
            check.ResourceName = first.Name;
            check.Provider = first.Kind.ToString();
            check.VerifiedUtc = first.LastTestedUtc;
            check.State = failures.Count > 0 ? SetupCheckStateEnum.Failed : SetupCheckStateEnum.Unverified;
            check.Message = failures.Count > 0 ? String.Join(" ", failures) : null;
            return check;
        }

        private async Task<SetupCheck> EmbeddingCheckAsync(string tenantId, bool live, CancellationToken token)
        {
            SetupCheck check = new SetupCheck { Key = "embeddings", Kind = SetupCheckKindEnum.Embeddings, Required = false };
            ResolvedModel resolved;
            try
            {
                resolved = await _Models.ResolveAsync(tenantId, ModelTaskEnum.Embedding, null, null, null, token).ConfigureAwait(false);
            }
            catch (ModelCallException)
            {
                check.State = SetupCheckStateEnum.Missing;
                check.Message = "No embedding endpoint is set up. Research still runs, and duplicates are found by comparing text.";
                return check;
            }

            ModelEndpoint e = resolved.Endpoint;
            check.ResourceId = e.Id;
            check.ResourceName = e.Name;
            check.Provider = e.Provider.ToString();
            check.Model = resolved.Model;
            check.NeedsApiKey = NeedsKey(e.Provider);
            check.HasApiKey = await HasKeyAsync(tenantId, e, token).ConfigureAwait(false);
            if (live) await _Models.ValidateAsync(e, token).ConfigureAwait(false);
            check.VerifiedUtc = e.LastValidatedUtc;
            check.State = e.LastValidationSucceeded == true ? SetupCheckStateEnum.Ready
                : e.LastValidationSucceeded == false ? SetupCheckStateEnum.Failed
                : SetupCheckStateEnum.Unverified;
            if (check.State == SetupCheckStateEnum.Failed)
                check.Message = (e.LastValidationResult ?? e.Name + " did not return a vector.") + " Research still runs, and duplicates are found by comparing text.";
            return check;
        }

        private SetupCheck EmailCheck()
        {
            EmailSettings email = _Settings().Email;
            SetupCheck check = new SetupCheck { Key = "email", Kind = SetupCheckKindEnum.Email, Required = false, Provider = email.Provider.ToString() };
            if (!email.Enabled || email.Provider == EmailProviderEnum.Disabled)
            {
                check.State = SetupCheckStateEnum.Missing;
                check.Message = "Email is turned off, so briefings are only shown in the app.";
            }
            else if (email.Provider == EmailProviderEnum.File)
            {
                check.State = SetupCheckStateEnum.Missing;
                check.Message = "Emails are saved as files in " + email.FileDirectory + " on the server instead of being sent. Set the Mailgun values in docker/.env to deliver them.";
            }
            else if (String.IsNullOrWhiteSpace(email.MailgunApiKey) || String.IsNullOrWhiteSpace(email.MailgunDomain))
            {
                check.State = SetupCheckStateEnum.Missing;
                check.Message = "Mailgun is selected but its domain or API key is not set.";
            }
            else
            {
                check.State = SetupCheckStateEnum.Ready;
            }

            return check;
        }

        private async Task<bool> HasKeyAsync(string tenantId, ModelEndpoint endpoint, CancellationToken token)
        {
            if (String.IsNullOrEmpty(endpoint.CredentialId)) return false;
            try
            {
                string? key = await _Vault.ResolveAsync(tenantId, endpoint.CredentialId, token).ConfigureAwait(false);
                return !String.IsNullOrWhiteSpace(key);
            }
            catch (VaultSecretUnavailableException)
            {
                return false;
            }
        }

        private async Task<string> FreeCredentialNameAsync(string tenantId, string name, CancellationToken token)
        {
            string candidate = name.Length > 100 ? name.Substring(0, 100) : name;
            for (int i = 2; i < 100; i++)
            {
                EnumerationResult<VaultCredential> found = await _Database.VaultCredentials.EnumerateAsync(new VaultCredentialFilter { TenantId = tenantId, Name = candidate, PageSize = 1 }, token).ConfigureAwait(false);
                if (!found.Items.Any(v => String.Equals(v.Name, candidate, StringComparison.OrdinalIgnoreCase))) return candidate;
                candidate = (name.Length > 100 ? name.Substring(0, 100) : name) + " " + i;
            }

            return name + " " + Guid.NewGuid().ToString("N").Substring(0, 6);
        }

        private static string Explain(SetupCheck check)
        {
            string what = check.ResourceName ?? "The model endpoint";
            string detail = String.IsNullOrWhiteSpace(check.Message) ? what + " is not ready." : check.Message;
            if (check.State == SetupCheckStateEnum.Failed) detail = what + " failed its last check: " + detail;
            return detail + " An administrator can finish this on the Setup page.";
        }

        private static bool IsPlatform(ModelEndpoint endpoint)
        {
            return String.Equals(endpoint.CredentialId, VaultService.PlatformAnthropicCredentialId, StringComparison.Ordinal);
        }

        private static bool NeedsKey(ModelProviderEnum provider)
        {
            return provider == ModelProviderEnum.Anthropic
                || provider == ModelProviderEnum.OpenAi
                || provider == ModelProviderEnum.Gemini
                || provider == ModelProviderEnum.XAi
                || provider == ModelProviderEnum.OllamaCloud;
        }

        private static bool IsWebSearch(SearchProviderKindEnum kind)
        {
            return kind == SearchProviderKindEnum.Searxng
                || kind == SearchProviderKindEnum.Tavily
                || kind == SearchProviderKindEnum.YouCom
                || kind == SearchProviderKindEnum.HostedModelWebSearch;
        }

        private static VaultProviderEnum VaultProviderFor(ModelProviderEnum provider)
        {
            switch (provider)
            {
                case ModelProviderEnum.Anthropic: return VaultProviderEnum.Anthropic;
                case ModelProviderEnum.OpenAi: return VaultProviderEnum.OpenAi;
                case ModelProviderEnum.Gemini: return VaultProviderEnum.Gemini;
                case ModelProviderEnum.XAi: return VaultProviderEnum.XAi;
                case ModelProviderEnum.OllamaCloud: return VaultProviderEnum.OllamaCloud;
                case ModelProviderEnum.OpenAiCompatible: return VaultProviderEnum.OpenAiCompatible;
                default: return VaultProviderEnum.Custom;
            }
        }

        #endregion
    }
}
