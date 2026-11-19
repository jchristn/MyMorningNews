namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Tenant: the hard isolation boundary for all data.
    /// </summary>
    public class Tenant
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix ten_).
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
        /// Display name.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Name));
                _Name = value;
            }
        }

        /// <summary>
        /// Optional description. May be null.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Optional region label. May be null.
        /// </summary>
        public string? Region { get; set; } = null;

        /// <summary>
        /// Default IANA time zone for schedules and email.
        /// </summary>
        public string TimeZone
        {
            get
            {
                return _TimeZone;
            }
            set
            {
                _TimeZone = value ?? String.Empty;
            }
        }

        /// <summary>
        /// Protected records cannot be deleted through the API.
        /// </summary>
        public bool IsProtected { get; set; } = false;

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

        private string _Id = IdGenerator.GenerateTenantId();
        private string _Name = String.Empty;
        private string _TimeZone = "UTC";

        #endregion
    }
}
