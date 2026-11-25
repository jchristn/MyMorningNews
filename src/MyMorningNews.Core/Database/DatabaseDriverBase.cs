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
        /// Application entitlement resolver for atomic topic creation and restoration. Null leaves embedded database callers unrestricted.
        /// Arguments are tenant, user, one-time flag, cancellation. Null result is unlimited; zero denies creation.
        /// Set once during server startup. All writes still acquire the account lock before checking slot counts.
        /// </summary>
        public Func<string, string, bool, CancellationToken, Task<long?>>? TopicSlotLimit { get; set; } = null;


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
        /// In-app notification data access.
        /// </summary>
        public INotificationMethods Notifications { get; protected set; } = null!;

        /// <summary>
        /// Notification preference data access.
        /// </summary>
        public INotificationPreferenceMethods NotificationPreferences { get; protected set; } = null!;

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

        /// <summary>
        /// Run stages.
        /// </summary>
        public IRunStageMethods RunStages { get; protected set; } = null!;

        /// <summary>
        /// Search queries per run.
        /// </summary>
        public ISearchQueryMethods SearchQueries { get; protected set; } = null!;

        /// <summary>
        /// Search results per run.
        /// </summary>
        public ISearchResultMethods SearchResults { get; protected set; } = null!;

        /// <summary>
        /// Fetched sources.
        /// </summary>
        public ISourceItemMethods SourceItems { get; protected set; } = null!;

        /// <summary>
        /// Verified quotes.
        /// </summary>
        public IEvidenceMethods Evidence { get; protected set; } = null!;

        /// <summary>
        /// Findings.
        /// </summary>
        public IFindingMethods Findings { get; protected set; } = null!;

        /// <summary>
        /// Finding to evidence links.
        /// </summary>
        public IFindingEvidenceMethods FindingEvidence { get; protected set; } = null!;

        /// <summary>
        /// Predictions.
        /// </summary>
        public IPredictionMethods Predictions { get; protected set; } = null!;

        /// <summary>
        /// Prediction reviews.
        /// </summary>
        public IPredictionReviewMethods PredictionReviews { get; protected set; } = null!;

        /// <summary>
        /// Suggestions.
        /// </summary>
        public ISuggestionMethods Suggestions { get; protected set; } = null!;

        /// <summary>Internal temporary crawl cache metadata and lease operations.</summary>
        public ICrawlCacheMethods CrawlCache { get; protected set; } = null!;

        /// <summary>Artifact metadata.</summary>
        public IArtifactMethods Artifacts { get; protected set; } = null!;

        /// <summary>
        /// Briefing templates.
        /// </summary>
        public IBriefingTemplateMethods BriefingTemplates { get; protected set; } = null!;

        /// <summary>
        /// Briefing template versions.
        /// </summary>
        public IBriefingTemplateVersionMethods BriefingTemplateVersions { get; protected set; } = null!;

        /// <summary>
        /// Editions.
        /// </summary>
        public IEditionMethods Editions { get; protected set; } = null!;

        /// <summary>
        /// Edition topics.
        /// </summary>
        public IEditionTopicMethods EditionTopics { get; protected set; } = null!;

        /// <summary>
        /// Briefings.
        /// </summary>
        public IBriefingMethods Briefings { get; protected set; } = null!;

        /// <summary>
        /// Briefing sections.
        /// </summary>
        public IBriefingSectionMethods BriefingSections { get; protected set; } = null!;

        /// <summary>
        /// Briefing items.
        /// </summary>
        public IBriefingItemMethods BriefingItems { get; protected set; } = null!;

        /// <summary>
        /// Voice profiles.
        /// </summary>
        public IVoiceProfileMethods VoiceProfiles { get; protected set; } = null!;

        /// <summary>
        /// Audio episodes.
        /// </summary>
        public IMediaAssetMethods MediaAssets { get; protected set; } = null!;

        /// <summary>
        /// Audio chapters.
        /// </summary>
        public IMediaChapterMethods MediaChapters { get; protected set; } = null!;

        /// <summary>
        /// Podcast feed tokens.
        /// </summary>
        public IFeedTokenMethods FeedTokens { get; protected set; } = null!;

        /// <summary>
        /// Public share links for briefings.
        /// </summary>
        public IBriefingShareMethods BriefingShares { get; protected set; } = null!;

        /// <summary>
        /// Email outbox.
        /// </summary>
        public IEmailMessageMethods EmailMessages { get; protected set; } = null!;

        /// <summary>
        /// Email link tokens.
        /// </summary>
        public IEmailActionTokenMethods EmailActionTokens { get; protected set; } = null!;

        /// <summary>
        /// Email suppressions.
        /// </summary>
        public IEmailSuppressionMethods EmailSuppressions { get; protected set; } = null!;

        /// <summary>
        /// Reader feedback.
        /// </summary>
        public IFeedbackMethods Feedback { get; protected set; } = null!;

        /// <summary>
        /// Learned preferences.
        /// </summary>
        public ILearnedPreferenceMethods LearnedPreferences { get; protected set; } = null!;

        /// <summary>
        /// Feedback behind learned preferences.
        /// </summary>
        public ILearnedPreferenceFeedbackMethods LearnedPreferenceFeedback { get; protected set; } = null!;

        /// <summary>
        /// Feedback reason tags.
        /// </summary>
        public IFeedbackReasonTagMethods FeedbackReasonTags { get; protected set; } = null!;

        /// <summary>
        /// Learning checkpoints.
        /// </summary>
        public ILearningCheckpointMethods LearningCheckpoints { get; protected set; } = null!;

        /// <summary>
        /// Service plans.
        /// </summary>
        public IPlanMethods Plans { get; protected set; } = null!;

        /// <summary>Immutable historical briefing context for research runs.</summary>
        public IRunHistorySnapshotMethods RunHistorySnapshots { get; protected set; } = null!;

        /// <summary>Private account provider connections.</summary>
        public IProviderConnectionMethods ProviderConnections { get; protected set; } = null!;

        /// <summary>
        /// AccountPlanPolicy persistence.
        /// </summary>
        public IAccountPlanPolicyMethods AccountPlanPolicies { get; protected set; } = null!;

        /// <summary>
        /// EntitlementOverride persistence.
        /// </summary>
        public IEntitlementOverrideMethods EntitlementOverrides { get; protected set; } = null!;

        /// <summary>
        /// CatalogPublication persistence.
        /// </summary>
        public ICatalogPublicationMethods CatalogPublications { get; protected set; } = null!;

        /// <summary>
        /// CatalogSubscription persistence.
        /// </summary>
        public ICatalogSubscriptionMethods CatalogSubscriptions { get; protected set; } = null!;

        /// <summary>Durable catalog delivery queue.</summary>
        public ICatalogDeliveryMethods CatalogDeliveries { get; protected set; } = null!;

        /// <summary>
        /// AudioReservation persistence.
        /// </summary>
        public IAudioReservationMethods AudioReservations { get; protected set; } = null!;

        /// <summary>
        /// BillingSubscription persistence.
        /// </summary>
        public IBillingSubscriptionMethods BillingSubscriptions { get; protected set; } = null!;

        /// <summary>BillingAccount persistence.</summary>
        public IBillingAccountMethods BillingAccounts { get; protected set; } = null!;

        /// <summary>BillingCheckout persistence.</summary>
        public IBillingCheckoutMethods BillingCheckouts { get; protected set; } = null!;

        /// <summary>BillingEvent persistence.</summary>
        public IBillingEventMethods BillingEvents { get; protected set; } = null!;

        /// <summary>
        /// SignupBenefit persistence.
        /// </summary>
        public ISignupBenefitMethods SignupBenefits { get; protected set; } = null!;

        /// <summary>
        /// Plan entitlement values.
        /// </summary>
        public IPlanFeatureMethods PlanFeatures { get; protected set; } = null!;

        /// <summary>
        /// Plan assignments.
        /// </summary>
        public IPlanAssignmentMethods PlanAssignments { get; protected set; } = null!;

        /// <summary>
        /// Promotional codes.
        /// </summary>
        public IPromoCodeMethods PromoCodes { get; protected set; } = null!;

        /// <summary>
        /// Promotional code redemptions.
        /// </summary>
        public IPromoRedemptionMethods PromoRedemptions { get; protected set; } = null!;

        /// <summary>
        /// Registered MCP servers.
        /// </summary>
        public IMcpServerMethods McpServers { get; protected set; } = null!;

        /// <summary>
        /// Tools discovered on registered MCP servers.
        /// </summary>
        public IMcpServerToolMethods McpServerTools { get; protected set; } = null!;

        /// <summary>
        /// Tool executions in agentic research runs.
        /// </summary>
        public IRunToolCallMethods RunToolCalls { get; protected set; } = null!;

        /// <summary>
        /// Email verification and password reset tokens.
        /// </summary>
        public IAccountTokenMethods AccountTokens { get; protected set; } = null!;

        /// <summary>
        /// Listening progress per user and briefing.
        /// </summary>
        public IBriefingProgressMethods BriefingProgress { get; protected set; } = null!;

        /// <summary>
        /// Embedding vectors (pgvector).
        /// </summary>
        public IEmbeddingMethods Embeddings { get; protected set; } = null!;

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
