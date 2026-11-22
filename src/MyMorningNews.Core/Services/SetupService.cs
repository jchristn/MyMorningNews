namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Audio;
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

        /// <summary>
        /// Audio service used to check the speech endpoint. Null leaves the speech check out of the status.
        /// </summary>
        public AudioService? Audio { get; set; } = null;

        /// <summary>
        /// Seconds the result of the speech endpoint check is kept, so reading the status stays fast. A re-check with
        /// live calls ignores it. Default 300, minimum 0 (never kept), maximum 86400.
        /// </summary>
        public int SpeechCacheSeconds
        {
            get { return _SpeechCacheSeconds; }
            set { _SpeechCacheSeconds = Math.Clamp(value, 0, 86400); }
        }

        /// <summary>
        /// Seconds to wait for the speech endpoint to answer the check. Default 8, minimum 1, maximum 120.
        /// </summary>
        public int SpeechProbeTimeoutSeconds
        {
            get { return _SpeechProbeTimeoutSeconds; }
            set { _SpeechProbeTimeoutSeconds = Math.Clamp(value, 1, 120); }
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
        private readonly object _SpeechLock = new object();
        private int _RetryAfterSeconds = 120;
        private int _SpeechCacheSeconds = 300;
        private int _SpeechProbeTimeoutSeconds = 8;
        private string? _SpeechKey = null;
        private string? _SpeechFailure = null;
        private DateTime _SpeechCheckedUtc = DateTime.MinValue;
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
        /// hosted service's own key, a SearXNG test query when SearXNG has not been tested yet, and (for administrators)
        /// a one word speech request when the last one is older than <see cref="SpeechCacheSeconds"/>; all are free to the user.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="canConfigure">Whether the caller is an administrator; optional checks are included only for administrators.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Status.</returns>
        /// <exception cref="ArgumentNullException">tenantId is null or empty.</exception>
        public async Task<SetupStatusResponse> GetStatusAsync(string tenantId, bool canConfigure, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            return await BuildAsync(tenantId, canConfigure, false, null, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Re-check everything with live calls: each chat endpoint gets a short probe prompt, each default web search
        /// provider gets a test query, the embedding endpoint is asked for one vector, and the speech endpoint speaks one word.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Status.</returns>
        /// <exception cref="ArgumentNullException">tenantId is null or empty.</exception>
        public async Task<SetupStatusResponse> VerifyAsync(string tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            return await BuildAsync(tenantId, true, true, null, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Re-check one kind of check with live calls (for example only the speech endpoint) and report every check;
        /// the other checks come from stored results.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="only">The kind to re-check; null re-checks everything.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Status.</returns>
        /// <exception cref="ArgumentNullException">tenantId is null or empty.</exception>
        public async Task<SetupStatusResponse> VerifyAsync(string tenantId, SetupCheckKindEnum? only, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            return await BuildAsync(tenantId, true, true, only, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Model providers the Setup page can connect on this server, with their sign-in methods.
        /// </summary>
        /// <param name="globalAdmin">Whether the caller is a global administrator (agent CLIs may be limited to them).</param>
        /// <returns>Options.</returns>
        public SetupOptionsResponse GetOptions(bool globalAdmin = false)
        {
            SetupOptionsResponse options = new SetupOptionsResponse();
            string chat = _Settings().Models.DefaultChatModel;
            options.Providers.Add(Option(ModelProviderEnum.Anthropic, SetupProviderGroupEnum.Api, globalAdmin, true, chat, false, null));
            options.Providers.Add(Option(ModelProviderEnum.OpenAi, SetupProviderGroupEnum.Api, globalAdmin, false, null, false, null));
            options.Providers.Add(Option(ModelProviderEnum.Gemini, SetupProviderGroupEnum.Api, globalAdmin, false, null, false, null));
            options.Providers.Add(Option(ModelProviderEnum.XAi, SetupProviderGroupEnum.Api, globalAdmin, false, null, false, null));
            options.Providers.Add(Option(ModelProviderEnum.OllamaCloud, SetupProviderGroupEnum.Api, globalAdmin, false, null, false, null));
            options.Providers.Add(Option(ModelProviderEnum.ClaudeCode, SetupProviderGroupEnum.AgentCli, globalAdmin, false, chat, false, null));
            options.Providers.Add(Option(ModelProviderEnum.Codex, SetupProviderGroupEnum.AgentCli, globalAdmin, false, AgentCliService.DefaultModel, false, null));
            options.Providers.Add(Option(ModelProviderEnum.GeminiCli, SetupProviderGroupEnum.AgentCli, globalAdmin, false, AgentCliService.DefaultModel, false, null));
            options.Providers.Add(Option(ModelProviderEnum.Mux, SetupProviderGroupEnum.AgentCli, globalAdmin, false, AgentCliService.DefaultModel, false, null));
            options.Providers.Add(Option(ModelProviderEnum.Ollama, SetupProviderGroupEnum.SelfHosted, globalAdmin, false, null, true, _Settings().Models.DefaultOllamaEndpoint));
            options.Providers.Add(Option(ModelProviderEnum.OpenAiCompatible, SetupProviderGroupEnum.SelfHosted, globalAdmin, false, null, true, null));
            return options;
        }

        /// <summary>
        /// Connect the workspace's chat model in one step: store the secret for the chosen sign-in method (rotating the
        /// endpoint's own credential when it has one), point every chat task at the endpoint, and verify it with a live
        /// call. The returned status reports the result on the endpoint's model check.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">Administrator, when known.</param>
        /// <param name="req">Request.</param>
        /// <param name="globalAdmin">Whether the caller is a global administrator (agent CLIs may be limited to them).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Status after the check.</returns>
        /// <exception cref="ArgumentNullException">tenantId or req is null.</exception>
        /// <exception cref="ArgumentException">The request is incomplete, the provider is unavailable, or the secret does not look right.</exception>
        /// <exception cref="KeyNotFoundException">The named endpoint does not exist.</exception>
        public async Task<SetupStatusResponse> ConnectAsync(string tenantId, string? userId, SetupConnectRequest req, bool globalAdmin = false, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            ArgumentNullException.ThrowIfNull(req);
            if (!req.Provider.HasValue) throw new FieldValidationException("provider", "Choose a provider.");
            ModelProviderEnum provider = req.Provider.Value;
            SetupProviderOption option = GetOptions(globalAdmin).Providers.FirstOrDefault(o => o.Provider == provider)
                ?? throw new FieldValidationException("provider", provider + " cannot be set up here. Use the Models page.");
            if (!option.Available) throw new FieldValidationException("provider", option.UnavailableReason ?? provider + " is not available on this server.");
            ModelAuthMethodEnum method = req.AuthMethod ?? option.Methods[0].Method;
            if (!ModelCredentials.Accepts(provider, method)) throw new FieldValidationException("authMethod", provider + " cannot sign in with " + AgentCliService.Describe(method, provider) + ".");

            ModelEndpoint? endpoint = null;
            if (!String.IsNullOrWhiteSpace(req.ModelEndpointId))
            {
                endpoint = await _Database.ModelEndpoints.ReadAsync(tenantId, req.ModelEndpointId, token).ConfigureAwait(false);
                if (endpoint == null) throw new KeyNotFoundException("Model endpoint " + req.ModelEndpointId + " was not found.");
                if (IsPlatform(endpoint)) throw new ArgumentException("This endpoint uses the hosted service's key, so there is nothing to enter.");
            }
            else
            {
                EnumerationResult<ModelEndpoint> same = await _Database.ModelEndpoints.EnumerateAsync(new ModelEndpointFilter
                {
                    TenantId = tenantId, Provider = provider, PageSize = 100, SortBy = "createdUtc", SortDirection = SortDirectionEnum.Ascending
                }, token).ConfigureAwait(false);
                endpoint = same.Items.FirstOrDefault(e => e.SupportsChat && !IsPlatform(e));
            }

            bool create = endpoint == null;
            bool workingBefore = await ChatModelWorksAsync(tenantId, token).ConfigureAwait(false);
            ModelEndpoint? previous = create ? null : await _Database.ModelEndpoints.ReadAsync(tenantId, endpoint!.Id, token).ConfigureAwait(false);
            endpoint ??= new ModelEndpoint
            {
                TenantId = tenantId,
                Name = await FreeEndpointNameAsync(tenantId, Label(provider), token).ConfigureAwait(false),
                Provider = provider,
                SupportsChat = true
            };
            if (endpoint.Provider != provider) throw new ArgumentException("Endpoint " + endpoint.Name + " uses " + endpoint.Provider + ", not " + provider + ".");

            string secret = (req.Secret ?? String.Empty).Trim();
            bool needsSecret = AgentCliService.NeedsSecret(method);
            bool reuse = needsSecret && secret.Length == 0 && !create && endpoint.AuthMethod == method && await HasKeyAsync(tenantId, endpoint, token).ConfigureAwait(false);
            if (needsSecret && !reuse)
            {
                string? bad = ModelCredentials.Check(provider, method, secret);
                if (bad != null) throw new FieldValidationException("secret", bad);
            }

            string? model = String.IsNullOrWhiteSpace(req.Model) ? (endpoint.Model ?? option.DefaultModel) : req.Model.Trim();
            if (AgentCliService.IsAgentCli(provider) && String.Equals(model, AgentCliService.DefaultModel, StringComparison.OrdinalIgnoreCase)) model = null;
            if (String.IsNullOrWhiteSpace(model) && !AgentCliService.IsAgentCli(provider)) throw new FieldValidationException("model", "Name the model to use, for example one listed in the provider's console.");

            if (option.NeedsBaseUrl)
            {
                string url = String.IsNullOrWhiteSpace(req.BaseUrl) ? (endpoint.Endpoint ?? option.DefaultBaseUrl ?? String.Empty) : req.BaseUrl.Trim();
                if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed) || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
                    throw new FieldValidationException("baseUrl", "Enter the server's address, such as http://host.docker.internal:11434.");
                await Security.NetworkGuard.EnsureAllowedAsync(url, token).ConfigureAwait(false);
                endpoint.Endpoint = url;
            }
            else if (AgentCliService.IsAgentCli(provider))
            {
                endpoint.Endpoint = null;
            }

            endpoint.AuthMethod = method;
            endpoint.Model = model;
            endpoint.SupportsChat = true;
            endpoint.Active = true;
            if (AgentCliService.IsAgentCli(provider))
            {
                endpoint.SupportsTools = false;
                endpoint.SupportsEmbeddings = false;
                endpoint.Temperature = null;
            }

            if (provider == ModelProviderEnum.Anthropic || provider == ModelProviderEnum.ClaudeCode) endpoint.Temperature = null;
            endpoint.LastValidationSucceeded = null;
            endpoint = create
                ? await _Database.ModelEndpoints.CreateAsync(endpoint, token).ConfigureAwait(false)
                : await _Database.ModelEndpoints.UpdateAsync(endpoint, token).ConfigureAwait(false);

            // While a working model exists, a change is tried on a new credential so a failed test can be undone.
            bool safeChange = workingBefore && previous != null;
            string? oldCredentialId = previous?.CredentialId;
            string? newCredentialId = null;
            if (needsSecret && !reuse) newCredentialId = await StoreSecretAsync(tenantId, userId, endpoint, method, secret, !safeChange, token).ConfigureAwait(false);
            if (!needsSecret && !String.IsNullOrEmpty(endpoint.CredentialId) && method != ModelAuthMethodEnum.None)
            {
                endpoint.CredentialId = null;
                endpoint = await _Database.ModelEndpoints.UpdateAsync(endpoint, token).ConfigureAwait(false);
            }

            if (!workingBefore) await AssignChatTasksAsync(tenantId, endpoint.Id, token).ConfigureAwait(false);
            ModelValidationResponse test = await _Models.ValidateAsync(endpoint, token).ConfigureAwait(false);
            SetupConnectionResult connection = new SetupConnectionResult
            {
                ModelEndpointId = endpoint.Id,
                Succeeded = test.Succeeded,
                Message = String.Join(" ", test.Messages)
            };

            if (test.Succeeded)
            {
                if (workingBefore) await AssignChatTasksAsync(tenantId, endpoint.Id, token).ConfigureAwait(false);
                if (newCredentialId != null && !String.IsNullOrEmpty(oldCredentialId) && !String.Equals(oldCredentialId, newCredentialId, StringComparison.Ordinal))
                    await DeleteIfUnusedAsync(tenantId, oldCredentialId, token).ConfigureAwait(false);
            }
            else if (workingBefore)
            {
                connection.KeptPrevious = true;
                if (previous != null) await _Database.ModelEndpoints.UpdateAsync(previous, token).ConfigureAwait(false);
                if (newCredentialId != null && !String.Equals(oldCredentialId, newCredentialId, StringComparison.Ordinal))
                    await DeleteIfUnusedAsync(tenantId, newCredentialId, token).ConfigureAwait(false);
            }

            _Logging.Info(_Header + (test.Succeeded ? "connected " : "could not connect ") + provider + " (" + method + ") as endpoint " + endpoint.Id + " in tenant " + tenantId
                + (connection.KeptPrevious ? "; kept the working model" : String.Empty));
            SetupStatusResponse status = await BuildAsync(tenantId, true, false, null, token).ConfigureAwait(false);
            status.Connection = connection;
            return status;
        }

        /// <summary>
        /// Store an API key for an endpoint and verify it. Kept for clients of the first Setup API; new clients use
        /// <see cref="ConnectAsync"/>.
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
            if (String.IsNullOrWhiteSpace(req.ApiKey)) throw new ArgumentException("apiKey is required.");
            ModelEndpoint? endpoint = await _Database.ModelEndpoints.ReadAsync(tenantId, req.ModelEndpointId, token).ConfigureAwait(false);
            if (endpoint == null) throw new KeyNotFoundException("Model endpoint " + req.ModelEndpointId + " was not found.");
            if (IsPlatform(endpoint)) throw new ArgumentException("This endpoint uses the hosted service's key, so there is nothing to enter.");
            ModelAuthMethodEnum method = AgentCliService.EffectiveMethod(endpoint);
            if (!AgentCliService.NeedsSecret(method)) method = ModelAuthMethodEnum.ApiKey;
            string? bad = ModelCredentials.Check(endpoint.Provider, method, req.ApiKey);
            if (bad != null) throw new ArgumentException(bad);
            endpoint.AuthMethod = method;
            endpoint.LastValidationSucceeded = null;
            endpoint = await _Database.ModelEndpoints.UpdateAsync(endpoint, token).ConfigureAwait(false);
            await StoreSecretAsync(tenantId, userId, endpoint, method, req.ApiKey.Trim(), true, token).ConfigureAwait(false);
            await _Models.ValidateAsync(endpoint, token).ConfigureAwait(false);
            _Logging.Info(_Header + "stored a key for endpoint " + endpoint.Id + " in tenant " + tenantId);
            return await BuildAsync(tenantId, true, false, null, token).ConfigureAwait(false);
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

        private async Task<SetupStatusResponse> BuildAsync(string tenantId, bool canConfigure, bool live, SetupCheckKindEnum? only, CancellationToken token)
        {
            bool liveModel = live && (!only.HasValue || only.Value == SetupCheckKindEnum.Model);
            bool liveSearch = live && (!only.HasValue || only.Value == SetupCheckKindEnum.Search);
            bool liveEmbeddings = live && (!only.HasValue || only.Value == SetupCheckKindEnum.Embeddings);
            bool liveSpeech = live && (!only.HasValue || only.Value == SetupCheckKindEnum.Speech);
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
                SetupCheck check = await ModelCheckAsync(tenantId, pair.Value.Endpoint, pair.Value.Model, liveModel, token).ConfigureAwait(false);
                check.Tasks = tasks[pair.Key];
                status.Checks.Add(check);
            }

            status.Checks.Add(await SearchCheckAsync(tenantId, liveSearch, token).ConfigureAwait(false));

            if (canConfigure)
            {
                status.Checks.Add(await EmbeddingCheckAsync(tenantId, liveEmbeddings, token).ConfigureAwait(false));
                status.Checks.Add(EmailCheck());
                if (Audio != null) status.Checks.Add(await SpeechCheckAsync(Audio, liveSpeech, token).ConfigureAwait(false));
            }

            if (!canConfigure) foreach (SetupCheck c in status.Checks) c.BaseUrl = null;
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
                AuthMethod = AgentCliService.EffectiveMethod(endpoint),
                BaseUrl = endpoint.Provider == ModelProviderEnum.Ollama || endpoint.Provider == ModelProviderEnum.OpenAiCompatible ? endpoint.Endpoint : null,
                NeedsApiKey = AgentCliService.NeedsSecret(AgentCliService.EffectiveMethod(endpoint)),
                UsesPlatformKey = IsPlatform(endpoint) || !String.Equals(endpoint.TenantId, tenantId, StringComparison.Ordinal),
                VerifiedUtc = endpoint.LastValidatedUtc
            };

            // The operator's shared model (an endpoint of the default workspace): report it, never change or probe it here.
            bool shared = !String.Equals(endpoint.TenantId, tenantId, StringComparison.Ordinal);
            if (shared) live = false;

            if (AgentCliService.IsAgentCli(endpoint.Provider))
            {
                if (!_Models.AgentClis.Allowed())
                {
                    check.State = SetupCheckStateEnum.Failed;
                    check.Message = "Agent CLIs are turned off on this server, so " + endpoint.Name + " cannot run. Choose a provider API key instead.";
                    return check;
                }

                if (!_Models.AgentClis.IsInstalled(endpoint.Provider))
                {
                    check.State = SetupCheckStateEnum.Missing;
                    check.Message = "The " + Label(endpoint.Provider) + " CLI (" + _Models.AgentClis.ExecutableFor(endpoint.Provider) + ") is not installed on the server.";
                    return check;
                }
            }

            check.HasApiKey = await HasKeyAsync(tenantId, endpoint, token).ConfigureAwait(false);
            if (check.NeedsApiKey && !check.HasApiKey)
            {
                check.State = SetupCheckStateEnum.Missing;
                check.Message = check.UsesPlatformKey
                    ? "The hosted service's model key is not configured. The operator needs to set it."
                    : endpoint.Name + " needs " + AgentCliService.Describe(check.AuthMethod ?? ModelAuthMethodEnum.ApiKey, endpoint.Provider) + ".";
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
            else if (!shared && check.UsesPlatformKey && endpoint.LastValidationSucceeded != true)
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
            check.NeedsApiKey = AgentCliService.NeedsSecret(AgentCliService.EffectiveMethod(e));
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

        private async Task<SetupCheck> SpeechCheckAsync(AudioService audio, bool live, CancellationToken token)
        {
            TtsSettings tts = _Settings().Tts;
            SetupCheck check = new SetupCheck
            {
                Key = "speech",
                Kind = SetupCheckKindEnum.Speech,
                Required = false,
                Provider = TtsProviderEnum.OpenAiCompatibleSpeech.ToString(),
                Model = tts.Model,
                Voice = tts.Voice,
                BaseUrl = tts.Endpoint
            };
            if (Uri.TryCreate(tts.Endpoint, UriKind.Absolute, out Uri? uri)) check.ResourceName = uri.IsDefaultPort ? uri.Host : uri.Host + ":" + uri.Port;
            if (!tts.Enabled)
            {
                check.State = SetupCheckStateEnum.Missing;
                check.Message = "Speech is turned off, so briefings have no audio. A server administrator can turn it on in Server settings (tts.enabled).";
                return check;
            }

            if (String.IsNullOrWhiteSpace(tts.Endpoint) || uri == null)
            {
                check.State = SetupCheckStateEnum.Missing;
                check.Message = "No speech endpoint is set, so briefings have no audio. A server administrator can set it in Server settings (tts.endpoint).";
                return check;
            }

            string key = tts.Endpoint + "|" + tts.Model + "|" + tts.Voice;
            bool known;
            string? failure;
            DateTime checkedUtc;
            lock (_SpeechLock)
            {
                known = String.Equals(_SpeechKey, key, StringComparison.Ordinal) && (DateTime.UtcNow - _SpeechCheckedUtc).TotalSeconds < _SpeechCacheSeconds;
                failure = _SpeechFailure;
                checkedUtc = _SpeechCheckedUtc;
            }

            if (live || !known)
            {
                failure = await audio.ProbeAsync(_SpeechProbeTimeoutSeconds, token).ConfigureAwait(false);
                checkedUtc = DateTime.UtcNow;
                lock (_SpeechLock)
                {
                    _SpeechKey = key;
                    _SpeechFailure = failure;
                    _SpeechCheckedUtc = checkedUtc;
                }

                if (failure != null) _Logging.Warn(_Header + "the speech endpoint failed its check: " + failure);
            }

            check.VerifiedUtc = checkedUtc;
            if (failure == null)
            {
                check.State = SetupCheckStateEnum.Ready;
            }
            else
            {
                check.State = SetupCheckStateEnum.Failed;
                string reason = failure.Trim();
                if (reason.Length > 0 && !reason.EndsWith('.')) reason += ".";
                check.Message = reason + " Research and email still work; briefings have no audio until the speech server answers.";
            }

            return check;
        }

        private async Task<bool> HasKeyAsync(string tenantId, ModelEndpoint endpoint, CancellationToken token)
        {
            if (String.IsNullOrEmpty(endpoint.CredentialId)) return false;
            try
            {
                string? key = await _Vault.ResolveAsync(endpoint.TenantId, endpoint.CredentialId, token).ConfigureAwait(false);
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

        private static bool IsWebSearch(SearchProviderKindEnum kind)
        {
            return kind == SearchProviderKindEnum.Searxng
                || kind == SearchProviderKindEnum.Tavily
                || kind == SearchProviderKindEnum.YouCom
                || kind == SearchProviderKindEnum.HostedModelWebSearch;
        }

        private SetupProviderOption Option(ModelProviderEnum provider, SetupProviderGroupEnum group, bool globalAdmin, bool recommended, string? defaultModel, bool needsBaseUrl, string? defaultBaseUrl)
        {
            SetupProviderOption option = new SetupProviderOption
            {
                Provider = provider,
                Group = group,
                Recommended = recommended,
                DefaultModel = defaultModel,
                NeedsBaseUrl = needsBaseUrl,
                DefaultBaseUrl = defaultBaseUrl
            };
            IReadOnlyList<ModelAuthMethodEnum> methods = AgentCliService.MethodsFor(provider);
            for (int i = 0; i < methods.Count; i++)
            {
                ModelAuthMethodEnum m = methods[i];
                option.Methods.Add(new SetupAuthOption
                {
                    Method = m,
                    Input = m == ModelAuthMethodEnum.AuthFile ? "File" : AgentCliService.NeedsSecret(m) ? "Text" : "None",
                    FileName = m == ModelAuthMethodEnum.AuthFile ? AgentCliService.AuthFileName(provider) : null,
                    Recommended = i == 0
                });
            }

            if (AgentCliService.IsAgentCli(provider))
            {
                option.Installed = _Models.AgentClis.IsInstalled(provider);
                if (!_Models.AgentClis.Allowed())
                {
                    option.Available = false;
                    option.TurnedOff = true;
                    option.UnavailableReason = "Agent CLIs are turned off on this server (Models.AllowAgentClis).";
                }
                else if (!_Models.AgentClis.AllowedFor(globalAdmin))
                {
                    option.Available = false;
                    option.TurnedOff = true;
                    option.UnavailableReason = "On this server, only administrators can connect agent CLIs. Use an API key instead.";
                }
                else if (!option.Installed)
                {
                    option.Available = false;
                    option.UnavailableReason = "The " + Label(provider) + " CLI (" + _Models.AgentClis.ExecutableFor(provider) + ") is not installed on the server.";
                }
            }

            return option;
        }

        private async Task<string> StoreSecretAsync(string tenantId, string? userId, ModelEndpoint endpoint, ModelAuthMethodEnum method, string secret, bool rotate, CancellationToken token)
        {
            VaultCredential? existing = null;
            if (rotate && !String.IsNullOrEmpty(endpoint.CredentialId) && !IsPlatform(endpoint))
                existing = await _Database.VaultCredentials.ReadAsync(tenantId, endpoint.CredentialId, token).ConfigureAwait(false);
            if (existing != null && existing.Kind == ModelCredentials.KindFor(method))
            {
                await _Vault.UpdateAsync(existing, new VaultCredentialUpsertRequest { Secret = secret, Active = true }, token).ConfigureAwait(false);
                return existing.Id;
            }

            string suffix = method == ModelAuthMethodEnum.AuthFile ? " " + (AgentCliService.AuthFileName(endpoint.Provider) ?? "sign-in file")
                : method == ModelAuthMethodEnum.AccessToken ? " token" : " API key";
            VaultCredential created = await _Vault.CreateAsync(tenantId, userId, new VaultCredentialUpsertRequest
            {
                Name = await FreeCredentialNameAsync(tenantId, endpoint.Name + suffix, token).ConfigureAwait(false),
                Kind = ModelCredentials.KindFor(method),
                Provider = ModelCredentials.VaultProviderFor(endpoint.Provider),
                Secret = secret,
                Description = "Added on the Setup page."
            }, token).ConfigureAwait(false);
            endpoint.CredentialId = created.Id;
            await _Database.ModelEndpoints.UpdateAsync(endpoint, token).ConfigureAwait(false);
            return created.Id;
        }

        private async Task<bool> ChatModelWorksAsync(string tenantId, CancellationToken token)
        {
            foreach (ModelTaskEnum task in ChatTasks)
            {
                try
                {
                    ResolvedModel resolved = await _Models.ResolveAsync(tenantId, task, null, null, null, token).ConfigureAwait(false);
                    if (resolved.Endpoint.LastValidationSucceeded != true) return false;
                }
                catch (ModelCallException)
                {
                    return false;
                }
            }

            return true;
        }

        private async Task DeleteIfUnusedAsync(string tenantId, string credentialId, CancellationToken token)
        {
            if (String.Equals(credentialId, VaultService.PlatformAnthropicCredentialId, StringComparison.Ordinal)) return;
            EnumerationResult<ModelEndpoint> users = await _Database.ModelEndpoints.EnumerateAsync(new ModelEndpointFilter { TenantId = tenantId, CredentialId = credentialId, PageSize = 1 }, token).ConfigureAwait(false);
            if (users.Items.Count > 0) return;
            await _Database.VaultCredentials.DeleteAsync(tenantId, credentialId, token).ConfigureAwait(false);
        }

        private async Task AssignChatTasksAsync(string tenantId, string endpointId, CancellationToken token)
        {
            foreach (ModelTaskEnum task in ChatTasks)
            {
                EnumerationResult<ModelAssignment> existing = await _Database.ModelAssignments.EnumerateAsync(new ModelAssignmentFilter { TenantId = tenantId, Task = task, PageSize = 100 }, token).ConfigureAwait(false);
                ModelAssignment? a = existing.Items.FirstOrDefault(x => x.TopicId == null);
                if (a == null)
                {
                    await _Database.ModelAssignments.CreateAsync(new ModelAssignment { TenantId = tenantId, Task = task, ModelEndpointId = endpointId }, token).ConfigureAwait(false);
                }
                else if (!String.Equals(a.ModelEndpointId, endpointId, StringComparison.Ordinal) || !a.Active || a.ModelOverride != null)
                {
                    a.ModelEndpointId = endpointId;
                    a.ModelOverride = null;
                    a.Active = true;
                    await _Database.ModelAssignments.UpdateAsync(a, token).ConfigureAwait(false);
                }
            }
        }

        private async Task<string> FreeEndpointNameAsync(string tenantId, string name, CancellationToken token)
        {
            EnumerationResult<ModelEndpoint> all = await _Database.ModelEndpoints.EnumerateAsync(new ModelEndpointFilter { TenantId = tenantId, PageSize = 1000 }, token).ConfigureAwait(false);
            string candidate = name;
            for (int i = 2; all.Items.Any(e => String.Equals(e.Name, candidate, StringComparison.OrdinalIgnoreCase)); i++) candidate = name + " " + i;
            return candidate;
        }

        private static string Label(ModelProviderEnum provider)
        {
            switch (provider)
            {
                case ModelProviderEnum.OpenAi: return "OpenAI";
                case ModelProviderEnum.Gemini: return "Google Gemini";
                case ModelProviderEnum.XAi: return "xAI";
                case ModelProviderEnum.OllamaCloud: return "Ollama Cloud";
                case ModelProviderEnum.ClaudeCode: return "Claude Code";
                case ModelProviderEnum.GeminiCli: return "Gemini CLI";
                case ModelProviderEnum.OpenAiCompatible: return "OpenAI-compatible server";
                default: return provider.ToString();
            }
        }

        #endregion
    }
}
