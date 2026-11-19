namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A mobile device registered for push notifications.
    /// </summary>
    public class Device
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix dev_).
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
        /// Owning user.
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
        /// Expo push token.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string ExpoPushToken
        {
            get
            {
                return _ExpoPushToken;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(ExpoPushToken));
                _ExpoPushToken = value;
            }
        }

        /// <summary>
        /// Platform: ios or android.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Platform
        {
            get
            {
                return _Platform;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Platform));
                _Platform = value;
            }
        }

        /// <summary>
        /// Device name shown to the user. May be null.
        /// </summary>
        public string? Name { get; set; } = null;

        /// <summary>
        /// Device locale for notification text. May be null.
        /// </summary>
        public string? Locale { get; set; } = null;

        /// <summary>
        /// UTC time the device last registered or received a push. May be null.
        /// </summary>
        public DateTime? LastSeenUtc { get; set; } = null;

        /// <summary>
        /// Last push delivery error, when any. May be null.
        /// </summary>
        public string? LastError { get; set; } = null;

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

        private string _Id = IdGenerator.GenerateDeviceId();
        private string _TenantId = String.Empty;
        private string _UserId = String.Empty;
        private string _ExpoPushToken = String.Empty;
        private string _Platform = String.Empty;

        #endregion
    }
}
