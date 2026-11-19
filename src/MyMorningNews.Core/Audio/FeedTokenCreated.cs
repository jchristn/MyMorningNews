namespace MyMorningNews.Core.Audio
{
    using System;

    /// <summary>
    /// A new feed token; the secret and URL are shown once.
    /// </summary>
    public class FeedTokenCreated
    {
        /// <summary>
        /// Stored token (hash only).
        /// </summary>
        public MyMorningNews.Core.Models.FeedToken Token { get; set; } = new MyMorningNews.Core.Models.FeedToken();

        /// <summary>
        /// The token itself. Shown once.
        /// </summary>
        public string Secret { get; set; } = String.Empty;

        /// <summary>
        /// Private podcast feed URL.
        /// </summary>
        public string FeedUrl { get; set; } = String.Empty;
    }
}
