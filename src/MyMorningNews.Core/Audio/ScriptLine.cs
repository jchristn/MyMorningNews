namespace MyMorningNews.Core.Audio
{
    using System;

    /// <summary>
    /// One spoken line.
    /// </summary>
    public class ScriptLine
    {
        /// <summary>
        /// HOST_A, HOST_B, or null for the narrator.
        /// </summary>
        public string? Speaker { get; set; } = null;

        /// <summary>
        /// Text to speak.
        /// </summary>
        public string Text { get; set; } = String.Empty;
    }
}
