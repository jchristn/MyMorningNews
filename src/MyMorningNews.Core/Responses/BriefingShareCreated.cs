namespace MyMorningNews.Core.Responses
{
    using System;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// A new public share; the secret and the two links are shown once.
    /// </summary>
    public class BriefingShareCreated
    {
        /// <summary>
        /// Stored share (hash redacted).
        /// </summary>
        public BriefingShare Share { get; set; } = new BriefingShare();

        /// <summary>
        /// The link secret. Shown once.
        /// </summary>
        public string Secret { get; set; } = String.Empty;

        /// <summary>
        /// Public reading page URL. No sign-in is needed to open it.
        /// </summary>
        public string ReadUrl { get; set; } = String.Empty;

        /// <summary>
        /// Public listening page URL. No sign-in is needed to open it. It plays audio once the briefing has audio.
        /// </summary>
        public string ListenUrl { get; set; } = String.Empty;

        /// <summary>
        /// Whether the briefing has finished audio right now.
        /// </summary>
        public bool AudioReady { get; set; } = false;
    }
}
