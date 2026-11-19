namespace MyMorningNews.Core.Storage
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Where artifact bytes live (plan section 22.1). Keys are relative (tenant/yyyy/MM/id.ext); a store may add its own prefix.
    /// Implementations are safe for concurrent use.
    /// </summary>
    public interface IArtifactStore : IDisposable
    {
        /// <summary>
        /// Provider.
        /// </summary>
        StorageProviderEnum Provider { get; }

        /// <summary>
        /// Write content, replacing any existing content at the key.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="content">Content.</param>
        /// <param name="contentType">Media type.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentException">The key is empty or escapes the store.</exception>
        Task PutAsync(string key, byte[] content, string contentType, CancellationToken token = default);

        /// <summary>
        /// Open content for reading, optionally a byte range.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="offset">First byte, or null for the start.</param>
        /// <param name="length">Bytes to read, or null for the rest.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Open stream with the total size; the caller disposes it.</returns>
        /// <exception cref="FileNotFoundException">No content at the key.</exception>
        Task<ArtifactReadResult> OpenReadAsync(string key, long? offset = null, long? length = null, CancellationToken token = default);

        /// <summary>
        /// Delete content. Deleting a missing key succeeds.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteAsync(string key, CancellationToken token = default);

        /// <summary>
        /// A time-limited download URL, or null when the store serves content only through the server.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="fileName">Suggested file name.</param>
        /// <param name="minutes">Validity in minutes.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>URL, or null.</returns>
        Task<string?> GetDownloadUrlAsync(string key, string fileName, int minutes, CancellationToken token = default);
    }
}
