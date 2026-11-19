namespace MyMorningNews.Core.Models
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A facet of a topic that research searches, reads, and ranks against.
    /// </summary>
    public class TopicDimension
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix dim_).
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
        /// One-sentence description. May be null.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Why it might matter to the reader. May be null.
        /// </summary>
        public string? Rationale { get; set; } = null;

        /// <summary>
        /// Importance.
        /// </summary>
        public DimensionWeightEnum Weight { get; set; } = DimensionWeightEnum.Normal;

        /// <summary>
        /// Example include terms.
        /// </summary>
        public List<string> IncludeTerms
        {
            get
            {
                return _IncludeTerms;
            }
            set
            {
                _IncludeTerms = value ?? new List<string>();
            }
        }

        /// <summary>
        /// Example exclude terms.
        /// </summary>
        public List<string> ExcludeTerms
        {
            get
            {
                return _ExcludeTerms;
            }
            set
            {
                _ExcludeTerms = value ?? new List<string>();
            }
        }

        /// <summary>
        /// Locked by the reader; regeneration keeps it.
        /// </summary>
        public bool IsLocked { get; set; } = false;

        /// <summary>
        /// Who created it.
        /// </summary>
        public OriginEnum Origin { get; set; } = OriginEnum.Model;

        /// <summary>
        /// Display order.
        /// </summary>
        public int SortOrder { get; set; } = 0;

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

        private string _Id = IdGenerator.GenerateTopicDimensionId();
        private string _TenantId = String.Empty;
        private string _TopicId = String.Empty;
        private string _Name = String.Empty;
        private List<string> _IncludeTerms = new List<string>();
        private List<string> _ExcludeTerms = new List<string>();

        #endregion
    }
}
