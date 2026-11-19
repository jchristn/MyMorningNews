namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A chapter of an audio episode, aligned to a briefing section.
    /// </summary>
    public class MediaChapter
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix chp_).
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
        /// Audio.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string MediaAssetId
        {
            get
            {
                return _MediaAssetId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(MediaAssetId));
                _MediaAssetId = value;
            }
        }

        /// <summary>
        /// Section. May be null.
        /// </summary>
        public string? BriefingSectionId { get; set; } = null;

        /// <summary>
        /// Title.
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
        /// Start.
        /// </summary>
        public double StartSeconds { get; set; } = 0;

        /// <summary>
        /// End.
        /// </summary>
        public double EndSeconds { get; set; } = 0;

        /// <summary>
        /// Order.
        /// </summary>
        public int SortOrder { get; set; } = 0;

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

        private string _Id = IdGenerator.GenerateMediaChapterId();
        private string _TenantId = String.Empty;
        private string _MediaAssetId = String.Empty;
        private string _Title = String.Empty;

        #endregion
    }
}
