namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A Markdown briefing template with front matter and section directives (plan section 21.1).
    /// </summary>
    public class BriefingTemplate
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix btp_).
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
        /// Owner; null for a tenant template everyone can use. May be null.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Name.
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
        /// Description. May be null.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Key of the built-in default this template came from (morning-news, run-briefing), or null. May be null.
        /// </summary>
        public string? DefaultKey { get; set; } = null;

        /// <summary>
        /// Used for new editions and run briefings when nothing else is chosen.
        /// </summary>
        public bool IsDefault { get; set; } = false;

        /// <summary>
        /// Current version number.
        /// </summary>
        public int CurrentVersion { get; set; } = 1;

        /// <summary>
        /// Edited since it was seeded from a default.
        /// </summary>
        public bool Customized { get; set; } = false;

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

        private string _Id = IdGenerator.GenerateBriefingTemplateId();
        private string _TenantId = String.Empty;
        private string _Name = String.Empty;

        #endregion
    }
}
