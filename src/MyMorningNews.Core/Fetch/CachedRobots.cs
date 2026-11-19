namespace MyMorningNews.Core.Fetch
{
    using System;
    using CrawlSharp.Web;

    /// <summary>
    /// A cached robots.txt for one origin. Null robots means none was found, so everything is allowed.
    /// </summary>
    internal class CachedRobots
    {
        /// <summary>
        /// Parsed robots.txt, or null when the origin has none.
        /// </summary>
        public RobotsFile? Robots { get; set; } = null;

        /// <summary>
        /// When the entry must be fetched again (UTC).
        /// </summary>
        public DateTime ExpiresUtc { get; set; } = DateTime.UtcNow;
    }
}
