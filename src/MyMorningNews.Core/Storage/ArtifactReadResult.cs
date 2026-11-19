namespace MyMorningNews.Core.Storage
{
    using System;
    using System.IO;

    /// <summary>
    /// An open artifact stream. Dispose it when done.
    /// </summary>
    public class ArtifactReadResult : IDisposable
    {
        /// <summary>
        /// Content stream positioned at the requested offset.
        /// </summary>
        public Stream Stream { get; set; } = Stream.Null;

        /// <summary>
        /// Total size of the stored content in bytes.
        /// </summary>
        public long TotalBytes { get; set; } = 0;

        /// <summary>
        /// First byte returned.
        /// </summary>
        public long Offset { get; set; } = 0;

        /// <summary>
        /// Bytes the stream returns.
        /// </summary>
        public long Length { get; set; } = 0;

        private bool _Disposed = false;

        /// <summary>
        /// Dispose the stream.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        /// <param name="disposing">Disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing) Stream.Dispose();
            _Disposed = true;
        }
    }
}
