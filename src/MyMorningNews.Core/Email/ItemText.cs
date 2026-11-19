namespace MyMorningNews.Core.Email
{
    using System;

    /// <summary>
    /// The title, summary, and link shown on an email landing page.
    /// </summary>
    public class ItemText
    {
        /// <summary>
        /// Title.
        /// </summary>
        public string Title { get; set; } = String.Empty;

        /// <summary>
        /// Summary.
        /// </summary>
        public string? Summary { get; set; } = null;

        /// <summary>
        /// Web app link.
        /// </summary>
        public string? OpenUrl { get; set; } = null;
    }
}
