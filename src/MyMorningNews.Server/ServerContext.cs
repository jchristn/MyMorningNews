namespace MyMorningNews.Server
{
    using System;
    using MyMorningNews.Core.Briefings;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Fetch;
    using MyMorningNews.Core.Pipeline;
    using MyMorningNews.Core.Scheduling;
    using MyMorningNews.Core.Search;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Core.Storage;
    using SyslogLogging;

    /// <summary>
    /// Shared dependencies handed to route registrars and workers.
    /// </summary>
    public class ServerContext
    {
        /// <summary>
        /// Settings service.
        /// </summary>
        public SettingsService SettingsService { get; }

        /// <summary>
        /// Settings in effect.
        /// </summary>
        public ServerSettings Settings
        {
            get { return SettingsService.Current; }
        }

        /// <summary>
        /// Database driver.
        /// </summary>
        public DatabaseDriverBase Database { get; }

        /// <summary>
        /// Logging.
        /// </summary>
        public LoggingModule Logging { get; }

        /// <summary>
        /// Authentication.
        /// </summary>
        public AuthenticationService Authentication { get; }

        /// <summary>
        /// Authorization.
        /// </summary>
        public AuthorizationService Authorization { get; }

        /// <summary>
        /// Audit.
        /// </summary>
        public AuditService Audit { get; }

        /// <summary>
        /// Lifetime.
        /// </summary>
        public ServerLifetime Lifetime { get; }

        /// <summary>
        /// Credential vault.
        /// </summary>
        public VaultService Vault { get; }

        /// <summary>
        /// Prompt templates.
        /// </summary>
        public PromptService Prompts { get; }

        /// <summary>
        /// Model gateway.
        /// </summary>
        public ModelGateway Models { get; }

        /// <summary>
        /// Per-tenant defaults.
        /// </summary>
        public TenantDefaultsService TenantDefaults { get; }

        /// <summary>
        /// Mobile push notifications.
        /// </summary>
        public PushService Push { get; }

        /// <summary>
        /// Topic refinement wizard.
        /// </summary>
        public RefinementService Refinement { get; }

        /// <summary>
        /// One-time requests.
        /// </summary>
        public ResearchRequestService ResearchRequests { get; }

        /// <summary>
        /// Schedules and the run queue.
        /// </summary>
        public SchedulingService Scheduling { get; }

        /// <summary>
        /// Web search across providers.
        /// </summary>
        public SearchService Search { get; }

        /// <summary>
        /// Page fetching.
        /// </summary>
        public FetchService Fetch { get; }

        /// <summary>
        /// Executor used by the run worker. Replaceable (tests and later phases install the research pipeline).
        /// </summary>
        public IRunExecutor RunExecutor { get; set; } = new PendingPipelineExecutor();

        /// <summary>
        /// Stored files (briefing Markdown and HTML, audio, exports).
        /// </summary>
        public ArtifactService Artifacts { get; set; } = null!;

        /// <summary>
        /// Briefings, editions, and composition.
        /// </summary>
        public BriefingService Briefings { get; set; } = null!;

        /// <summary>
        /// Audio, voices, and podcast feeds.
        /// </summary>
        public MyMorningNews.Core.Audio.AudioService Audio { get; set; } = null!;

        /// <summary>
        /// Email outbox and delivery.
        /// </summary>
        public MyMorningNews.Core.Email.EmailService Email { get; set; } = null!;

        /// <summary>
        /// Reader feedback.
        /// </summary>
        public MyMorningNews.Core.Feedback.FeedbackService Feedback { get; set; } = null!;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settingsService">Settings service.</param>
        /// <param name="database">Database.</param>
        /// <param name="logging">Logging.</param>
        /// <param name="authentication">Authentication.</param>
        /// <param name="authorization">Authorization.</param>
        /// <param name="audit">Audit.</param>
        /// <param name="lifetime">Lifetime.</param>
        /// <param name="vault">Vault.</param>
        /// <param name="prompts">Prompts.</param>
        /// <param name="models">Model gateway.</param>
        /// <param name="tenantDefaults">Tenant defaults.</param>
        /// <param name="push">Push notifications.</param>
        /// <param name="refinement">Refinement wizard.</param>
        /// <param name="researchRequests">One-time requests.</param>
        /// <param name="scheduling">Scheduling and runs.</param>
        /// <param name="search">Search.</param>
        /// <param name="fetch">Fetch.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public ServerContext(
            SettingsService settingsService,
            DatabaseDriverBase database,
            LoggingModule logging,
            AuthenticationService authentication,
            AuthorizationService authorization,
            AuditService audit,
            ServerLifetime lifetime,
            VaultService vault,
            PromptService prompts,
            ModelGateway models,
            TenantDefaultsService tenantDefaults,
            PushService push,
            RefinementService refinement,
            ResearchRequestService researchRequests,
            SchedulingService scheduling,
            SearchService search,
            FetchService fetch)
        {
            ArgumentNullException.ThrowIfNull(settingsService);
            ArgumentNullException.ThrowIfNull(database);
            ArgumentNullException.ThrowIfNull(logging);
            ArgumentNullException.ThrowIfNull(authentication);
            ArgumentNullException.ThrowIfNull(authorization);
            ArgumentNullException.ThrowIfNull(audit);
            ArgumentNullException.ThrowIfNull(lifetime);
            SettingsService = settingsService;
            Database = database;
            Logging = logging;
            Authentication = authentication;
            Authorization = authorization;
            Audit = audit;
            Lifetime = lifetime;
            Vault = vault ?? throw new ArgumentNullException(nameof(vault));
            Prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
            Push = push ?? throw new ArgumentNullException(nameof(push));
            Refinement = refinement ?? throw new ArgumentNullException(nameof(refinement));
            ResearchRequests = researchRequests ?? throw new ArgumentNullException(nameof(researchRequests));
            Scheduling = scheduling ?? throw new ArgumentNullException(nameof(scheduling));
            Search = search ?? throw new ArgumentNullException(nameof(search));
            Fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));
            Models = models ?? throw new ArgumentNullException(nameof(models));
            TenantDefaults = tenantDefaults ?? throw new ArgumentNullException(nameof(tenantDefaults));
        }
    }
}
