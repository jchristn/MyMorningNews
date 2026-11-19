namespace MyMorningNews.Core.Storage
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Read-only view of at most N bytes of an inner stream from its current position. Disposes the inner stream.
    /// </summary>
    public class BoundedStream : Stream
    {
        private readonly Stream _Inner;
        private long _Remaining;
        private readonly long _Length;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="inner">Inner stream.</param>
        /// <param name="length">Bytes to expose (minimum 0).</param>
        /// <exception cref="ArgumentNullException">inner is null.</exception>
        public BoundedStream(Stream inner, long length)
        {
            _Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _Length = Math.Max(0, length);
            _Remaining = _Length;
        }

        /// <inheritdoc />
        public override bool CanRead
        {
            get { return true; }
        }

        /// <inheritdoc />
        public override bool CanSeek
        {
            get { return false; }
        }

        /// <inheritdoc />
        public override bool CanWrite
        {
            get { return false; }
        }

        /// <inheritdoc />
        public override long Length
        {
            get { return _Length; }
        }

        /// <inheritdoc />
        public override long Position
        {
            get { return _Length - _Remaining; }
            set { throw new NotSupportedException("The stream cannot seek."); }
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_Remaining <= 0) return 0;
            int n = _Inner.Read(buffer, offset, (int)Math.Min(count, _Remaining));
            _Remaining -= n;
            return n;
        }

        /// <inheritdoc />
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_Remaining <= 0) return 0;
            int n = await _Inner.ReadAsync(buffer.AsMemory(offset, (int)Math.Min(count, _Remaining)), cancellationToken).ConfigureAwait(false);
            _Remaining -= n;
            return n;
        }

        /// <inheritdoc />
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_Remaining <= 0) return 0;
            int n = await _Inner.ReadAsync(buffer.Slice(0, (int)Math.Min(buffer.Length, _Remaining)), cancellationToken).ConfigureAwait(false);
            _Remaining -= n;
            return n;
        }

        /// <inheritdoc />
        public override void Flush()
        {
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("The stream cannot seek.");
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            throw new NotSupportedException("The stream is read-only.");
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("The stream is read-only.");
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing) _Inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
