namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// An outbound email in the outbox: rendered once, delivered by the worker with retries, idempotent per key.
    /// </summary>
    public class EmailMessage
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix eml_).
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
        /// Recipient user, when the recipient has an account. May be null.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Template.
        /// </summary>
        public EmailTemplateEnum Template { get; set; } = EmailTemplateEnum.RunDigest;

        /// <summary>
        /// Run. May be null.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// Briefing. May be null.
        /// </summary>
        public string? BriefingId { get; set; } = null;

        /// <summary>
        /// Topic. May be null.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Recipient address (lowercase).
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Recipient
        {
            get
            {
                return _Recipient;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Recipient));
                _Recipient = value;
            }
        }

        /// <summary>
        /// Subject.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Subject
        {
            get
            {
                return _Subject;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Subject));
                _Subject = value;
            }
        }

        /// <summary>
        /// Status.
        /// </summary>
        public EmailStatusEnum Status { get; set; } = EmailStatusEnum.Queued;

        /// <summary>
        /// Provider used.
        /// </summary>
        public EmailProviderEnum Provider { get; set; } = EmailProviderEnum.File;

        /// <summary>
        /// Provider message ID. May be null.
        /// </summary>
        public string? ProviderMessageId { get; set; } = null;

        /// <summary>
        /// Delivery attempts.
        /// </summary>
        public int Attempts { get; set; } = 0;

        /// <summary>
        /// Next delivery attempt. May be null.
        /// </summary>
        public DateTime? NextAttemptUtc { get; set; } = null;

        /// <summary>
        /// Delivery claim expiry. May be null.
        /// </summary>
        public DateTime? LockedUntilUtc { get; set; } = null;

        /// <summary>
        /// Last delivery error. May be null.
        /// </summary>
        public string? LastError { get; set; } = null;

        /// <summary>
        /// Key that prevents duplicates, such as run:{runId}:{recipient}.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string IdempotencyKey
        {
            get
            {
                return _IdempotencyKey;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(IdempotencyKey));
                _IdempotencyKey = value;
            }
        }

        /// <summary>
        /// Rendered HTML (sent, and kept for preview until retention). May be null.
        /// </summary>
        public string? RenderedHtml { get; set; } = null;

        /// <summary>
        /// Plain-text rendering. May be null.
        /// </summary>
        public string? RenderedText { get; set; } = null;

        /// <summary>
        /// Sent. May be null.
        /// </summary>
        public DateTime? SentUtc { get; set; } = null;

        /// <summary>
        /// Failed for good. May be null.
        /// </summary>
        public DateTime? FailedUtc { get; set; } = null;

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

        private string _Id = IdGenerator.GenerateEmailMessageId();
        private string _TenantId = String.Empty;
        private string _Recipient = String.Empty;
        private string _Subject = String.Empty;
        private string _IdempotencyKey = String.Empty;

        #endregion
    }
}
