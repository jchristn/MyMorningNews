namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Authenticated provider event inbox. Stores routing identifiers, never raw purchase tokens or card data.
    /// </summary>
    public class BillingEvent
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix bev_).
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
        /// Stripe, Apple, or Google.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Provider
        {
            get
            {
                return _Provider;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Provider));
                _Provider = value;
            }
        }

        /// <summary>
        /// Production or Sandbox.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Environment
        {
            get
            {
                return _Environment;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Environment));
                _Environment = value;
            }
        }

        /// <summary>
        /// Provider deduplication identifier.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string EventId
        {
            get
            {
                return _EventId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(EventId));
                _EventId = value;
            }
        }

        /// <summary>
        /// Subscription lineage or SHA-256 of a Google purchase token.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string ProviderId
        {
            get
            {
                return _ProviderId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(ProviderId));
                _ProviderId = value;
            }
        }

        /// <summary>
        /// Pending, Processing, or Completed.
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
        /// Worker attempts.
        /// </summary>
        public int Attempts { get; set; } = 0;

        /// <summary>
        /// Next retry or lease expiration.
        /// </summary>
        public DateTime RetryUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Sanitized reconciliation error. May be null.
        /// </summary>
        public string? Error { get; set; } = null;

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

        private string _Id = IdGenerator.GenerateBillingEventId();
        private string _Provider = String.Empty;
        private string _Environment = String.Empty;
        private string _EventId = String.Empty;
        private string _ProviderId = String.Empty;
        private string _Status = "Pending";

        #endregion
    }
}
