namespace MyMorningNews.Core.Models
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A proposed resolution of a prediction from new evidence; final only when accepted.
    /// </summary>
    public class PredictionReview
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix prv_).
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
        /// Prediction.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string PredictionId
        {
            get
            {
                return _PredictionId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(PredictionId));
                _PredictionId = value;
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
        /// Proposed status.
        /// </summary>
        public PredictionStatusEnum ProposedStatus { get; set; } = PredictionStatusEnum.Unresolvable;

        /// <summary>
        /// Rationale. May be null.
        /// </summary>
        public string? Rationale { get; set; } = null;

        /// <summary>
        /// Confidence 0 to 1.
        /// </summary>
        public double Confidence { get; set; } = 0.5;

        /// <summary>
        /// Accepted (true), rejected (false), or undecided (null). May be null.
        /// </summary>
        public bool? AcceptedByUser { get; set; } = null;

        /// <summary>
        /// Findings cited.
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

        private string _Id = IdGenerator.GeneratePredictionReviewId();
        private string _TenantId = String.Empty;
        private string _PredictionId = String.Empty;
        private string _RunId = String.Empty;
        private List<string> _FindingIds = new List<string>();

        #endregion
    }
}
