namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// An address that asked not to receive a category of email (optionally for one topic).
    /// </summary>
    public class EmailSuppression
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix esp_).
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
        /// Address (lowercase).
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
        /// Category.
        /// </summary>
        public EmailCategoryEnum Category { get; set; } = EmailCategoryEnum.RunEmails;

        /// <summary>
        /// Topic, or null for every topic. May be null.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// How it was set (unsubscribe link, preferences, admin).
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Source
        {
            get
            {
                return _Source;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Source));
                _Source = value;
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

        private string _Id = IdGenerator.GenerateEmailSuppressionId();
        private string _TenantId = String.Empty;
        private string _Recipient = String.Empty;
        private string _Source = "link";

        #endregion
    }
}
