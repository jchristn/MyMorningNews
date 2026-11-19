namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Speak a sample.
    /// </summary>
    public class VoiceTestRequest
    {
        /// <summary>
        /// Text; null speaks a default sentence.
        /// </summary>
        public string? Text { get; set; } = null;
    }
}
