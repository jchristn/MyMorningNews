namespace MyMorningNews.Server
{
    using System;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Core.Settings;
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
            PushService push)
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
            Models = models ?? throw new ArgumentNullException(nameof(models));
            TenantDefaults = tenantDefaults ?? throw new ArgumentNullException(nameof(tenantDefaults));
        }
    }
}
