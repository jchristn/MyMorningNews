namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A falsifiable prediction with a confidence and a horizon, graded later.
    /// </summary>
    public class Prediction
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix prd_).
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
        /// Statement.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Statement
        {
            get
            {
                return _Statement;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Statement));
                _Statement = value;
            }
        }

        /// <summary>
        /// Rationale. May be null.
        /// </summary>
        public string? Rationale { get; set; } = null;

        /// <summary>
        /// Confidence 0 to 1.
        /// </summary>
        public double Confidence { get; set; } = 0.5;

        /// <summary>
        /// When it should be judged.
        /// </summary>
        public DateTime HorizonUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Status.
        /// </summary>
        public PredictionStatusEnum Status { get; set; } = PredictionStatusEnum.Open;

        /// <summary>
        /// When resolved. May be null.
        /// </summary>
        public DateTime? ResolvedUtc { get; set; } = null;

        /// <summary>
        /// User, Agent, or AgentConfirmed. May be null.
        /// </summary>
        public string? ResolvedBy { get; set; } = null;

        /// <summary>
        /// Note. May be null.
        /// </summary>
        public string? ResolutionNote { get; set; } = null;

        /// <summary>
        /// Brier score once resolved. May be null.
        /// </summary>
        public double? BrierScore { get; set; } = null;

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

        private string _Id = IdGenerator.GeneratePredictionId();
        private string _TenantId = String.Empty;
        private string _RunId = String.Empty;
        private string _TopicId = String.Empty;
        private string _UserId = String.Empty;
        private string _Statement = String.Empty;

        #endregion
    }
}
