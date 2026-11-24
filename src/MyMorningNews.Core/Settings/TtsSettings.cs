namespace MyMorningNews.Core.Settings
{
    using System;

    /// <summary>
    /// Text-to-speech and audio production (plan section 22).
    /// </summary>
    public class TtsSettings
    {
        #region Public-Members

        /// <summary>Allow BYOK accounts to use the operator's local speech engine without a speech key. Default false; enable only for a locally funded engine.</summary>
        [Setting(Description = "Share the configured local speech engine with BYOK accounts.")]
        public bool AllowLocalForByok { get; set; } = false;

        /// <summary>
        /// Produce audio for briefings. Default true.
        /// </summary>
        [Setting(Description = "Produce audio for briefings.", EnvironmentVariable = "TTS_ENABLED")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Default OpenAI-compatible speech endpoint (without /audio/speech). Default http://127.0.0.1:12104/v1 (compose sets http://tts:8880/v1).
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Default speech endpoint (OpenAI-compatible).", EnvironmentVariable = "TTS_URL")]
        public string Endpoint
        {
            get { return _Endpoint; }
            set { _Endpoint = String.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(Endpoint)) : value.Trim().TrimEnd('/'); }
        }

        /// <summary>
        /// Default speech model. Default kokoro.
        /// </summary>
        [Setting(Description = "Default speech model.")]
        public string Model { get; set; } = "kokoro";

        /// <summary>
        /// Default narrator voice (and HOST_A). Default af_heart.
        /// </summary>
        [Setting(Description = "Default narrator voice.")]
        public string Voice { get; set; } = "af_heart";

        /// <summary>
        /// Default second voice for two-host scripts (HOST_B). Default am_michael.
        /// </summary>
        [Setting(Description = "Default second voice for two-host scripts.")]
        public string SecondaryVoice { get; set; } = "am_michael";

        /// <summary>
        /// Most characters sent to the speech endpoint in one request. Default 1200, minimum 200, maximum 4000.
        /// </summary>
        [Setting(Description = "Most characters per speech request.", Minimum = 200, Maximum = 4000)]
        public int MaxSegmentCharacters
        {
            get { return _MaxSegmentCharacters; }
            set { _MaxSegmentCharacters = Math.Clamp(value, 200, 4000); }
        }

        /// <summary>
        /// Speech requests in parallel per episode. Default 3, minimum 1, maximum 8.
        /// </summary>
        [Setting(Description = "Speech requests in parallel per episode.", Minimum = 1, Maximum = 8)]
        public int MaxParallelSegments
        {
            get { return _MaxParallelSegments; }
            set { _MaxParallelSegments = Math.Clamp(value, 1, 8); }
        }

        /// <summary>
        /// Seconds to wait for one speech request. Default 180, minimum 10, maximum 900.
        /// </summary>
        [Setting(Description = "Seconds to wait for one speech request.", Minimum = 10, Maximum = 900)]
        public int TimeoutSeconds
        {
            get { return _TimeoutSeconds; }
            set { _TimeoutSeconds = Math.Clamp(value, 10, 900); }
        }

        /// <summary>
        /// Attempts per episode before it is marked failed. Default 3, minimum 1, maximum 10.
        /// </summary>
        [Setting(Description = "Attempts per episode before it fails.", Minimum = 1, Maximum = 10)]
        public int MaxAttempts
        {
            get { return _MaxAttempts; }
            set { _MaxAttempts = Math.Clamp(value, 1, 10); }
        }

        /// <summary>
        /// ffmpeg executable. Default ffmpeg (on PATH). When it is missing, episodes are stored as WAV.
        /// </summary>
        [Setting(Description = "ffmpeg executable path.", EnvironmentVariable = "FFMPEG_PATH")]
        public string FfmpegPath { get; set; } = "ffmpeg";

        /// <summary>
        /// MP3 bitrate. Default 96k.
        /// </summary>
        [Setting(Description = "MP3 bitrate.")]
        public string Bitrate { get; set; } = "96k";

        /// <summary>
        /// Normalize loudness to -16 LUFS (podcast standard). Default true.
        /// </summary>
        [Setting(Description = "Normalize loudness to -16 LUFS.")]
        public bool NormalizeLoudness { get; set; } = true;

        #endregion

        #region Private-Members

        private string _Endpoint = "http://127.0.0.1:12104/v1";
        private int _MaxSegmentCharacters = 1200;
        private int _MaxParallelSegments = 3;
        private int _TimeoutSeconds = 180;
        private int _MaxAttempts = 3;

        #endregion
    }
}
