namespace MyMorningNews.Core.Services
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// One model call through the gateway.
    /// </summary>
    public class ModelCallRequest
    {
        /// <summary>
        /// Tenant.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// User the call is attributed to for cost analytics (the caller, or the topic owner for scheduled work).
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Topic scope for model assignment resolution, when any.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Task role.
        /// </summary>
        public ModelTaskEnum Task { get; set; } = ModelTaskEnum.Synthesis;

        /// <summary>
        /// System prompt.
        /// </summary>
        public string? SystemPrompt { get; set; } = null;

        /// <summary>
        /// User prompt.
        /// </summary>
        public string UserPrompt { get; set; } = String.Empty;

        /// <summary>
        /// Prompt key, for the call log.
        /// </summary>
        public string? PromptKey { get; set; } = null;

        /// <summary>
        /// Prompt version, for the call log.
        /// </summary>
        public string? PromptTemplateVersionId { get; set; } = null;

        /// <summary>
        /// Research run, for the call log.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// Run stage, for the call log. Null takes the stage from <see cref="RunStageScope.Current"/>.
        /// </summary>
        public string? RunStageId { get; set; } = null;

        /// <summary>
        /// Refinement session, for the call log.
        /// </summary>
        public string? RefinementSessionId { get; set; } = null;

        /// <summary>
        /// Briefing, for the call log.
        /// </summary>
        public string? BriefingId { get; set; } = null;

        /// <summary>
        /// Endpoint override (prompt lab and tests).
        /// </summary>
        public string? ModelEndpointId { get; set; } = null;

        /// <summary>
        /// Model override.
        /// </summary>
        public string? Model { get; set; } = null;

        /// <summary>
        /// Maximum output tokens override.
        /// </summary>
        public int? MaxTokens { get; set; } = null;

        /// <summary>
        /// Whether the call is a dry run (prompt lab, preview).
        /// </summary>
        public bool IsDryRun { get; set; } = false;
    }
}
