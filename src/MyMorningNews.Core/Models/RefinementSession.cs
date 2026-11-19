namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A topic refinement wizard session. The topic exists from the start in Draft status and holds the evolving dimensions, constraints, and brief.
    /// </summary>
    public class RefinementSession
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix rfs_).
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
        /// Topic being refined.
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
        /// User.
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
        /// Status.
        /// </summary>
        public RefinementStatusEnum Status { get; set; } = RefinementStatusEnum.Active;

        /// <summary>
        /// Current step.
        /// </summary>
        public RefinementStepEnum Step { get; set; } = RefinementStepEnum.Describe;

        /// <summary>
        /// Question round.
        /// </summary>
        public int Round { get; set; } = 0;

        /// <summary>
        /// What the reader said they want to follow.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string SeedText
        {
            get
            {
                return _SeedText;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(SeedText));
                _SeedText = value;
            }
        }

        /// <summary>
        /// Why the reader wants it. May be null.
        /// </summary>
        public string? Goal { get; set; } = null;

        /// <summary>
        /// Depth (beginner to expert). May be null.
        /// </summary>
        public string? Depth { get; set; } = null;

        /// <summary>
        /// Horizon in the reader words. May be null.
        /// </summary>
        public string? HorizonText { get; set; } = null;

        /// <summary>
        /// What the reader already knows. May be null.
        /// </summary>
        public string? Known { get; set; } = null;

        /// <summary>
        /// The model judged further questions unnecessary.
        /// </summary>
        public bool SufficientContext { get; set; } = false;

        /// <summary>
        /// Why the request is not a topic to follow, when the model said so. May be null.
        /// </summary>
        public string? OutOfScopeReason { get; set; } = null;

        /// <summary>
        /// Model endpoint override for this session. May be null.
        /// </summary>
        public string? ModelEndpointId { get; set; } = null;

        /// <summary>
        /// Last generation error. May be null.
        /// </summary>
        public string? LastError { get; set; } = null;

        /// <summary>
        /// UTC completion time. May be null.
        /// </summary>
        public DateTime? CompletedUtc { get; set; } = null;

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

        private string _Id = IdGenerator.GenerateRefinementSessionId();
        private string _TenantId = String.Empty;
        private string _TopicId = String.Empty;
        private string _UserId = String.Empty;
        private string _SeedText = String.Empty;

        #endregion
    }
}
