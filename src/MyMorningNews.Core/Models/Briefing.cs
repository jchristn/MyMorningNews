namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A composed briefing: an edition across topics, a single run, or a one-time report.
    /// </summary>
    public class Briefing
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix brf_).
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
        /// Owner.
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
        /// Edition, run, or one-time report.
        /// </summary>
        public BriefingKindEnum Kind { get; set; } = BriefingKindEnum.Edition;

        /// <summary>
        /// Edition. May be null.
        /// </summary>
        public string? EditionId { get; set; } = null;

        /// <summary>
        /// Topic, for run and one-time briefings. May be null.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Run, for run and one-time briefings. May be null.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// One-time request. May be null.
        /// </summary>
        public string? ResearchRequestId { get; set; } = null;

        /// <summary>
        /// Schedule that triggered it. May be null.
        /// </summary>
        public string? ScheduleId { get; set; } = null;

        /// <summary>
        /// Start of the covered window. May be null.
        /// </summary>
        public DateTime? PeriodStartUtc { get; set; } = null;

        /// <summary>
        /// End of the covered window. May be null.
        /// </summary>
        public DateTime? PeriodEndUtc { get; set; } = null;

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
        /// Composed Markdown. May be null.
        /// </summary>
        public string? Markdown { get; set; } = null;

        /// <summary>
        /// Status.
        /// </summary>
        public BriefingStatusEnum Status { get; set; } = BriefingStatusEnum.Queued;

        /// <summary>
        /// Earliest compose time. May be null.
        /// </summary>
        public DateTime? ComposeAfterUtc { get; set; } = null;

        /// <summary>
        /// Compose by this time even if fresh research is still running. May be null.
        /// </summary>
        public DateTime? ResearchDeadlineUtc { get; set; } = null;

        /// <summary>
        /// Composition claim expiry. May be null.
        /// </summary>
        public DateTime? LockedUntilUtc { get; set; } = null;

        /// <summary>
        /// Template version used. May be null.
        /// </summary>
        public string? BriefingTemplateVersionId { get; set; } = null;

        /// <summary>
        /// Audio (Phase 8). May be null.
        /// </summary>
        public string? MediaAssetId { get; set; } = null;

        /// <summary>
        /// Stored Markdown file. May be null.
        /// </summary>
        public string? MarkdownArtifactId { get; set; } = null;

        /// <summary>
        /// Stored HTML report. May be null.
        /// </summary>
        public string? HtmlArtifactId { get; set; } = null;

        /// <summary>
        /// Findings, predictions, and suggestions included.
        /// </summary>
        public int ItemCount { get; set; } = 0;

        /// <summary>
        /// Words.
        /// </summary>
        public int WordCount { get; set; } = 0;

        /// <summary>
        /// Estimated reading minutes.
        /// </summary>
        public int ReadMinutes { get; set; } = 0;

        /// <summary>
        /// Sections written without a model because the model was unavailable.
        /// </summary>
        public int ModelFallbacks { get; set; } = 0;

        /// <summary>
        /// Failure reason. May be null.
        /// </summary>
        public string? ErrorMessage { get; set; } = null;

        /// <summary>
        /// Kept past the retention window.
        /// </summary>
        public bool Pinned { get; set; } = false;

        /// <summary>
        /// Preview or dry run.
        /// </summary>
        public bool IsDryRun { get; set; } = false;

        /// <summary>
        /// When composition finished. May be null.
        /// </summary>
        public DateTime? ComposedUtc { get; set; } = null;

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

        private string _Id = IdGenerator.GenerateBriefingId();
        private string _TenantId = String.Empty;
        private string _UserId = String.Empty;
        private string _Title = String.Empty;

        #endregion
    }
}
