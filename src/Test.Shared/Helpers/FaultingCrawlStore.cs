namespace Test.Shared.Helpers
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Fetch;

    /// <summary>Storage failure injection around a real disk or S3 adapter.</summary>
    public class FaultingCrawlStore : ICrawlContentStore
    {
        private readonly ICrawlContentStore _Inner;
        private bool _Disposed;
        /// <summary>Fail reads. Default false.</summary>
        public bool FailReads { get; set; }
        /// <summary>Fail Markdown writes after raw upload. Default false.</summary>
        public bool FailWrites { get; set; }
        /// <summary>Fail deletes. Default false.</summary>
        public bool FailDeletes { get; set; }
        /// <summary>Simulate a cancellable slow read. Default false.</summary>
        public bool SlowReads { get; set; }
        /// <summary>Create an owned fault wrapper.</summary>
        public FaultingCrawlStore(ICrawlContentStore inner) { _Inner = inner; }
        /// <inheritdoc />
        public Task WriteAsync(string key, string contentType, byte[] data, CancellationToken token = default)
        {
            if (FailWrites && key.EndsWith("content.md", StringComparison.Ordinal)) throw new IOException("Injected failure");
            return _Inner.WriteAsync(key, contentType, data, token);
        }
        /// <inheritdoc />
        public async Task<byte[]> ReadAsync(string key, long limit, CancellationToken token = default)
        {
            if (FailReads) throw new IOException("Injected failure");
            if (SlowReads) await Task.Delay(10000, token).ConfigureAwait(false);
            return await _Inner.ReadAsync(key, limit, token).ConfigureAwait(false);
        }
        /// <inheritdoc />
        public Task DeleteAsync(string key, CancellationToken token = default)
        {
            if (FailDeletes) throw new IOException("Injected failure");
            return _Inner.DeleteAsync(key, token);
        }
        /// <inheritdoc />
        public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
        /// <summary>Dispose the owned store.</summary>
        protected virtual void Dispose(bool disposing) { if (_Disposed) return; if (disposing) _Inner.Dispose(); _Disposed = true; }
    }
}
