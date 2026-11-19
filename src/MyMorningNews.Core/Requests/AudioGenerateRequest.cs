namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Make audio for a briefing.
    /// </summary>
    public class AudioGenerateRequest
    {
        /// <summary>
        /// Voice, or null for the edition or tenant default.
        /// </summary>
        public string? VoiceProfileId { get; set; } = null;
    }
}
