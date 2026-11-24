namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// An in-app notification for one user, with the item it points to.
    /// </summary>
    public class Notification
    {
        /// <summary>System publication for a shared briefing notification. Null for private content.</summary>
        public string? CatalogPublicationId { get; set; } = null;
        #region Public-Members

        /// <summary>
        /// Identifier (prefix ntf_).
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
        /// User the notification is for.
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
        /// What happened.
        /// </summary>
        public NotificationKindEnum Kind { get; set; } = NotificationKindEnum.RunResults;

        /// <summary>
        /// What it is about, for example the topic or briefing title.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Title
        {
            get
            {
                return _Title;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Title));
                _Title = value;
            }
        }

        /// <summary>
        /// A short sentence. May be null.
        /// </summary>
        public string? Body { get; set; } = null;

        /// <summary>
        /// Topic to open. May be null.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Run to open. May be null.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// Briefing to open. May be null.
        /// </summary>
        public string? BriefingId { get; set; } = null;

        /// <summary>
        /// Audio to open. May be null.
        /// </summary>
        public string? MediaAssetId { get; set; } = null;

        /// <summary>
        /// Prediction to open. May be null.
        /// </summary>
        public string? PredictionId { get; set; } = null;

        /// <summary>
        /// UTC time the user read it; null while unread. May be null.
        /// </summary>
        public DateTime? ReadUtc { get; set; } = null;

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

        private string _Id = IdGenerator.GenerateNotificationId();
        private string _TenantId = String.Empty;
        private string _UserId = String.Empty;
        private string _Title = String.Empty;

        #endregion
    }
}
