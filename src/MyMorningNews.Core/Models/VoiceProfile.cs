namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A text-to-speech voice: provider, endpoint, voices for narrator and two-host formats, and speed.
    /// </summary>
    public class VoiceProfile
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix voi_).
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
        /// Provider.
        /// </summary>
        public TtsProviderEnum Provider { get; set; } = TtsProviderEnum.OpenAiCompatibleSpeech;

        /// <summary>
        /// Base URL; null uses Tts.Endpoint. May be null.
        /// </summary>
        public string? Endpoint { get; set; } = null;

        /// <summary>
        /// Speech model.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Model
        {
            get
            {
                return _Model;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Model));
                _Model = value;
            }
        }

        /// <summary>
        /// Narrator voice, and HOST_A in two-host scripts.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Voice
        {
            get
            {
                return _Voice;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Voice));
                _Voice = value;
            }
        }

        /// <summary>
        /// HOST_B voice for two-host scripts; null uses the narrator voice. May be null.
        /// </summary>
        public string? SecondaryVoice { get; set; } = null;

        /// <summary>
        /// Speaking speed (0.5 to 2.0). Minimum 0.5, maximum 2.0; values outside the range are clamped.
        /// </summary>
        public double Speed
        {
            get
            {
                return _Speed;
            }
            set
            {
                _Speed = Math.Clamp(value, 0.5, 2.0);
            }
        }

        /// <summary>
        /// Vault credential with the API key, or null. May be null.
        /// </summary>
        public string? CredentialId { get; set; } = null;

        /// <summary>
        /// Used when an edition names no voice.
        /// </summary>
        public bool IsDefault { get; set; } = false;

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

        private string _Id = IdGenerator.GenerateVoiceProfileId();
        private string _TenantId = String.Empty;
        private string _Name = String.Empty;
        private string _Model = "kokoro";
        private string _Voice = "af_heart";
        private double _Speed = 1.0;

        #endregion
    }
}
