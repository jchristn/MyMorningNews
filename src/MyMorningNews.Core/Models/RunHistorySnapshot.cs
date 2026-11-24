namespace MyMorningNews.Core.Models
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Immutable bounded comparison context selected once for a research run and reused on retry.
    /// </summary>
    public class RunHistorySnapshot
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix rhs_).
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
        /// Research run.
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
        /// Topic owning the baseline.
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
        /// Off, PreviousIssue, RecentIssues, SinceDate, or SpecificIssue.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Mode
        {
            get
            {
                return _Mode;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Mode));
                _Mode = value;
            }
        }

        /// <summary>
        /// Selected issue IDs in deterministic order.
        /// </summary>
        public List<string> BaselineIds
        {
            get
            {
                return _BaselineIds;
            }
            set
            {
                _BaselineIds = value ?? new List<string>();
            }
        }

        /// <summary>
        /// Bounded comparison text, never treated as current evidence.
        /// </summary>
        public string ContextText
        {
            get
            {
                return _ContextText;
            }
            set
            {
                _ContextText = value ?? String.Empty;
            }
        }

        /// <summary>
        /// SHA-256 hash of mode, baseline IDs and context.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string ContextHash
        {
            get
            {
                return _ContextHash;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(ContextHash));
                _ContextHash = value;
            }
        }

        /// <summary>
        /// Fixed selection cutoff.
        /// </summary>
        public DateTime CutoffUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Context was truncated to its conservative token budget.
        /// </summary>
        public bool Truncated { get; set; } = false;

        /// <summary>
        /// First-run or missing-history explanation. May be null.
        /// </summary>
        public string? Note { get; set; } = null;

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

        private string _Id = IdGenerator.GenerateRunHistorySnapshotId();
        private string _TenantId = String.Empty;
        private string _RunId = String.Empty;
        private string _TopicId = String.Empty;
        private string _Mode = String.Empty;
        private List<string> _BaselineIds = new List<string>();
        private string _ContextText = String.Empty;
        private string _ContextHash = String.Empty;

        #endregion
    }
}
