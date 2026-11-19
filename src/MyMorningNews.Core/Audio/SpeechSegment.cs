namespace MyMorningNews.Core.Audio
{
    using System;

    /// <summary>
    /// A segment sent to the speech endpoint: one voice, one chapter.
    /// </summary>
    public class SpeechSegment
    {
        /// <summary>
        /// Chapter index.
        /// </summary>
        public int PartIndex { get; set; } = 0;

        /// <summary>
        /// HOST_A, HOST_B, or null.
        /// </summary>
        public string? Speaker { get; set; } = null;

        /// <summary>
        /// Text.
        /// </summary>
        public string Text { get; set; } = String.Empty;
    }
}
