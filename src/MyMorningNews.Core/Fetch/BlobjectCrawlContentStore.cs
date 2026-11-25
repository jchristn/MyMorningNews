namespace MyMorningNews.Core.Fetch
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Blobject.AmazonS3;
    using Blobject.Core;
    using Blobject.Disk;
    using MyMorningNews.Core.Settings;

    /// <summary>Blobject disk/S3 adapter with bounded concurrency and reads. Owns its client.</summary>
    public class BlobjectCrawlContentStore : ICrawlContentStore
    {
        private readonly BlobClientBase _Client;
        private readonly string _Prefix;
        private readonly bool _Disk;
        private readonly SemaphoreSlim _Gate;
        private bool _Disposed;

        /// <summary>Create a private store. S3 requires explicit credentials; blank keys are not anonymous access.</summary>
        /// <param name="settings">Validated cache settings. Connection configuration is captured at construction.</param>
        /// <exception cref="ArgumentException">Storage configuration is invalid.</exception>
        public BlobjectCrawlContentStore(CrawlCacheSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            settings.Storage.Validate(true);
            _Gate = new SemaphoreSlim(settings.MaxConcurrency);
            CrawlCacheStorageSettings s = settings.Storage;
            _Disk = s.Provider == "Disk";
            _Prefix = s.Provider == "S3" ? s.S3Prefix.TrimEnd('/') + "/" : String.Empty;
            if (s.Provider == "Disk") _Client = new DiskBlobClient(new DiskSettings(Path.GetFullPath(s.Directory)));
            else _Client = new AmazonS3BlobClient(new AwsSettings
            {
                Endpoint = String.IsNullOrEmpty(s.S3Endpoint) ? null : s.S3Endpoint,
                Ssl = !s.S3Endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase),
                Region = s.S3Region, Bucket = s.S3Bucket, AccessKey = s.S3AccessKey, SecretKey = s.S3SecretKey
            });
        }

        /// <inheritdoc />
        public async Task WriteAsync(string key, string contentType, byte[] data, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(data);
            string safe = Key(key);
            await _Gate.WaitAsync(token).ConfigureAwait(false);
            try { await _Client.WriteAsync(safe, contentType, data, token).ConfigureAwait(false); }
            finally { _Gate.Release(); }
        }

        /// <inheritdoc />
        public async Task<byte[]> ReadAsync(string key, long limit, CancellationToken token = default)
        {
            string safe = Key(key);
            await _Gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                using (BlobData blob = await _Client.GetStreamAsync(safe, token).ConfigureAwait(false))
                {
                    if (limit < 0 || blob.ContentLength > limit || blob.ContentLength > Int32.MaxValue) throw new InvalidDataException("Crawl object exceeds the read limit.");
                    byte[] bytes = new byte[(int)blob.ContentLength];
                    await blob.Data.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
                    byte[] extra = new byte[1];
                    if (await blob.Data.ReadAsync(extra, token).ConfigureAwait(false) != 0) throw new InvalidDataException("Crawl object length changed.");
                    return bytes;
                }
            }
            finally { _Gate.Release(); }
        }

        /// <inheritdoc />
        public async Task DeleteAsync(string key, CancellationToken token = default)
        {
            string safe = Key(key);
            await _Gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await _Client.DeleteAsync(safe, token).ConfigureAwait(false);
                if (_Disk)
                {
                    string parent = key;
                    for (int depth = 0; depth < 2; depth++)
                    {
                        int slash = parent.LastIndexOf('/');
                        if (slash < 1) break;
                        parent = parent.Substring(0, slash);
                        try { await _Client.DeleteAsync(Key(parent), token).ConfigureAwait(false); }
                        catch (IOException) { break; } // Another generation or file still uses this directory.
                    }
                }
            }
            finally { _Gate.Release(); }
        }

        private string Key(string key)
        {
            if (String.IsNullOrWhiteSpace(key) || key.StartsWith('/') || key.Contains("..", StringComparison.Ordinal) || key.Contains('\\')) throw new ArgumentException("Unsafe crawl object key.", nameof(key));
            return _Prefix + key;
        }

        /// <inheritdoc />
        public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
        /// <summary>Release the owned client and concurrency gate after operations finish.</summary>
        /// <param name="disposing">Whether to release managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing) { ((IDisposable)_Client).Dispose(); _Gate.Dispose(); }
            _Disposed = true;
        }
    }
}
