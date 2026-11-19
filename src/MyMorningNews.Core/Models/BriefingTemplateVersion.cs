namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// One saved version of a briefing template.
    /// </summary>
    public class BriefingTemplateVersion
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix btv_).
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
        /// Template.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string TemplateId
        {
            get
            {
                return _TemplateId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(TemplateId));
                _TemplateId = value;
            }
        }

        /// <summary>
        /// Version number, starting at 1.
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// Template Markdown.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Content
        {
            get
            {
                return _Content;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Content));
                _Content = value;
            }
        }

        /// <summary>
        /// What changed. May be null.
        /// </summary>
        public string? ChangeNote { get; set; } = null;

        /// <summary>
        /// Author. May be null.
        /// </summary>
        public string? CreatedByUserId { get; set; } = null;

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

        private string _Id = IdGenerator.GenerateBriefingTemplateVersionId();
        private string _TenantId = String.Empty;
        private string _TemplateId = String.Empty;
        private string _Content = String.Empty;

        #endregion
    }
}
