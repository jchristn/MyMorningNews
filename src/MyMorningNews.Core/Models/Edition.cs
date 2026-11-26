namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A recurring briefing across topics with its own schedule, template, and delivery (plan section 21.3).
    /// </summary>
    public class Edition
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix edn_).
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
        /// Name.
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
        /// Description. May be null.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Template; null uses the tenant default. May be null.
        /// </summary>
        public string? BriefingTemplateId { get; set; } = null;

        /// <summary>
        /// Voice for audio (Phase 8). May be null.
        /// </summary>
        public string? VoiceProfileId { get; set; } = null;

        /// <summary>
        /// Produce audio.
        /// </summary>
        public bool AudioEnabled { get; set; } = false;

        /// <summary>
        /// Email the briefing.
        /// </summary>
        public bool EmailEnabled { get; set; } = true;

        /// <summary>
        /// Compose on schedule.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Run the topics first, then compose when they finish.
        /// </summary>
        public bool FreshResearchBeforeCompose { get; set; } = true;

        /// <summary>
        /// Longest wait for fresh research before composing anyway (5 to 240). Minimum 5, maximum 240; values outside the range are clamped.
        /// </summary>
        public int FreshResearchLeadMinutes
        {
            get
            {
                return _FreshResearchLeadMinutes;
            }
            set
            {
                _FreshResearchLeadMinutes = Math.Clamp(value, 5, 240);
            }
        }

        /// <summary>
        /// Which findings are eligible.
        /// </summary>
        public LookbackModeEnum LookbackMode { get; set; } = LookbackModeEnum.SinceLastEdition;

        /// <summary>
        /// Hours to look back for FixedHours, and the cap for SinceLastEdition (1 to 2160). Minimum 1, maximum 2160; values outside the range are clamped.
        /// </summary>
        public int LookbackHours
        {
            get
            {
                return _LookbackHours;
            }
            set
            {
                _LookbackHours = Math.Clamp(value, 1, 2160);
            }
        }

        /// <summary>
        /// Last successful composition. May be null.
        /// </summary>
        public DateTime? LastComposedUtc { get; set; } = null;

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
        /// Send a push notification to the owner's devices when the edition is ready. Default true.
        /// </summary>
        public bool PushEnabled { get; set; } = true;

        /// <summary>
        /// Comparison of each composed edition with earlier issues of this edition. Default Off, which keeps the topics'
        /// own research comparisons in the composition instructions. Any other mode compares the edition with its own
        /// earlier issues instead.
        /// </summary>
        public HistoryModeEnum HistoryMode { get; set; } = HistoryModeEnum.Off;

        /// <summary>
        /// Maximum baseline editions for RecentIssues. Default 5, minimum 1, maximum 20. Validated before writes.
        /// </summary>
        public int HistoryCount { get; set; } = 5;

        /// <summary>
        /// Inclusive earliest baseline time for SinceDate, otherwise null.
        /// </summary>
        public DateTime? HistorySinceUtc { get; set; } = null;

        /// <summary>
        /// Explicit baseline for SpecificIssue: a ready issue of this edition. Null otherwise.
        /// </summary>
        public string? HistoryBriefingId { get; set; } = null;

        /// <summary>
        /// Conservative comparison budget in UTF-8 bytes. Default 8000, minimum 256, maximum 32000.
        /// </summary>
        public int HistoryTokenBudget { get; set; } = 8000;

        #endregion

        #region Private-Members

        private string _Id = IdGenerator.GenerateEditionId();
        private string _TenantId = String.Empty;
        private string _UserId = String.Empty;
        private string _Name = String.Empty;
        private int _FreshResearchLeadMinutes = 45;
        private int _LookbackHours = 24;

        #endregion
    }
}
