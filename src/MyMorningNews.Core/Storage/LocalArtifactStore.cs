namespace MyMorningNews.Core.Storage
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Artifacts on the local filesystem under a root directory (the Docker media volume). Keys cannot escape the root.
    /// Writes go to a temporary file and are moved into place, so readers never see partial content.
    /// </summary>
    public class LocalArtifactStore : IArtifactStore
    {
        /// <inheritdoc />
        public StorageProviderEnum Provider
        {
            get { return StorageProviderEnum.Local; }
        }

        /// <summary>
        /// Absolute root directory.
        /// </summary>
        public string Root { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="root">Root directory; created when missing.</param>
        /// <exception cref="ArgumentNullException">root is null or empty.</exception>
        public LocalArtifactStore(string root)
        {
            if (String.IsNullOrWhiteSpace(root)) throw new ArgumentNullException(nameof(root));
            Root = Path.GetFullPath(root);
            Directory.CreateDirectory(Root);
        }

        /// <inheritdoc />
        public async Task PutAsync(string key, byte[] content, string contentType, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(content);
            string path = Resolve(key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllBytesAsync(temp, content, token).ConfigureAwait(false);
            File.Move(temp, path, true);
        }

        /// <inheritdoc />
        public Task<ArtifactReadResult> OpenReadAsync(string key, long? offset = null, long? length = null, CancellationToken token = default)
        {
            string path = Resolve(key);
            if (!File.Exists(path)) throw new FileNotFoundException("The stored file no longer exists.", key);
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
            long total = fs.Length;
            long start = Math.Clamp(offset ?? 0, 0, total);
            long count = Math.Clamp(length ?? (total - start), 0, total - start);
            fs.Seek(start, SeekOrigin.Begin);
            return Task.FromResult(new ArtifactReadResult { Stream = new BoundedStream(fs, count), TotalBytes = total, Offset = start, Length = count });
        }

        /// <inheritdoc />
        public Task DeleteAsync(string key, CancellationToken token = default)
        {
            string path = Resolve(key);
            if (File.Exists(path)) File.Delete(path);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<string?> GetDownloadUrlAsync(string key, string fileName, int minutes, CancellationToken token = default)
        {
            return Task.FromResult<string?>(null);
        }

        /// <summary>
        /// Dispose (nothing to release).
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        private string Resolve(string key)
        {
            if (String.IsNullOrWhiteSpace(key)) throw new ArgumentException("The key is empty.", nameof(key));
            string full = Path.GetFullPath(Path.Combine(Root, key.Replace('\\', '/').TrimStart('/')));
            if (!full.StartsWith(Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new ArgumentException("The key escapes the storage directory.", nameof(key));
            return full;
        }
    }
}
