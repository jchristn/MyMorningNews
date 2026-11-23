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
        /// The public link: one page to listen to and read the briefing. No sign-in is needed to open it. It plays
        /// audio once the briefing has audio.
        /// </summary>
        public string Url { get; set; } = String.Empty;

        /// <summary>
        /// Same value as <see cref="Url"/>. Kept for clients written when reading and listening had separate links.
        /// </summary>
        public string ReadUrl { get; set; } = String.Empty;

        /// <summary>
        /// Same value as <see cref="Url"/>. Kept for clients written when reading and listening had separate links.
        /// </summary>
        public string ListenUrl { get; set; } = String.Empty;

        /// <summary>
        /// Whether the briefing has finished audio right now.
        /// </summary>
        public bool AudioReady { get; set; } = false;
    }
}
