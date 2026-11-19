namespace MyMorningNews.Core.Models
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Something worth digging into, linked to findings.
    /// </summary>
    public class Suggestion
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix sug_).
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
        /// Description. May be null.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Rationale. May be null.
        /// </summary>
        public string? Rationale { get; set; } = null;

        /// <summary>
        /// Priority.
        /// </summary>
        public SuggestionPriorityEnum Priority { get; set; } = SuggestionPriorityEnum.Medium;

        /// <summary>
        /// Effort.
        /// </summary>
        public SuggestionEffortEnum Effort { get; set; } = SuggestionEffortEnum.Hours;

        /// <summary>
        /// Status.
        /// </summary>
        public SuggestionStatusEnum Status { get; set; } = SuggestionStatusEnum.New;

        /// <summary>
        /// Snoozed until. May be null.
        /// </summary>
        public DateTime? SnoozedUntilUtc { get; set; } = null;

        /// <summary>
        /// Last status change. May be null.
        /// </summary>
        public DateTime? StatusChangedUtc { get; set; } = null;

        /// <summary>
        /// Findings it builds on.
        /// </summary>
        public List<string> FindingIds
        {
            get
            {
                return _FindingIds;
            }
            set
            {
                _FindingIds = value ?? new List<string>();
            }
        }

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

        private string _Id = IdGenerator.GenerateSuggestionId();
        private string _TenantId = String.Empty;
        private string _RunId = String.Empty;
        private string _TopicId = String.Empty;
        private string _UserId = String.Empty;
        private string _Title = String.Empty;
        private List<string> _FindingIds = new List<string>();

        #endregion
    }
}
