namespace MyMorningNews.Core.Models
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A link token in an email: feedback on one item or unsubscribing from one category. Only the SHA-256 hash is stored.
    /// </summary>
    public class EmailActionToken
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix eat_).
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
        /// User the feedback is attributed to (the topic owner when the recipient has no account).
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
        /// Email.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string EmailMessageId
        {
            get
            {
                return _EmailMessageId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(EmailMessageId));
                _EmailMessageId = value;
            }
        }

        /// <summary>
        /// Feedback or unsubscribe.
        /// </summary>
        public ActionTokenKindEnum Kind { get; set; } = ActionTokenKindEnum.Feedback;

        /// <summary>
        /// Item type, for feedback tokens.
        /// </summary>
        public FeedbackTargetTypeEnum TargetType { get; set; } = FeedbackTargetTypeEnum.Finding;

        /// <summary>
        /// Item ID, or for unsubscribe the category and optional topic (RunEmails:top_x).
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string TargetId
        {
            get
            {
                return _TargetId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(TargetId));
                _TargetId = value;
            }
        }

        /// <summary>
        /// Topic. May be null.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Recipient address.
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
        /// Actions this token allows.
        /// </summary>
        public List<string> AllowedActions
        {
            get
            {
                return _AllowedActions;
            }
            set
            {
                _AllowedActions = value ?? new List<string>();
            }
        }

        /// <summary>
        /// SHA-256 of the token, lowercase hex.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string TokenHash
        {
            get
            {
                return _TokenHash;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(TokenHash));
                _TokenHash = value;
            }
        }

        /// <summary>
        /// Expiry.
        /// </summary>
        public DateTime ExpiresUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Revoked. May be null.
        /// </summary>
        public DateTime? RevokedUtc { get; set; } = null;

        /// <summary>
        /// Landing page views.
        /// </summary>
        public int OpenCount { get; set; } = 0;

        /// <summary>
        /// Submissions.
        /// </summary>
        public int SubmitCount { get; set; } = 0;

        /// <summary>
        /// Last view or submission. May be null.
        /// </summary>
        public DateTime? LastUsedUtc { get; set; } = null;

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

        private string _Id = IdGenerator.GenerateEmailActionTokenId();
        private string _TenantId = String.Empty;
        private string _UserId = String.Empty;
        private string _EmailMessageId = String.Empty;
        private string _TargetId = String.Empty;
        private string _Recipient = String.Empty;
        private List<string> _AllowedActions = new List<string>();
        private string _TokenHash = String.Empty;

        #endregion
    }
}
