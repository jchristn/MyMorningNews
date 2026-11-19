namespace MyMorningNews.Core.Audio
{
    using System;

    /// <summary>
    /// Synthesized speech.
    /// </summary>
    public class TtsResult
    {
        /// <summary>
        /// Audio bytes.
        /// </summary>
        public byte[] Audio { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Media type.
        /// </summary>
        public string ContentType { get; set; } = "audio/wav";
    }
}
