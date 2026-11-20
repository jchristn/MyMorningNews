namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Create or update a topic. Null fields are left unchanged on update.
    /// </summary>
    public class TopicUpsertRequest
    {
        /// <summary>
        /// Name.
        /// </summary>
        public string? Name { get; set; } = null;

        /// <summary>
        /// Description.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Research brief (Markdown).
        /// </summary>
        public string? Brief { get; set; } = null;

        /// <summary>
        /// Audience level.
        /// </summary>
        public AudienceLevelEnum? AudienceLevel { get; set; } = null;

        /// <summary>
        /// Horizon.
        /// </summary>
        public HorizonEnum? Horizon { get; set; } = null;

        /// <summary>
        /// Language code.
        /// </summary>
        public string? Language { get; set; } = null;

        /// <summary>
        /// Status (Draft, Active, Paused, Archived).
        /// </summary>
        public TopicStatusEnum? Status { get; set; } = null;

        /// <summary>
        /// Novelty preference, 0 to 100.
        /// </summary>
        public int? NoveltyPreference { get; set; } = null;

        /// <summary>
        /// Email after every run.
        /// </summary>
        public bool? EmailOnRun { get; set; } = null;

        /// <summary>
        /// Email when nothing is found.
        /// </summary>
        public bool? EmailWhenEmpty { get; set; } = null;

        /// <summary>
        /// Maximum findings per email, 1 to 50.
        /// </summary>
        public int? MaxEmailFindings { get; set; } = null;

        /// <summary>
        /// Relevance threshold override, 0 to 100.
        /// </summary>
        public int? DisplayThreshold { get; set; } = null;

        /// <summary>
        /// Ranking weight override for the llm component (0 to 1); a negative value clears the override.
        /// </summary>
        public double? WeightLlm { get; set; } = null;

        /// <summary>
        /// Ranking weight override for the feedback component (0 to 1); a negative value clears the override.
        /// </summary>
        public double? WeightFeedback { get; set; } = null;

        /// <summary>
        /// Ranking weight override for the novelty component (0 to 1); a negative value clears the override.
        /// </summary>
        public double? WeightNovelty { get; set; } = null;

        /// <summary>
        /// Ranking weight override for the source component (0 to 1); a negative value clears the override.
        /// </summary>
        public double? WeightSource { get; set; } = null;

        /// <summary>
        /// Ranking weight override for the recency component (0 to 1); a negative value clears the override.
        /// </summary>
        public double? WeightRecency { get; set; } = null;

        /// <summary>
        /// Turn agentic search on or off for this topic (the research model searches and reads pages itself).
        /// </summary>
        public bool? AgenticSearch { get; set; } = null;

        /// <summary>
        /// MCP tool IDs (mct_...) agentic search may use; an empty list allows every enabled tool. Null leaves it.
        /// </summary>
        public List<string>? McpToolIds { get; set; } = null;

        /// <summary>
        /// Seed search queries.
        /// </summary>
        public List<string>? SeedQueries { get; set; } = null;
    }
}
