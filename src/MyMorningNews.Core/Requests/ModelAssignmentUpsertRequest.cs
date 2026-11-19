namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Assign a task role to a model endpoint.
    /// </summary>
    public class ModelAssignmentUpsertRequest
    {
        /// <summary>
        /// Task role.
        /// </summary>
        public ModelTaskEnum Task { get; set; } = ModelTaskEnum.Synthesis;

        /// <summary>
        /// Topic override; null for the tenant default.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Model endpoint.
        /// </summary>
        public string ModelEndpointId { get; set; } = String.Empty;

        /// <summary>
        /// Model name override.
        /// </summary>
        public string? ModelOverride { get; set; } = null;

        /// <summary>
        /// Reasoning effort.
        /// </summary>
        public ReasoningEffortEnum? ReasoningEffort { get; set; } = null;
    }
}
