namespace MyMorningNews.Core.Security
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Single source of truth mapping each request type to the permission it requires. Adding a request type without
    /// a mapping fails at startup (see <see cref="Validate"/>).
    /// </summary>
    public static class OperationScopeMap
    {
        #region Private-Members

        private static readonly Dictionary<RequestTypeEnum, RequiredPermission> _Map = new Dictionary<RequestTypeEnum, RequiredPermission>
        {
            { RequestTypeEnum.Unknown, new RequiredPermission(ResourceTypeEnum.Admin, OperationTypeEnum.Admin) },
            { RequestTypeEnum.HealthRead, new RequiredPermission(ResourceTypeEnum.All, OperationTypeEnum.Read, anonymous: true) },
            { RequestTypeEnum.TokenCreate, new RequiredPermission(ResourceTypeEnum.Session, OperationTypeEnum.Create, anonymous: true) },
            { RequestTypeEnum.TokenRead, new RequiredPermission(ResourceTypeEnum.Session, OperationTypeEnum.Read, anyAuthenticated: true) },
            { RequestTypeEnum.TokenRefresh, new RequiredPermission(ResourceTypeEnum.Session, OperationTypeEnum.Update, anyAuthenticated: true) },
            { RequestTypeEnum.TokenDelete, new RequiredPermission(ResourceTypeEnum.Session, OperationTypeEnum.Delete, anyAuthenticated: true) },
            { RequestTypeEnum.TenantRead, new RequiredPermission(ResourceTypeEnum.Tenant, OperationTypeEnum.Read) },
            { RequestTypeEnum.TenantWrite, new RequiredPermission(ResourceTypeEnum.Admin, OperationTypeEnum.Admin) },
            { RequestTypeEnum.TenantDelete, new RequiredPermission(ResourceTypeEnum.Admin, OperationTypeEnum.Admin) },
            { RequestTypeEnum.UserRead, new RequiredPermission(ResourceTypeEnum.User, OperationTypeEnum.Read) },
            { RequestTypeEnum.UserWrite, new RequiredPermission(ResourceTypeEnum.Admin, OperationTypeEnum.Admin) },
            { RequestTypeEnum.UserDelete, new RequiredPermission(ResourceTypeEnum.Admin, OperationTypeEnum.Admin) },
            { RequestTypeEnum.UserSelfRead, new RequiredPermission(ResourceTypeEnum.User, OperationTypeEnum.Read, anyAuthenticated: true) },
            { RequestTypeEnum.UserSelfWrite, new RequiredPermission(ResourceTypeEnum.User, OperationTypeEnum.Update, anyAuthenticated: true) },
            { RequestTypeEnum.DeviceRead, new RequiredPermission(ResourceTypeEnum.User, OperationTypeEnum.Read, anyAuthenticated: true) },
            { RequestTypeEnum.TopicRead, new RequiredPermission(ResourceTypeEnum.Topic, OperationTypeEnum.Read) },
            { RequestTypeEnum.TopicWrite, new RequiredPermission(ResourceTypeEnum.Topic, OperationTypeEnum.Update) },
            { RequestTypeEnum.TopicDelete, new RequiredPermission(ResourceTypeEnum.Topic, OperationTypeEnum.Delete) },
            { RequestTypeEnum.RefinementRead, new RequiredPermission(ResourceTypeEnum.RefinementSession, OperationTypeEnum.Read) },
            { RequestTypeEnum.RefinementWrite, new RequiredPermission(ResourceTypeEnum.RefinementSession, OperationTypeEnum.Execute) },
            { RequestTypeEnum.ResearchRequestRead, new RequiredPermission(ResourceTypeEnum.Topic, OperationTypeEnum.Read) },
            { RequestTypeEnum.ResearchRequestWrite, new RequiredPermission(ResourceTypeEnum.Topic, OperationTypeEnum.Update) },
            { RequestTypeEnum.ResearchRequestDelete, new RequiredPermission(ResourceTypeEnum.Topic, OperationTypeEnum.Delete) },
            { RequestTypeEnum.ScheduleRead, new RequiredPermission(ResourceTypeEnum.Schedule, OperationTypeEnum.Read) },
            { RequestTypeEnum.ScheduleWrite, new RequiredPermission(ResourceTypeEnum.Schedule, OperationTypeEnum.Update) },
            { RequestTypeEnum.RunRead, new RequiredPermission(ResourceTypeEnum.Run, OperationTypeEnum.Read) },
            { RequestTypeEnum.RunExecute, new RequiredPermission(ResourceTypeEnum.Run, OperationTypeEnum.Execute) },
            { RequestTypeEnum.SearchProviderRead, new RequiredPermission(ResourceTypeEnum.SearchProvider, OperationTypeEnum.Read) },
            { RequestTypeEnum.SearchProviderWrite, new RequiredPermission(ResourceTypeEnum.SearchProvider, OperationTypeEnum.Admin) },
            { RequestTypeEnum.FindingRead, new RequiredPermission(ResourceTypeEnum.Finding, OperationTypeEnum.Read) },
            { RequestTypeEnum.FindingWrite, new RequiredPermission(ResourceTypeEnum.Finding, OperationTypeEnum.Update) },
            { RequestTypeEnum.PredictionRead, new RequiredPermission(ResourceTypeEnum.Prediction, OperationTypeEnum.Read) },
            { RequestTypeEnum.PredictionWrite, new RequiredPermission(ResourceTypeEnum.Prediction, OperationTypeEnum.Update) },
            { RequestTypeEnum.SuggestionRead, new RequiredPermission(ResourceTypeEnum.Suggestion, OperationTypeEnum.Read) },
            { RequestTypeEnum.SuggestionWrite, new RequiredPermission(ResourceTypeEnum.Suggestion, OperationTypeEnum.Update) },
            { RequestTypeEnum.SourceRead, new RequiredPermission(ResourceTypeEnum.Source, OperationTypeEnum.Read) },
            { RequestTypeEnum.BriefingRead, new RequiredPermission(ResourceTypeEnum.Briefing, OperationTypeEnum.Read) },
            { RequestTypeEnum.BriefingWrite, new RequiredPermission(ResourceTypeEnum.Briefing, OperationTypeEnum.Update) },
            { RequestTypeEnum.EditionRead, new RequiredPermission(ResourceTypeEnum.Edition, OperationTypeEnum.Read) },
            { RequestTypeEnum.EditionWrite, new RequiredPermission(ResourceTypeEnum.Edition, OperationTypeEnum.Update) },
            { RequestTypeEnum.TemplateRead, new RequiredPermission(ResourceTypeEnum.Template, OperationTypeEnum.Read) },
            { RequestTypeEnum.TemplateWrite, new RequiredPermission(ResourceTypeEnum.Template, OperationTypeEnum.Update) },
            { RequestTypeEnum.ArtifactRead, new RequiredPermission(ResourceTypeEnum.Briefing, OperationTypeEnum.Read) },
            { RequestTypeEnum.ArtifactWrite, new RequiredPermission(ResourceTypeEnum.Briefing, OperationTypeEnum.Update) },
            { RequestTypeEnum.MediaRead, new RequiredPermission(ResourceTypeEnum.Media, OperationTypeEnum.Read) },
            { RequestTypeEnum.MediaWrite, new RequiredPermission(ResourceTypeEnum.Media, OperationTypeEnum.Update) },
            { RequestTypeEnum.VoiceRead, new RequiredPermission(ResourceTypeEnum.Voice, OperationTypeEnum.Read) },
            { RequestTypeEnum.VoiceWrite, new RequiredPermission(ResourceTypeEnum.Voice, OperationTypeEnum.Update) },
            { RequestTypeEnum.FeedTokenRead, new RequiredPermission(ResourceTypeEnum.Media, OperationTypeEnum.Read) },
            { RequestTypeEnum.FeedTokenWrite, new RequiredPermission(ResourceTypeEnum.Media, OperationTypeEnum.Update) },
            { RequestTypeEnum.EmailRead, new RequiredPermission(ResourceTypeEnum.EmailMessage, OperationTypeEnum.Read) },
            { RequestTypeEnum.EmailWrite, new RequiredPermission(ResourceTypeEnum.EmailMessage, OperationTypeEnum.Update) },
            { RequestTypeEnum.FeedbackRead, new RequiredPermission(ResourceTypeEnum.Feedback, OperationTypeEnum.Read) },
            { RequestTypeEnum.FeedbackWrite, new RequiredPermission(ResourceTypeEnum.Feedback, OperationTypeEnum.Create) },
            { RequestTypeEnum.PreferenceRead, new RequiredPermission(ResourceTypeEnum.LearnedPreference, OperationTypeEnum.Read) },
            { RequestTypeEnum.PreferenceWrite, new RequiredPermission(ResourceTypeEnum.LearnedPreference, OperationTypeEnum.Update) },
            { RequestTypeEnum.AnalyticsRead, new RequiredPermission(ResourceTypeEnum.Analytics, OperationTypeEnum.Read) },
            { RequestTypeEnum.DeviceWrite, new RequiredPermission(ResourceTypeEnum.User, OperationTypeEnum.Update, anyAuthenticated: true) },
            { RequestTypeEnum.CredentialRead, new RequiredPermission(ResourceTypeEnum.Credential, OperationTypeEnum.Read) },
            { RequestTypeEnum.CredentialWrite, new RequiredPermission(ResourceTypeEnum.Admin, OperationTypeEnum.Admin) },
            { RequestTypeEnum.CredentialDelete, new RequiredPermission(ResourceTypeEnum.Admin, OperationTypeEnum.Admin) },
            { RequestTypeEnum.RoleRead, new RequiredPermission(ResourceTypeEnum.Role, OperationTypeEnum.Read) },
            { RequestTypeEnum.RoleWrite, new RequiredPermission(ResourceTypeEnum.Admin, OperationTypeEnum.Admin) },
            { RequestTypeEnum.RoleDelete, new RequiredPermission(ResourceTypeEnum.Admin, OperationTypeEnum.Admin) },
            { RequestTypeEnum.PermissionRead, new RequiredPermission(ResourceTypeEnum.Permission, OperationTypeEnum.Read) },
            { RequestTypeEnum.PermissionWrite, new RequiredPermission(ResourceTypeEnum.Admin, OperationTypeEnum.Admin) },
            { RequestTypeEnum.PermissionDelete, new RequiredPermission(ResourceTypeEnum.Admin, OperationTypeEnum.Admin) },
            { RequestTypeEnum.AssignmentRead, new RequiredPermission(ResourceTypeEnum.Assignment, OperationTypeEnum.Read) },
            { RequestTypeEnum.AssignmentWrite, new RequiredPermission(ResourceTypeEnum.Admin, OperationTypeEnum.Admin) },
            { RequestTypeEnum.AssignmentDelete, new RequiredPermission(ResourceTypeEnum.Admin, OperationTypeEnum.Admin) },
            { RequestTypeEnum.EffectivePermissionRead, new RequiredPermission(ResourceTypeEnum.Admin, OperationTypeEnum.Admin) },
            { RequestTypeEnum.AuditRead, new RequiredPermission(ResourceTypeEnum.Audit, OperationTypeEnum.Read) },
            { RequestTypeEnum.AuditDelete, new RequiredPermission(ResourceTypeEnum.Admin, OperationTypeEnum.Admin) },
            { RequestTypeEnum.RequestHistoryRead, new RequiredPermission(ResourceTypeEnum.RequestHistory, OperationTypeEnum.Read) },
            { RequestTypeEnum.RequestHistoryDelete, new RequiredPermission(ResourceTypeEnum.Admin, OperationTypeEnum.Admin) },
            { RequestTypeEnum.SettingsRead, new RequiredPermission(ResourceTypeEnum.Admin, OperationTypeEnum.Admin) },
            { RequestTypeEnum.SettingsWrite, new RequiredPermission(ResourceTypeEnum.Admin, OperationTypeEnum.Admin) },
            { RequestTypeEnum.ServerRestart, new RequiredPermission(ResourceTypeEnum.Admin, OperationTypeEnum.Admin) },
            { RequestTypeEnum.ServerInfoRead, new RequiredPermission(ResourceTypeEnum.All, OperationTypeEnum.Read, anyAuthenticated: true) },
            { RequestTypeEnum.VaultRead, new RequiredPermission(ResourceTypeEnum.VaultCredential, OperationTypeEnum.Read) },
            { RequestTypeEnum.VaultWrite, new RequiredPermission(ResourceTypeEnum.VaultCredential, OperationTypeEnum.Admin) },
            { RequestTypeEnum.VaultDelete, new RequiredPermission(ResourceTypeEnum.VaultCredential, OperationTypeEnum.Admin) },
            { RequestTypeEnum.VaultReveal, new RequiredPermission(ResourceTypeEnum.VaultCredential, OperationTypeEnum.Admin) },
            { RequestTypeEnum.ModelRead, new RequiredPermission(ResourceTypeEnum.ModelEndpoint, OperationTypeEnum.Read) },
            { RequestTypeEnum.ModelWrite, new RequiredPermission(ResourceTypeEnum.ModelEndpoint, OperationTypeEnum.Update) },
            { RequestTypeEnum.ModelDelete, new RequiredPermission(ResourceTypeEnum.ModelEndpoint, OperationTypeEnum.Delete) },
            { RequestTypeEnum.ModelExecute, new RequiredPermission(ResourceTypeEnum.ModelEndpoint, OperationTypeEnum.Execute) },
            { RequestTypeEnum.PromptRead, new RequiredPermission(ResourceTypeEnum.Prompt, OperationTypeEnum.Read) },
            { RequestTypeEnum.PromptWrite, new RequiredPermission(ResourceTypeEnum.Prompt, OperationTypeEnum.Update) },
            { RequestTypeEnum.PromptDelete, new RequiredPermission(ResourceTypeEnum.Prompt, OperationTypeEnum.Delete) },
            { RequestTypeEnum.PromptExecute, new RequiredPermission(ResourceTypeEnum.Prompt, OperationTypeEnum.Execute) },
            { RequestTypeEnum.LlmCallRead, new RequiredPermission(ResourceTypeEnum.LlmCall, OperationTypeEnum.Read) },
            { RequestTypeEnum.UsageRead, new RequiredPermission(ResourceTypeEnum.Usage, OperationTypeEnum.Read) },
        };

        #endregion

        #region Public-Methods

        /// <summary>
        /// Required permission for a request type.
        /// </summary>
        /// <param name="requestType">Request type.</param>
        /// <returns>Required permission.</returns>
        /// <exception cref="KeyNotFoundException">The request type has no mapping.</exception>
        public static RequiredPermission Get(RequestTypeEnum requestType)
        {
            if (_Map.TryGetValue(requestType, out RequiredPermission? required)) return required;
            throw new KeyNotFoundException("Request type " + requestType + " has no permission mapping in OperationScopeMap.");
        }

        /// <summary>
        /// Register or replace a mapping. Used by feature modules added in later phases.
        /// </summary>
        /// <param name="requestType">Request type.</param>
        /// <param name="required">Required permission.</param>
        /// <exception cref="ArgumentNullException">required is null.</exception>
        public static void Register(RequestTypeEnum requestType, RequiredPermission required)
        {
            ArgumentNullException.ThrowIfNull(required);
            lock (_Map)
            {
                _Map[requestType] = required;
            }
        }

        /// <summary>
        /// Verify every request type has a mapping.
        /// </summary>
        /// <exception cref="InvalidOperationException">At least one request type is unmapped.</exception>
        public static void Validate()
        {
            List<string> missing = new List<string>();
            foreach (RequestTypeEnum value in Enum.GetValues<RequestTypeEnum>())
            {
                if (!_Map.ContainsKey(value)) missing.Add(value.ToString());
            }

            if (missing.Count > 0) throw new InvalidOperationException("Request types without permission mappings: " + String.Join(", ", missing));
        }

        #endregion
    }
}
