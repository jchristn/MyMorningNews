namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// One saved version of a prompt template.
    /// </summary>
    public class PromptTemplateVersion
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix pmv_).
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
        /// Prompt template.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string PromptTemplateId
        {
            get
            {
                return _PromptTemplateId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(PromptTemplateId));
                _PromptTemplateId = value;
            }
        }

        /// <summary>
        /// Version number.
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// Prompt Markdown including front matter.
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
        /// Change note. May be null.
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

        private string _Id = IdGenerator.GeneratePromptTemplateVersionId();
        private string _TenantId = String.Empty;
        private string _PromptTemplateId = String.Empty;
        private string _Content = String.Empty;

        #endregion
    }
}
