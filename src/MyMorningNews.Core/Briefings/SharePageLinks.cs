namespace MyMorningNews.Core.Briefings
{
    using System;

    /// <summary>
    /// The addresses a public share page links to. Paths are relative to the server root so the page loads its logo
    /// and audio from its own origin; the absolute logo URL is only for link previews.
    /// </summary>
    public class SharePageLinks
    {
        /// <summary>
        /// Path of the reading page. Empty on the not-found page.
        /// </summary>
        public string ReadPath { get; set; } = String.Empty;

        /// <summary>
        /// Path of the listening page. Empty on the not-found page.
        /// </summary>
        public string ListenPath { get; set; } = String.Empty;

        /// <summary>
        /// Path of the audio stream, or null when the briefing has no finished audio.
        /// </summary>
        public string? AudioPath { get; set; } = null;

        /// <summary>
        /// Path of the logo image.
        /// </summary>
        public string LogoPath { get; set; } = "/v1.0/public/assets/logo.png";

        /// <summary>
        /// Absolute URL of the logo image for Open Graph and Twitter cards.
        /// </summary>
        public string LogoUrl { get; set; } = String.Empty;

        /// <summary>
        /// Where the call to action goes: the sign-up form when public sign-up is open, otherwise the marketing site.
        /// </summary>
        public string SignupUrl { get; set; } = "https://mymorningnews.ai";

        /// <summary>
        /// Whether <see cref="SignupUrl"/> is a sign-up form (true) or the marketing site (false). Default false.
        /// </summary>
        public bool SignupOpen { get; set; } = false;

        /// <summary>
        /// The marketing site.
        /// </summary>
        public string SiteUrl { get; set; } = "https://mymorningnews.ai";
    }
}
