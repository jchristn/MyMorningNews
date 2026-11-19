namespace MyMorningNews.Core.Database
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database.Interfaces;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Abstract base class for database drivers. Exposes domain-specific method interfaces.
    /// Thread safety: method interfaces are safe for concurrent use after <see cref="InitializeAsync"/> completes.
    /// </summary>
    public abstract class DatabaseDriverBase : IDisposable, IAsyncDisposable
    {
        #region Public-Members

        /// <summary>
        /// Database type.
        /// </summary>
        public abstract DatabaseTypeEnum DatabaseType { get; }

        /// <summary>
        /// Settings used to connect.
        /// </summary>
        public DatabaseSettings Settings { get; }

        /// <summary>
        /// Highest applied schema migration version, available after initialization.
        /// </summary>
        public int SchemaVersion { get; protected set; } = 0;

        /// <summary>
        /// Tenant methods.
        /// </summary>
        public ITenantMethods Tenants { get; protected set; } = null!;

        /// <summary>
        /// User methods.
        /// </summary>
        public IUserMethods Users { get; protected set; } = null!;

        /// <summary>
        /// Credential methods.
        /// </summary>
        public ICredentialMethods Credentials { get; protected set; } = null!;

        /// <summary>
        /// Auth session methods.
        /// </summary>
        public IAuthSessionMethods AuthSessions { get; protected set; } = null!;

        /// <summary>
        /// Role methods.
        /// </summary>
        public IUserRoleMethods Roles { get; protected set; } = null!;

        /// <summary>
        /// Permission methods.
        /// </summary>
        public IPermissionMethods Permissions { get; protected set; } = null!;

        /// <summary>
        /// Role permission map methods.
        /// </summary>
        public IRolePermissionMapMethods RolePermissionMaps { get; protected set; } = null!;

        /// <summary>
        /// User role assignment methods.
        /// </summary>
        public IUserRoleAssignmentMethods UserRoleAssignments { get; protected set; } = null!;

        /// <summary>
        /// Credential scope assignment methods.
        /// </summary>
        public ICredentialScopeAssignmentMethods CredentialScopeAssignments { get; protected set; } = null!;

        /// <summary>
        /// Audit record methods.
        /// </summary>
        public IAuditRecordMethods AuditRecords { get; protected set; } = null!;

        /// <summary>
        /// Request history methods.
        /// </summary>
        public IRequestHistoryMethods RequestHistory { get; protected set; } = null!;

        /// <summary>
        /// Vault credential methods.
        /// </summary>
        public IVaultCredentialMethods VaultCredentials { get; protected set; } = null!;

        /// <summary>
        /// Model endpoint methods.
        /// </summary>
        public IModelEndpointMethods ModelEndpoints { get; protected set; } = null!;

        /// <summary>
        /// Model assignment methods.
        /// </summary>
        public IModelAssignmentMethods ModelAssignments { get; protected set; } = null!;

        /// <summary>
        /// Model call log methods.
        /// </summary>
        public ILlmCallMethods LlmCalls { get; protected set; } = null!;

        /// <summary>
        /// Prompt template methods.
        /// </summary>
        public IPromptTemplateMethods PromptTemplates { get; protected set; } = null!;

        /// <summary>
        /// Prompt template version methods.
        /// </summary>
        public IPromptTemplateVersionMethods PromptTemplateVersions { get; protected set; } = null!;

        /// <summary>
        /// Model usage and cost analytics.
        /// </summary>
        public IUsageAnalyticsMethods UsageAnalytics { get; protected set; } = null!;

        /// <summary>
        /// Mobile refresh tokens.
        /// </summary>
        public IRefreshTokenMethods RefreshTokens { get; protected set; } = null!;

        /// <summary>
        /// Push notification devices.
        /// </summary>
        public IDeviceMethods Devices { get; protected set; } = null!;

        /// <summary>
        /// Topics.
        /// </summary>
        public ITopicMethods Topics { get; protected set; } = null!;

        /// <summary>
        /// Topic dimensions.
        /// </summary>
        public ITopicDimensionMethods TopicDimensions { get; protected set; } = null!;

        /// <summary>
        /// Topic constraints.
        /// </summary>
        public ITopicConstraintMethods TopicConstraints { get; protected set; } = null!;

        /// <summary>
        /// Topic email recipients.
        /// </summary>
        public ITopicRecipientMethods TopicRecipients { get; protected set; } = null!;

        /// <summary>
        /// Refinement sessions.
        /// </summary>
        public IRefinementSessionMethods RefinementSessions { get; protected set; } = null!;

        /// <summary>
        /// Refinement transcript messages.
        /// </summary>
        public IRefinementMessageMethods RefinementMessages { get; protected set; } = null!;

        /// <summary>
        /// Refinement questions.
        /// </summary>
        public IRefinementQuestionMethods RefinementQuestions { get; protected set; } = null!;

        /// <summary>
        /// Refinement question options.
        /// </summary>
        public IRefinementQuestionOptionMethods RefinementQuestionOptions { get; protected set; } = null!;

        /// <summary>
        /// One-time research requests.
        /// </summary>
        public IResearchRequestMethods ResearchRequests { get; protected set; } = null!;

        /// <summary>
        /// Schedules.
        /// </summary>
        public IScheduleMethods Schedules { get; protected set; } = null!;

        /// <summary>
        /// Research runs.
        /// </summary>
        public IRunMethods Runs { get; protected set; } = null!;

        /// <summary>
        /// Search providers.
        /// </summary>
        public ISearchProviderMethods SearchProviders { get; protected set; } = null!;

        /// <summary>
        /// Search providers enabled per topic.
        /// </summary>
        public ITopicSearchProviderMethods TopicSearchProviders { get; protected set; } = null!;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <exception cref="ArgumentNullException">settings is null.</exception>
        protected DatabaseDriverBase(DatabaseSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            Settings = settings;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Initialize the driver: create the database if missing and apply migrations.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public abstract Task InitializeAsync(CancellationToken token = default);

        /// <summary>
        /// Execute a single query.
        /// </summary>
        /// <param name="query">Query text.</param>
        /// <param name="isTransaction">Whether to run inside a transaction.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result table.</returns>
        public abstract Task<DataTable> ExecuteQueryAsync(string query, bool isTransaction = false, CancellationToken token = default);

        /// <summary>
        /// Execute multiple queries, returning the result of the last one.
        /// </summary>
        /// <param name="queries">Queries.</param>
        /// <param name="isTransaction">Whether to run inside a single transaction.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result table of the last query.</returns>
        public abstract Task<DataTable> ExecuteQueriesAsync(IEnumerable<string> queries, bool isTransaction = false, CancellationToken token = default);

        /// <summary>
        /// Close the driver and release pooled connections.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public abstract Task CloseAsync(CancellationToken token = default);

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose of resources asynchronously.
        /// </summary>
        /// <returns>Task.</returns>
        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        /// <param name="disposing">True when called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
        }

        /// <summary>
        /// Asynchronous disposal hook.
        /// </summary>
        /// <returns>Task.</returns>
        protected virtual ValueTask DisposeAsyncCore()
        {
            return ValueTask.CompletedTask;
        }

        #endregion
    }
}
