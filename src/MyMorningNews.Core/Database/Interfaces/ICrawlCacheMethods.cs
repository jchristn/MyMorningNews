namespace MyMorningNews.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Fetch;
    using MyMorningNews.Core.Settings;

    /// <summary>Internal crawl metadata operations. Thread safe, bounded, and never exposed as REST arrays.</summary>
    public interface ICrawlCacheMethods
    {
        /// <summary>Bind a namespace permanently to its storage location; false prevents unsafe provider/path reuse.</summary>
        Task<bool> BindAsync(string ns, string storageIdentity, CancellationToken token = default);

        /// <summary>Read the active ready generation with exact identity verification; null on miss.</summary>
        Task<CrawlCacheGeneration?> ReadAsync(string ns, string key, string url, string profile, CancellationToken token = default);

        /// <summary>Acquire a bounded fill lease; null when owned elsewhere or metadata capacity is exhausted.</summary>
        Task<CrawlCacheLease?> AcquireAsync(string ns, string key, string url, string profile, CrawlCacheSettings settings, CancellationToken token = default);

        /// <summary>Renew only the current unexpired lease; false fences the caller out.</summary>
        Task<bool> RenewAsync(CrawlCacheLease lease, int seconds, CancellationToken token = default);

        /// <summary>Release ownership without affecting a successor; remove empty idle entries.</summary>
        Task ReleaseAsync(CrawlCacheLease lease, CancellationToken token = default);

        /// <summary>Reserve bounded capacity and immutable object keys before upload; false means bypass publication.</summary>
        Task<bool> ReserveAsync(CrawlCacheLease lease, CrawlCacheGeneration generation, CrawlCacheSettings settings, CancellationToken token = default);

        /// <summary>Atomically publish a completed generation only while the fill lease is current.</summary>
        Task<bool> PublishAsync(CrawlCacheLease lease, string generationId, CancellationToken token = default);

        /// <summary>Detach a corrupt or expired generation and mark it for cleanup.</summary>
        Task InvalidateAsync(string generationId, CancellationToken token = default);

        /// <summary>Claim at most CleanupBatchSize expired, abandoned, superseded or capacity-evicted generations.</summary>
        Task<List<CrawlCacheGeneration>> ClaimCleanupAsync(string ns, CrawlCacheSettings settings, CancellationToken token = default);

        /// <summary>Remove metadata only after both objects were deleted by the current cleanup claimant.</summary>
        Task CompleteCleanupAsync(CrawlCacheGeneration generation, CancellationToken token = default);

        /// <summary>Select at most one ready generation for periodic integrity verification.</summary>
        Task<CrawlCacheGeneration?> ClaimVerificationAsync(string ns, CancellationToken token = default);

        /// <summary>Return reserved bytes, generation count and entry count for fixed-cardinality metrics.</summary>
        Task<long[]> UsageAsync(string ns, CancellationToken token = default);

    }
}
