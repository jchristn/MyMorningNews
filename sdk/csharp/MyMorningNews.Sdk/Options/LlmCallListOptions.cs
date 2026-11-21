namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;

    /// <summary>
    /// Filters for listing model calls.
    /// </summary>
    public class LlmCallListOptions : ListOptions
    {
        /// <summary>
        /// Only calls of this run. Null by default.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// Only calls made in this run stage. Null by default.
        /// </summary>
        public string? RunStageId { get; set; } = null;

        /// <summary>
        /// Only calls of this refinement session. Null by default.
        /// </summary>
        public string? RefinementSessionId { get; set; } = null;

        /// <summary>
        /// Only calls of this briefing. Null by default.
        /// </summary>
        public string? BriefingId { get; set; } = null;

        /// <summary>
        /// Only calls to this endpoint. Null by default.
        /// </summary>
        public string? ModelEndpointId { get; set; } = null;

        /// <summary>
        /// Only calls to this model. Null by default.
        /// </summary>
        public string? Model { get; set; } = null;

        /// <summary>
        /// Only calls with this prompt key. Null by default.
        /// </summary>
        public string? PromptKey { get; set; } = null;

        /// <summary>
        /// Only calls attributed to this user. Null by default.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Only calls for this topic. Null by default.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Only successful (true) or failed (false) calls. Null by default.
        /// </summary>
        public bool? Success { get; set; } = null;

        /// <summary>
        /// Only dry-run (true) or real (false) calls. Null by default.
        /// </summary>
        public bool? IsDryRun { get; set; } = null;

        /// <summary>
        /// Only calls for this task. Null by default.
        /// </summary>
        public ModelTaskEnum? Task { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public LlmCallListOptions()
        {
        }

        /// <summary>
        /// Add this resource's filters.
        /// </summary>
        /// <param name="query">Query to add to.</param>
        protected override void AddFilters(Dictionary<string, string> query)
        {
            base.AddFilters(query);
            QueryFormat.Add(query, "runId", RunId);
            QueryFormat.Add(query, "runStageId", RunStageId);
            QueryFormat.Add(query, "refinementSessionId", RefinementSessionId);
            QueryFormat.Add(query, "briefingId", BriefingId);
            QueryFormat.Add(query, "modelEndpointId", ModelEndpointId);
            QueryFormat.Add(query, "model", Model);
            QueryFormat.Add(query, "promptKey", PromptKey);
            QueryFormat.Add(query, "userId", UserId);
            QueryFormat.Add(query, "topicId", TopicId);
            QueryFormat.Add(query, "success", Success);
            QueryFormat.Add(query, "isDryRun", IsDryRun);
            QueryFormat.Add(query, "task", Task);
        }
    }
}
