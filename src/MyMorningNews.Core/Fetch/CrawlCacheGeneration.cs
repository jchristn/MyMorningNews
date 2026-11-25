namespace MyMorningNews.Core.Fetch
{
    using System;

    /// <summary>Typed metadata for one immutable crawl generation; contains no response bodies.</summary>
    public class CrawlCacheGeneration
    {
        /// <summary>Generation identifier. Default String.Empty.</summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>Parent entry identifier. Default String.Empty.</summary>
        public string EntryId { get; set; } = String.Empty;

        /// <summary>Storage namespace. Default String.Empty.</summary>
        public string Namespace { get; set; } = String.Empty;

        /// <summary>Normalized requested URL. Default String.Empty.</summary>
        public string Url { get; set; } = String.Empty;

        /// <summary>Fetch profile hash. Default String.Empty.</summary>
        public string Profile { get; set; } = String.Empty;

        /// <summary>Raw object key. Default String.Empty.</summary>
        public string RawKey { get; set; } = String.Empty;

        /// <summary>Markdown object key. Default String.Empty.</summary>
        public string TextKey { get; set; } = String.Empty;

        /// <summary>Downloaded content SHA256. Default String.Empty.</summary>
        public string RawHash { get; set; } = String.Empty;

        /// <summary>Markdown SHA256. Default String.Empty.</summary>
        public string TextHash { get; set; } = String.Empty;

        /// <summary>Downloaded byte count. Default 0.</summary>
        public long RawBytes { get; set; } = 0;

        /// <summary>Markdown byte count. Default 0.</summary>
        public long TextBytes { get; set; } = 0;

        /// <summary>Original retrieval time. Default DateTime.UtcNow.</summary>
        public DateTime FetchedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Upper bound on freshness. Default DateTime.UtcNow.</summary>
        public DateTime ExpiresUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Final URL. Default String.Empty.</summary>
        public string FinalUrl { get; set; } = String.Empty;

        /// <summary>Content media type. Default String.Empty.</summary>
        public string ContentType { get; set; } = String.Empty;

        /// <summary>Page title. Default String.Empty.</summary>
        public string Title { get; set; } = String.Empty;

        /// <summary>Origin status. Default 200.</summary>
        public int StatusCode { get; set; } = 200;

        /// <summary>Cleanup fencing token, empty outside cleanup. Default String.Empty.</summary>
        public string Claim { get; set; } = String.Empty;

    }
}
