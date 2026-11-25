namespace MyMorningNews.Core.Fetch
{
    using System;

    /// <summary>Fill ownership; token changes on every acquisition and fences stale writers.</summary>
    public class CrawlCacheLease
    {
        /// <summary>Entry identifier. Default empty.</summary>
        public string EntryId { get; set; } = String.Empty;
        /// <summary>Unique acquisition token. Default empty.</summary>
        public string Token { get; set; } = String.Empty;
    }
}
