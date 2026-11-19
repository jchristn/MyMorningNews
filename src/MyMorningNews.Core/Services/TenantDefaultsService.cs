namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Settings;
    using SyslogLogging;

    /// <summary>
    /// Seeds per-tenant defaults: prompts, an Anthropic chat endpoint (claude-opus-5-5, key from
    /// MYMORNINGNEWS_ANTHROPIC_API_KEY when set), an Ollama embedding endpoint (all-minilm), and a model assignment for
    /// every task role. Runs at startup for every tenant and when a tenant is created. Never overwrites existing records.
    /// Thread safety: safe to call concurrently for different tenants.
    /// </summary>
    public class TenantDefaultsService
    {
        #region Public-Members

        /// <summary>
        /// Environment variable read for the initial Anthropic API key.
        /// </summary>
        public string AnthropicKeyVariable { get; set; } = Constants.EnvironmentPrefix + "ANTHROPIC_API_KEY";

        #endregion

        #region Public-Members

        /// <summary>
        /// Search service used to seed default search providers. Null skips search provider seeding.
        /// </summary>
        public Search.SearchService? Search { get; set; } = null;

        /// <summary>
        /// Briefing service for default templates and the Morning News edition; null skips them.
        /// </summary>
        public Briefings.BriefingService? Briefings { get; set; } = null;

        #endregion

        #region Private-Members

        private static readonly HashSet<ModelTaskEnum> _HighEffort = new HashSet<ModelTaskEnum>
        {
            ModelTaskEnum.Refinement, ModelTaskEnum.Synthesis, ModelTaskEnum.Prediction, ModelTaskEnum.Suggestion, ModelTaskEnum.Composition
        };

        private readonly DatabaseDriverBase _Database;
        private readonly PromptService _Prompts;
        private readonly VaultService _Vault;
        private readonly ModelSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[TenantDefaults] ";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="prompts">Prompt service.</param>
        /// <param name="vault">Vault.</param>
        /// <param name="settings">Model defaults.</param>
        /// <param name="logging">Logging.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public TenantDefaultsService(DatabaseDriverBase database, PromptService prompts, VaultService vault, ModelSettings settings, LoggingModule logging)
        {
            ArgumentNullException.ThrowIfNull(database);
            ArgumentNullException.ThrowIfNull(prompts);
            ArgumentNullException.ThrowIfNull(vault);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(logging);
            _Database = database;
            _Prompts = prompts;
            _Vault = vault;
            _Settings = settings;
            _Logging = logging;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Ensure defaults for every tenant.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task EnsureAllAsync(CancellationToken token = default)
        {
            int page = 1;
            while (true)
            {
                EnumerationResult<Tenant> tenants = await _Database.Tenants.EnumerateAsync(new TenantFilter { PageNumber = page, PageSize = 200 }, token).ConfigureAwait(false);
                foreach (Tenant t in tenants.Items) await EnsureAsync(t.Id, token).ConfigureAwait(false);
                if (page >= tenants.TotalPages) break;
                page++;
            }
        }

        /// <summary>
        /// Ensure defaults for one tenant.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task EnsureAsync(string tenantId, CancellationToken token = default)
        {
            int prompts = await _Prompts.EnsureDefaultsAsync(tenantId, token).ConfigureAwait(false);
            if (prompts > 0) _Logging.Info(_Header + "seeded or updated " + prompts + " prompts for tenant " + tenantId);
            if (Search != null)
            {
                int providers = await Search.EnsureDefaultsAsync(tenantId, token).ConfigureAwait(false);
                if (providers > 0) _Logging.Info(_Header + "seeded " + providers + " search providers for tenant " + tenantId);
            }

            if (Briefings != null)
            {
                int templates = await Briefings.Templates.EnsureDefaultsAsync(tenantId, token).ConfigureAwait(false);
                if (templates > 0) _Logging.Info(_Header + "seeded or updated " + templates + " briefing templates for tenant " + tenantId);
                await Briefings.BackfillDefaultEditionsAsync(tenantId, token).ConfigureAwait(false);
            }

            long endpoints = await _Database.ModelEndpoints.CountAsync(new ModelEndpointFilter { TenantId = tenantId }, token).ConfigureAwait(false);
            if (endpoints > 0) return;

            string? credentialId = null;
            string? key = Environment.GetEnvironmentVariable(AnthropicKeyVariable);
            if (!String.IsNullOrWhiteSpace(key))
            {
                VaultCredential credential = await _Vault.CreateAsync(tenantId, null, new VaultCredentialUpsertRequest
                {
                    Name = "Anthropic API key",
                    Kind = VaultCredentialKindEnum.ApiKey,
                    Provider = VaultProviderEnum.Anthropic,
                    Secret = key.Trim(),
                    Description = "Seeded from " + AnthropicKeyVariable + "."
                }, token).ConfigureAwait(false);
                credentialId = credential.Id;
            }

            ModelEndpoint anthropic = await _Database.ModelEndpoints.CreateAsync(new ModelEndpoint
            {
                TenantId = tenantId,
                Name = "Anthropic",
                Provider = ModelProviderEnum.Anthropic,
                CredentialId = credentialId,
                Model = _Settings.DefaultChatModel,
                SupportsChat = true,
                SupportsTools = true,
                SupportsEmbeddings = false,
                MaxTokens = 16000,
                ReasoningEffort = ReasoningEffortEnum.Medium,
                InputCostPerMTok = 4m,
                OutputCostPerMTok = 20m,
                CachedInputCostPerMTok = 0.20m,
                CacheWriteCostPerMTok = 5m
            }, token).ConfigureAwait(false);

            ModelEndpoint ollama = await _Database.ModelEndpoints.CreateAsync(new ModelEndpoint
            {
                TenantId = tenantId,
                Name = "Ollama embeddings",
                Provider = ModelProviderEnum.Ollama,
                Endpoint = _Settings.DefaultOllamaEndpoint,
                EmbeddingModel = _Settings.DefaultEmbeddingModel,
                EmbeddingDimensions = String.Equals(_Settings.DefaultEmbeddingModel, "all-minilm", StringComparison.OrdinalIgnoreCase) ? 384 : null,
                SupportsChat = false,
                SupportsEmbeddings = true
            }, token).ConfigureAwait(false);

            foreach (ModelTaskEnum task in Enum.GetValues<ModelTaskEnum>())
            {
                bool embedding = task == ModelTaskEnum.Embedding;
                await _Database.ModelAssignments.CreateAsync(new ModelAssignment
                {
                    TenantId = tenantId,
                    Task = task,
                    ModelEndpointId = embedding ? ollama.Id : anthropic.Id,
                    ReasoningEffort = embedding ? null : _HighEffort.Contains(task) ? ReasoningEffortEnum.High : ReasoningEffortEnum.Medium
                }, token).ConfigureAwait(false);
            }

            _Logging.Info(_Header + "seeded model endpoints for tenant " + tenantId + (credentialId == null ? " (no Anthropic key yet; add one in the vault)" : String.Empty));
        }

        #endregion
    }
}
