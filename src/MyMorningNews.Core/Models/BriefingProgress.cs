namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Where a user stopped listening to a briefing, and whether they finished it.
    /// </summary>
    public class BriefingProgress
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix bpr_).
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
        /// Owning user identifier.
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
        /// Briefing.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string BriefingId
        {
            get
            {
                return _BriefingId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(BriefingId));
                _BriefingId = value;
            }
        }

        /// <summary>
        /// Playback position in seconds. Minimum 0, maximum 86400; values outside the range are clamped.
        /// </summary>
        public double PositionSeconds
        {
            get
            {
                return _PositionSeconds;
            }
            set
            {
                _PositionSeconds = Math.Clamp(value, 0, 86400);
            }
        }

        /// <summary>
        /// Episode length in seconds when known. May be null.
        /// </summary>
        public double? DurationSeconds { get; set; } = null;

        /// <summary>
        /// Whether the user finished listening.
        /// </summary>
        public bool Completed { get; set; } = false;

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

        private string _Id = IdGenerator.GenerateBriefingProgressId();
        private string _TenantId = String.Empty;
        private string _UserId = String.Empty;
        private string _BriefingId = String.Empty;
        private double _PositionSeconds = 0;

        #endregion
    }
}
