namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A ranked research finding backed by verified evidence.
    /// </summary>
    public class Finding
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix fnd_).
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
        /// Run.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string RunId
        {
            get
            {
                return _RunId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(RunId));
                _RunId = value;
            }
        }

        /// <summary>
        /// Topic.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string TopicId
        {
            get
            {
                return _TopicId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(TopicId));
                _TopicId = value;
            }
        }

        /// <summary>
        /// Topic owner.
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
        /// Dimension. May be null.
        /// </summary>
        public string? DimensionId { get; set; } = null;

        /// <summary>
        /// Primary source. May be null.
        /// </summary>
        public string? SourceItemId { get; set; } = null;

        /// <summary>
        /// Title.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Title
        {
            get
            {
                return _Title;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Title));
                _Title = value;
            }
        }

        /// <summary>
        /// Summary. May be null.
        /// </summary>
        public string? Summary { get; set; } = null;

        /// <summary>
        /// Why it matters to the reader. May be null.
        /// </summary>
        public string? WhyItMatters { get; set; } = null;

        /// <summary>
        /// Category.
        /// </summary>
        public FindingCategoryEnum Category { get; set; } = FindingCategoryEnum.News;

        /// <summary>
        /// Composite relevance 0 to 100.
        /// </summary>
        public double RelevanceScore { get; set; } = 0;

        /// <summary>
        /// Model judgment 0 to 100.
        /// </summary>
        public double LlmScore { get; set; } = 0;

        /// <summary>
        /// Feedback similarity 0 to 100.
        /// </summary>
        public double FeedbackScore { get; set; } = 50;

        /// <summary>
        /// Novelty 0 to 100.
        /// </summary>
        public double NoveltyScore { get; set; } = 50;

        /// <summary>
        /// Source reputation 0 to 100.
        /// </summary>
        public double SourceScore { get; set; } = 50;

        /// <summary>
        /// Recency 0 to 100.
        /// </summary>
        public double RecencyScore { get; set; } = 50;

        /// <summary>
        /// Tier.
        /// </summary>
        public FindingTierEnum Tier { get; set; } = FindingTierEnum.Fyi;

        /// <summary>
        /// Why it scored as it did. May be null.
        /// </summary>
        public string? RelevanceRationale { get; set; } = null;

        /// <summary>
        /// Model confidence 0 to 1.
        /// </summary>
        public double Confidence { get; set; } = 0.5;

        /// <summary>
        /// Duplicate of an earlier finding.
        /// </summary>
        public bool IsDuplicate { get; set; } = false;

        /// <summary>
        /// Earlier finding it duplicates. May be null.
        /// </summary>
        public string? DuplicateOfFindingId { get; set; } = null;

        /// <summary>
        /// Earlier finding it updates. May be null.
        /// </summary>
        public string? IsUpdateOfFindingId { get; set; } = null;

        /// <summary>
        /// Rank within the run.
        /// </summary>
        public int Rank { get; set; } = 0;

        /// <summary>
        /// Latest reader rating. May be null.
        /// </summary>
        public string? UserRating { get; set; } = null;

        /// <summary>
        /// Source publication time. May be null.
        /// </summary>
        public DateTime? PublishedUtc { get; set; } = null;

        /// <summary>
        /// From a dry run.
        /// </summary>
        public bool IsDryRun { get; set; } = false;

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

        #endregion

        #region Private-Members

        private string _Id = IdGenerator.GenerateFindingId();
        private string _TenantId = String.Empty;
        private string _RunId = String.Empty;
        private string _TopicId = String.Empty;
        private string _UserId = String.Empty;
        private string _Title = String.Empty;

        #endregion
    }
}
