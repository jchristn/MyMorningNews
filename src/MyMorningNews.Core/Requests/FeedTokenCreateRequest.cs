namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Create a private podcast feed.
    /// </summary>
    public class FeedTokenCreateRequest
    {
        /// <summary>
        /// Label, such as the podcast app.
        /// </summary>
        [TextLimit(128)]
        public string? Name { get; set; } = null;

        /// <summary>
        /// Limit the feed to one edition, or null for all of my audio.
        /// </summary>
        public string? EditionId { get; set; } = null;
    }
}
