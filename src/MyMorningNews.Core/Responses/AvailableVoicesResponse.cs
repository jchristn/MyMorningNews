namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The voices someone can choose for a topic.
    /// </summary>
    public class AvailableVoicesResponse
    {
        /// <summary>
        /// Whether speech is turned on for this server. When false, voices can be chosen but no audio is made.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// True when the speech server listed its voices; false when the list is only the configured default voices.
        /// </summary>
        public bool Listed { get; set; } = false;

        /// <summary>
        /// The workspace's default voice profile, used by topics that have no voice of their own. May be null.
        /// </summary>
        public string? DefaultVoiceProfileId { get; set; } = null;

        /// <summary>
        /// Voices: the workspace's voice profiles first, then the speech server's other voices. Never null.
        /// </summary>
        public List<AvailableVoice> Voices
        {
            get { return _Voices; }
            set { _Voices = value ?? new List<AvailableVoice>(); }
        }

        private List<AvailableVoice> _Voices = new List<AvailableVoice>();
    }
}
