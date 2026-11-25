namespace MyMorningNews.Core.Fetch
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Private crawl object operations. Implementations are safe for concurrent use and bound reads.</summary>
    public interface ICrawlContentStore : IDisposable
    {
        /// <summary>Write an immutable object; caller allocates a unique key. Honors cancellation.</summary>
        /// <param name="key">Safe relative object key.</param>
        /// <param name="contentType">Media type.</param>
        /// <param name="data">Content bytes.</param>
        /// <param name="token">Cancellation token.</param>
        Task WriteAsync(string key, string contentType, byte[] data, CancellationToken token = default);
        /// <summary>Read an object, rejecting lengths greater than limit. Missing or invalid objects throw.</summary>
        /// <param name="key">Safe relative object key.</param>
        /// <param name="limit">Maximum bytes to allocate/read.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Bounded content.</returns>
        Task<byte[]> ReadAsync(string key, long limit, CancellationToken token = default);
        /// <summary>Delete exactly one object; an absent object is already deleted.</summary>
        /// <param name="key">Safe relative object key.</param>
        /// <param name="token">Cancellation token.</param>
        Task DeleteAsync(string key, CancellationToken token = default);
    }
}
