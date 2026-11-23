namespace MyMorningNews.Core.Pipeline
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Models.Results;

    /// <summary>
    /// State shared by the stages of one run.
    /// Thread safety: not thread safe; stages that work in parallel synchronize their own writes.
    /// </summary>
    public class RunContext
    {
        /// <summary>
        /// Run.
        /// </summary>
        public Run Run { get; set; } = new Run();

        /// <summary>
        /// Topic.
        /// </summary>
        public Topic Topic { get; set; } = new Topic();

        /// <summary>
        /// Topic dimensions.
        /// </summary>
        public List<TopicDimension> Dimensions { get; set; } = new List<TopicDimension>();

        /// <summary>
        /// Topic constraints.
        /// </summary>
        public List<TopicConstraint> Constraints { get; set; } = new List<TopicConstraint>();

        /// <summary>
        /// Search providers for the topic.
        /// </summary>
        public List<SearchProvider> Providers { get; set; } = new List<SearchProvider>();

        /// <summary>
        /// Current time at run start (UTC).
        /// </summary>
        public DateTime NowUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Previous run time, for "since" filters.
        /// </summary>
        public DateTime? SinceUtc { get; set; } = null;

        /// <summary>
        /// Prompt variables shared by every stage (topic, history, date).
        /// </summary>
        public Dictionary<string, object?> BaseValues { get; set; } = new Dictionary<string, object?>();

        /// <summary>
        /// Canonical URL hashes among this run's search results that a finished, non-dry run of the topic already fetched.
        /// Set by the Filter stage with a query for exactly this run's hashes, so no history size limit applies.
        /// </summary>
        public HashSet<string> SeenUrlHashes { get; set; } = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Recent run summaries, newest first.
        /// </summary>
        public List<string> RecentSummaries { get; set; } = new List<string>();

        /// <summary>
        /// Planned queries with their dimension.
        /// </summary>
        public List<PlannedQuery> PlannedQueries { get; set; } = new List<PlannedQuery>();

        /// <summary>
        /// Search results of this run.
        /// </summary>
        public List<SearchResult> Results { get; set; } = new List<SearchResult>();

        /// <summary>
        /// Results selected for fetching.
        /// </summary>
        public List<SearchResult> Selected { get; set; } = new List<SearchResult>();

        /// <summary>
        /// Sources fetched or reused with text.
        /// </summary>
        public List<SourceItem> Sources { get; set; } = new List<SourceItem>();

        /// <summary>
        /// Candidate findings.
        /// </summary>
        public List<Candidate> Candidates { get; set; } = new List<Candidate>();

        /// <summary>
        /// Embedding model used, when embedding succeeded.
        /// </summary>
        public string? EmbeddingModel { get; set; } = null;

        /// <summary>
        /// Embedding endpoint used.
        /// </summary>
        public string? EmbeddingEndpointId { get; set; } = null;

        /// <summary>
        /// Saved, non-duplicate findings ranked by relevance.
        /// </summary>
        public List<Finding> Findings { get; set; } = new List<Finding>();

        /// <summary>
        /// Run summary from Synthesize.
        /// </summary>
        public string? Summary { get; set; } = null;

        /// <summary>
        /// Stage notes (fallbacks used), for the run summary.
        /// </summary>
        public List<string> Notes { get; set; } = new List<string>();

        /// <summary>
        /// Whether a stage finished without the result it was meant to produce but the run can still complete, for
        /// example Fetch reading none of the selected pages. The run ends as PartiallySucceeded. Default false.
        /// </summary>
        public bool PartiallySucceeded { get; set; } = false;

        /// <summary>
        /// Briefing composed for the run, or null.
        /// </summary>
        public string? BriefingId { get; set; } = null;

        /// <summary>
        /// Whether a digest email was queued.
        /// </summary>
        public bool Delivered { get; set; } = false;
    }
}
