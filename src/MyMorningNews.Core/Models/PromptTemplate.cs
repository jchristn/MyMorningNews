namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Editable, versioned prompt used by one pipeline task. Tenant-wide, or overridden for one topic.
    /// </summary>
    public class PromptTemplate
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix pmt_).
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
        /// Prompt key, for example research.score.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Key
        {
            get
            {
                return _Key;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Key));
                _Key = value;
            }
        }

        /// <summary>
        /// Topic override; null for the tenant prompt. May be null.
        /// </summary>
        public string? TopicId { get; set; } = null;

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
        /// What the prompt does. May be null.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Task role that runs this prompt.
        /// </summary>
        public ModelTaskEnum Role { get; set; } = ModelTaskEnum.Synthesis;

        /// <summary>
        /// Structured result type name, or empty for text. May be null.
        /// </summary>
        public string? OutputType { get; set; } = null;

        /// <summary>
        /// Current version number.
        /// </summary>
        public int CurrentVersion { get; set; } = 1;

        /// <summary>
        /// Whether the content differs from the shipped default.
        /// </summary>
        public bool IsCustomized { get; set; } = false;

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

        private string _Id = IdGenerator.GeneratePromptTemplateId();
        private string _TenantId = String.Empty;
        private string _Key = String.Empty;
        private string _Name = String.Empty;

        #endregion
    }
}
