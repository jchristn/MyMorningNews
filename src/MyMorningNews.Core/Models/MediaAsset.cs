namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// An audio episode made from a briefing: script, synthesis status, stored audio, and chapters.
    /// </summary>
    public class MediaAsset
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix med_).
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
        /// Owner.
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
        /// Briefing.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string BriefingId
        {
            get
            {
                return _BriefingId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(BriefingId));
                _BriefingId = value;
            }
        }

        /// <summary>
        /// Edition, for the podcast feed. May be null.
        /// </summary>
        public string? EditionId { get; set; } = null;

        /// <summary>
        /// Kind.
        /// </summary>
        public MediaKindEnum Kind { get; set; } = MediaKindEnum.BriefingAudio;

        /// <summary>
        /// Status.
        /// </summary>
        public MediaStatusEnum Status { get; set; } = MediaStatusEnum.Queued;

        /// <summary>
        /// narrator or two-host.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Format
        {
            get
            {
                return _Format;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Format));
                _Format = value;
            }
        }

        /// <summary>
        /// Voice used. May be null.
        /// </summary>
        public string? VoiceProfileId { get; set; } = null;

        /// <summary>
        /// Episode title.
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
        /// Spoken script. May be null.
        /// </summary>
        public string? ScriptMarkdown { get; set; } = null;

        /// <summary>
        /// Written by the model (false: written from the briefing text without a model).
        /// </summary>
        public bool ScriptFromModel { get; set; } = false;

        /// <summary>
        /// Stored audio. May be null.
        /// </summary>
        public string? ArtifactId { get; set; } = null;

        /// <summary>
        /// Stored chapters JSON. May be null.
        /// </summary>
        public string? ChaptersArtifactId { get; set; } = null;

        /// <summary>
        /// Audio media type. May be null.
        /// </summary>
        public string? ContentType { get; set; } = null;

        /// <summary>
        /// Audio size.
        /// </summary>
        public long Bytes { get; set; } = 0;

        /// <summary>
        /// Duration.
        /// </summary>
        public double DurationSeconds { get; set; } = 0;

        /// <summary>
        /// Speech segments synthesized.
        /// </summary>
        public int SegmentCount { get; set; } = 0;

        /// <summary>
        /// Attempts.
        /// </summary>
        public int Attempts { get; set; } = 0;

        /// <summary>
        /// Worker claim expiry. May be null.
        /// </summary>
        public DateTime? LockedUntilUtc { get; set; } = null;

        /// <summary>
        /// Failure reason. May be null.
        /// </summary>
        public string? ErrorMessage { get; set; } = null;

        /// <summary>
        /// When the audio was ready. May be null.
        /// </summary>
        public DateTime? CompletedUtc { get; set; } = null;

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

        private string _Id = IdGenerator.GenerateMediaAssetId();
        private string _TenantId = String.Empty;
        private string _UserId = String.Empty;
        private string _BriefingId = String.Empty;
        private string _Format = "narrator";
        private string _Title = String.Empty;

        #endregion
    }
}
