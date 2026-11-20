namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Classified request type; each maps to a required (resource, operation) pair in OperationScopeMap.
    /// </summary>
    public enum RequestTypeEnum
    {
        /// <summary>
        /// Unclassified request.
        /// </summary>
        Unknown,

        /// <summary>
        /// Read health.
        /// </summary>
        HealthRead,

        /// <summary>
        /// Create a session token.
        /// </summary>
        TokenCreate,

        /// <summary>
        /// Validate or inspect the current token.
        /// </summary>
        TokenRead,

        /// <summary>
        /// Refresh the current token.
        /// </summary>
        TokenRefresh,

        /// <summary>
        /// Revoke the current token.
        /// </summary>
        TokenDelete,

        /// <summary>
        /// Read tenants.
        /// </summary>
        TenantRead,

        /// <summary>
        /// Create or update tenants.
        /// </summary>
        TenantWrite,

        /// <summary>
        /// Delete tenants.
        /// </summary>
        TenantDelete,

        /// <summary>
        /// Read users.
        /// </summary>
        UserRead,

        /// <summary>
        /// Create or update users.
        /// </summary>
        UserWrite,

        /// <summary>
        /// Delete users.
        /// </summary>
        UserDelete,

        /// <summary>
        /// Read own profile.
        /// </summary>
        UserSelfRead,

        /// <summary>
        /// Update own profile or password.
        /// </summary>
        UserSelfWrite,

        /// <summary>
        /// Read credentials.
        /// </summary>
        CredentialRead,

        /// <summary>
        /// Create or update credentials.
        /// </summary>
        CredentialWrite,

        /// <summary>
        /// Delete credentials.
        /// </summary>
        CredentialDelete,

        /// <summary>
        /// Read roles.
        /// </summary>
        RoleRead,

        /// <summary>
        /// Create or update roles.
        /// </summary>
        RoleWrite,

        /// <summary>
        /// Delete roles.
        /// </summary>
        RoleDelete,

        /// <summary>
        /// Read permissions.
        /// </summary>
        PermissionRead,

        /// <summary>
        /// Create or update permissions.
        /// </summary>
        PermissionWrite,

        /// <summary>
        /// Delete permissions.
        /// </summary>
        PermissionDelete,

        /// <summary>
        /// Read role assignments.
        /// </summary>
        AssignmentRead,

        /// <summary>
        /// Create or update role assignments.
        /// </summary>
        AssignmentWrite,

        /// <summary>
        /// Delete role assignments.
        /// </summary>
        AssignmentDelete,

        /// <summary>
        /// Inspect effective permissions.
        /// </summary>
        EffectivePermissionRead,

        /// <summary>
        /// Read audit records.
        /// </summary>
        AuditRead,

        /// <summary>
        /// Delete audit records.
        /// </summary>
        AuditDelete,

        /// <summary>
        /// Read request history.
        /// </summary>
        RequestHistoryRead,

        /// <summary>
        /// Delete request history.
        /// </summary>
        RequestHistoryDelete,

        /// <summary>
        /// Read server settings.
        /// </summary>
        SettingsRead,

        /// <summary>
        /// Update server settings.
        /// </summary>
        SettingsWrite,

        /// <summary>
        /// Restart the server.
        /// </summary>
        ServerRestart,

        /// <summary>
        /// Read server information.
        /// </summary>
        ServerInfoRead,

        /// <summary>
        /// Read vault credential metadata.
        /// </summary>
        VaultRead,

        /// <summary>
        /// Create, update, or rotate vault credentials.
        /// </summary>
        VaultWrite,

        /// <summary>
        /// Delete vault credentials.
        /// </summary>
        VaultDelete,

        /// <summary>
        /// Reveal a vault secret.
        /// </summary>
        VaultReveal,

        /// <summary>
        /// Read model endpoints and assignments.
        /// </summary>
        ModelRead,

        /// <summary>
        /// Create or update model endpoints and assignments.
        /// </summary>
        ModelWrite,

        /// <summary>
        /// Delete model endpoints.
        /// </summary>
        ModelDelete,

        /// <summary>
        /// Validate, list, or test models.
        /// </summary>
        ModelExecute,

        /// <summary>
        /// Read prompt templates.
        /// </summary>
        PromptRead,

        /// <summary>
        /// Create, update, roll back, or reset prompt templates.
        /// </summary>
        PromptWrite,

        /// <summary>
        /// Delete prompt overrides.
        /// </summary>
        PromptDelete,

        /// <summary>
        /// Run a prompt in the prompt lab.
        /// </summary>
        PromptExecute,

        /// <summary>
        /// Read model call logs.
        /// </summary>
        LlmCallRead,

        /// <summary>
        /// Read model usage and cost analytics.
        /// </summary>
        UsageRead,

        /// <summary>
        /// List my push devices.
        /// </summary>
        DeviceRead,

        /// <summary>
        /// Register, test, or remove my push devices.
        /// </summary>
        DeviceWrite,

        /// <summary>
        /// Read findings and evidence.
        /// </summary>
        FindingRead,

        /// <summary>
        /// Pin, hide, or rate findings.
        /// </summary>
        FindingWrite,

        /// <summary>
        /// Delete findings.
        /// </summary>
        FindingDelete,

        /// <summary>
        /// Read predictions.
        /// </summary>
        PredictionRead,

        /// <summary>
        /// Resolve predictions and accept reviews.
        /// </summary>
        PredictionWrite,

        /// <summary>
        /// Read suggestions.
        /// </summary>
        SuggestionRead,

        /// <summary>
        /// Change suggestion status.
        /// </summary>
        SuggestionWrite,

        /// <summary>
        /// Read fetched sources.
        /// </summary>
        SourceRead,

        /// <summary>
        /// Read schedules.
        /// </summary>
        ScheduleRead,

        /// <summary>
        /// Create, update, or delete schedules.
        /// </summary>
        ScheduleWrite,

        /// <summary>
        /// Read runs.
        /// </summary>
        RunRead,

        /// <summary>
        /// Start or cancel runs.
        /// </summary>
        RunExecute,

        /// <summary>
        /// Read search providers.
        /// </summary>
        SearchProviderRead,

        /// <summary>
        /// Manage search providers.
        /// </summary>
        SearchProviderWrite,

        /// <summary>
        /// Read topics.
        /// </summary>
        TopicRead,

        /// <summary>
        /// Create or update topics and their dimensions, constraints, and recipients.
        /// </summary>
        TopicWrite,

        /// <summary>
        /// Delete topics.
        /// </summary>
        TopicDelete,

        /// <summary>
        /// Read refinement sessions.
        /// </summary>
        RefinementRead,

        /// <summary>
        /// Run the refinement wizard.
        /// </summary>
        RefinementWrite,

        /// <summary>
        /// Read one-time requests.
        /// </summary>
        ResearchRequestRead,

        /// <summary>
        /// Create, confirm, or convert one-time requests.
        /// </summary>
        ResearchRequestWrite,

        /// <summary>
        /// Delete one-time requests.
        /// </summary>
        ResearchRequestDelete,

        /// <summary>
        /// Read briefings and download reports.
        /// </summary>
        BriefingRead,

        /// <summary>
        /// Compose, pin, regenerate, or delete briefings.
        /// </summary>
        BriefingWrite,

        /// <summary>
        /// Read editions.
        /// </summary>
        EditionRead,

        /// <summary>
        /// Create, update, or delete editions.
        /// </summary>
        EditionWrite,

        /// <summary>
        /// Read briefing templates.
        /// </summary>
        TemplateRead,

        /// <summary>
        /// Create, version, or delete briefing templates.
        /// </summary>
        TemplateWrite,

        /// <summary>
        /// Read stored files.
        /// </summary>
        ArtifactRead,

        /// <summary>
        /// Pin or delete stored files.
        /// </summary>
        ArtifactWrite,

        /// <summary>
        /// Read and stream audio.
        /// </summary>
        MediaRead,

        /// <summary>
        /// Generate or delete audio.
        /// </summary>
        MediaWrite,

        /// <summary>
        /// Read voice profiles.
        /// </summary>
        VoiceRead,

        /// <summary>
        /// Create, update, test, or delete voice profiles.
        /// </summary>
        VoiceWrite,

        /// <summary>
        /// List my podcast feed tokens.
        /// </summary>
        FeedTokenRead,

        /// <summary>
        /// Create or revoke podcast feed tokens.
        /// </summary>
        FeedTokenWrite,

        /// <summary>
        /// Read the email outbox.
        /// </summary>
        EmailRead,

        /// <summary>
        /// Resend, delete, or test email.
        /// </summary>
        EmailWrite,

        /// <summary>
        /// Read feedback.
        /// </summary>
        FeedbackRead,

        /// <summary>
        /// Give feedback.
        /// </summary>
        FeedbackWrite,

        /// <summary>
        /// Read learned preferences.
        /// </summary>
        PreferenceRead,

        /// <summary>
        /// Write, accept, reject, or disable learned preferences.
        /// </summary>
        PreferenceWrite,

        /// <summary>
        /// Read relevance, accuracy, feedback, and suggestion analytics.
        /// </summary>
        AnalyticsRead,

        /// <summary>
        /// Manage promotional codes, plans, and plan assignments (global administrators).
        /// </summary>
        PromoCodeAdmin,

        /// <summary>
        /// Redeem a promotional code and read my plan.
        /// </summary>
        PromoCodeRedeem
    }
}
