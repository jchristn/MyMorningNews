namespace MyMorningNews.Core.Models
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A research topic: what a reader wants to follow, with its brief, dimensions, constraints, and delivery options.
    /// </summary>
    public class Topic
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix top_).
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Id
        {
            get
            {
                return _Id;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Id));
                _Id = value;
            }
        }

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string TenantId
        {
            get
            {
                return _TenantId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(TenantId));
                _TenantId = value;
            }
        }

        /// <summary>
        /// Owning user.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string UserId
        {
            get
            {
                return _UserId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(UserId));
                _UserId = value;
            }
        }

        /// <summary>
        /// Display name.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Name));
                _Name = value;
            }
        }

        /// <summary>
        /// URL-friendly name, unique per tenant.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Slug
        {
            get
            {
                return _Slug;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Slug));
                _Slug = value;
            }
        }

        /// <summary>
        /// Short description. May be null.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Research brief in Markdown, user-editable. May be null.
        /// </summary>
        public string? Brief { get; set; } = null;

        /// <summary>
        /// Reader expertise.
        /// </summary>
        public AudienceLevelEnum AudienceLevel { get; set; } = AudienceLevelEnum.Practitioner;

        /// <summary>
        /// Time horizon of interest.
        /// </summary>
        public HorizonEnum Horizon { get; set; } = HorizonEnum.Weeks;

        /// <summary>
        /// Language code for research and output.
        /// </summary>
        public string Language
        {
            get
            {
                return _Language;
            }
            set
            {
                _Language = value ?? String.Empty;
            }
        }

        /// <summary>
        /// Lifecycle status.
        /// </summary>
        public TopicStatusEnum Status { get; set; } = TopicStatusEnum.Draft;

        /// <summary>
        /// Created for a one-time request rather than recurring research.
        /// </summary>
        public bool IsOneTime { get; set; } = false;

        /// <summary>
        /// 0 repeats important items, 100 shows only new items. Minimum 0, maximum 100; values outside the range are clamped.
        /// </summary>
        public int NoveltyPreference
        {
            get
            {
                return _NoveltyPreference;
            }
            set
            {
                _NoveltyPreference = Math.Clamp(value, 0, 100);
            }
        }

        /// <summary>
        /// Email a summary after every run.
        /// </summary>
        public bool EmailOnRun { get; set; } = true;

        /// <summary>
        /// Email even when a run finds nothing above the threshold.
        /// </summary>
        public bool EmailWhenEmpty { get; set; } = true;

        /// <summary>
        /// Maximum findings listed in the run email. Minimum 1, maximum 50; values outside the range are clamped.
        /// </summary>
        public int MaxEmailFindings
        {
            get
            {
                return _MaxEmailFindings;
            }
            set
            {
                _MaxEmailFindings = Math.Clamp(value, 1, 50);
            }
        }

        /// <summary>
        /// Relevance threshold override (0 to 100); null uses the tenant default. May be null.
        /// </summary>
        public int? DisplayThreshold { get; set; } = null;

        /// <summary>
        /// Ranking weight override for model judgment. May be null.
        /// </summary>
        public double? WeightLlm { get; set; } = null;

        /// <summary>
        /// Ranking weight override for feedback. May be null.
        /// </summary>
        public double? WeightFeedback { get; set; } = null;

        /// <summary>
        /// Ranking weight override for novelty. May be null.
        /// </summary>
        public double? WeightNovelty { get; set; } = null;

        /// <summary>
        /// Ranking weight override for source reputation. May be null.
        /// </summary>
        public double? WeightSource { get; set; } = null;

        /// <summary>
        /// Ranking weight override for recency. May be null.
        /// </summary>
        public double? WeightRecency { get; set; } = null;

        /// <summary>
        /// Seed search queries from refinement.
        /// </summary>
        public List<string> SeedQueries
        {
            get
            {
                return _SeedQueries;
            }
            set
            {
                _SeedQueries = value ?? new List<string>();
            }
        }

        /// <summary>
        /// Topic-specific briefing template. May be null.
        /// </summary>
        public string? BriefingTemplateId { get; set; } = null;

        /// <summary>
        /// UTC time of the last run. May be null.
        /// </summary>
        public DateTime? LastRunUtc { get; set; } = null;

        /// <summary>
        /// UTC time the topic was last refined. May be null.
        /// </summary>
        public DateTime? LastRefinedUtc { get; set; } = null;

        /// <summary>
        /// Whether the record is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// UTC creation time.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC time of the last update.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Agentic search: when the research model supports tools, it searches and reads pages itself (web_search,
        /// fetch_url, and allowed MCP tools) before the usual reading and verification. Opt-in, default false.
        /// </summary>
        public bool AgenticSearch { get; set; } = false;

        /// <summary>
        /// Voice profile that reads this topic's run briefings aloud. Null uses the workspace default voice.
        /// </summary>
        public string? VoiceProfileId { get; set; } = null;

        /// <summary>
        /// MCP tool IDs (mct_...) this topic may use in agentic search. Empty allows every enabled tool.
        /// </summary>
        public List<string> McpToolIds
        {
            get
            {
                return _McpToolIds;
            }
            set
            {
                _McpToolIds = value ?? new List<string>();
            }
        }

        #endregion

        #region Private-Members

        private List<string> _McpToolIds = new List<string>();

        private string _Id = IdGenerator.GenerateTopicId();
        private string _TenantId = String.Empty;
        private string _UserId = String.Empty;
        private string _Name = String.Empty;
        private string _Slug = String.Empty;
        private string _Language = "en";
        private int _NoveltyPreference = 50;
        private int _MaxEmailFindings = 10;
        private List<string> _SeedQueries = new List<string>();

        #endregion
    }
}
