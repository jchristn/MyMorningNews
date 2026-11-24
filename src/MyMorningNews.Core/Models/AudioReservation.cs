namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Durable daily audio reservation and settlement keyed by logical generation operation.
    /// </summary>
    public class AudioReservation
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix arv_).
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
        /// Logical media generation ID.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string OperationId
        {
            get
            {
                return _OperationId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(OperationId));
                _OperationId = value;
            }
        }

        /// <summary>
        /// UTC day against which generation is reserved.
        /// </summary>
        public DateTime PeriodStartUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Maximum publishable duration held for this operation.
        /// </summary>
        public long ReservedSeconds { get; set; } = 0;

        /// <summary>
        /// Actual published duration, rounded up to milliseconds.
        /// </summary>
        public long CommittedMilliseconds { get; set; } = 0;

        /// <summary>
        /// Reserved, Committed, or Released.
        /// </summary>
        public string Status
        {
            get
            {
                return _Status;
            }
            set
            {
                _Status = value ?? String.Empty;
            }
        }

        /// <summary>
        /// Lease expiration; recovery checks the job before releasing.
        /// </summary>
        public DateTime ExpiresUtc { get; set; } = DateTime.UtcNow;

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

        private string _Id = IdGenerator.GenerateAudioReservationId();
        private string _TenantId = String.Empty;
        private string _UserId = String.Empty;
        private string _OperationId = String.Empty;
        private string _Status = "Reserved";

        #endregion
    }
}
