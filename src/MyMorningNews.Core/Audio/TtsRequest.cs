namespace MyMorningNews.Core.Audio
{
    using System;

    /// <summary>
    /// One speech synthesis request.
    /// </summary>
    public class TtsRequest
    {
        /// <summary>
        /// Base URL (without /audio/speech).
        /// </summary>
        public string Endpoint { get; set; } = String.Empty;

        /// <summary>
        /// API key, or null.
        /// </summary>
        public string? ApiKey { get; set; } = null;

        /// <summary>
        /// Model.
        /// </summary>
        public string Model { get; set; } = "kokoro";

        /// <summary>
        /// Voice.
        /// </summary>
        public string Voice { get; set; } = "af_heart";

        /// <summary>
        /// Speed (0.5 to 2.0).
        /// </summary>
        public double Speed { get; set; } = 1.0;

        /// <summary>
        /// Response format: wav (default) or mp3.
        /// </summary>
        public string Format { get; set; } = "wav";

        /// <summary>
        /// Text to speak.
        /// </summary>
        public string Text { get; set; } = String.Empty;

        /// <summary>
        /// Timeout.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 180;
    }
}
