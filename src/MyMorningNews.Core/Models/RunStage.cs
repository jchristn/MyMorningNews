namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// One stage of a research run (plan section 16).
    /// </summary>
    public class RunStage
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix rst_).
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
        /// Stage name.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Stage
        {
            get
            {
                return _Stage;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Stage));
                _Stage = value;
            }
        }

        /// <summary>
        /// Status.
        /// </summary>
        public StageStatusEnum Status { get; set; } = StageStatusEnum.Pending;

        /// <summary>
        /// UTC start. May be null.
        /// </summary>
        public DateTime? StartedUtc { get; set; } = null;

        /// <summary>
        /// UTC end. May be null.
        /// </summary>
        public DateTime? CompletedUtc { get; set; } = null;

        /// <summary>
        /// Duration in milliseconds.
        /// </summary>
        public double DurationMs { get; set; } = 0;

        /// <summary>
        /// Items in.
        /// </summary>
        public int ItemsIn { get; set; } = 0;

        /// <summary>
        /// Items out.
        /// </summary>
        public int ItemsOut { get; set; } = 0;

        /// <summary>
        /// Error. May be null.
        /// </summary>
        public string? ErrorMessage { get; set; } = null;

        /// <summary>
        /// Order within the run.
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

        private string _Id = IdGenerator.GenerateRunStageId();
        private string _TenantId = String.Empty;
        private string _RunId = String.Empty;
        private string _Stage = String.Empty;

        #endregion
    }
}
