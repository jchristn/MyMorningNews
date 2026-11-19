namespace MyMorningNews.Core.Storage
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon;
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.S3.Model;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Settings;

    /// <summary>
    /// Artifacts in Amazon S3 or an S3-compatible service (MinIO, R2, B2). Keys are stored under the configured prefix.
    /// Checksums are only sent when required, and payloads are not chunk-encoded, so S3-compatible servers that lack
    /// those extensions work. Thread safety: safe for concurrent use.
    /// </summary>
    public class S3ArtifactStore : IArtifactStore
    {
        /// <inheritdoc />
        public StorageProviderEnum Provider
        {
            get { return StorageProviderEnum.S3; }
        }

        /// <summary>
        /// Bucket.
        /// </summary>
        public string Bucket { get; }

        /// <summary>
        /// Key prefix (empty or ending in a slash).
        /// </summary>
        public string Prefix { get; }

        private readonly AmazonS3Client _Client;
        private bool _Disposed = false;

        /// <summary>
        /// Instantiate from storage settings.
        /// </summary>
        /// <param name="settings">Storage settings with S3Bucket set.</param>
        /// <exception cref="ArgumentNullException">settings is null.</exception>
        /// <exception cref="ArgumentException">S3Bucket is empty.</exception>
        public S3ArtifactStore(StorageSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            if (String.IsNullOrWhiteSpace(settings.S3Bucket)) throw new ArgumentException("Storage.S3Bucket is required for the S3 store.", nameof(settings));
            Bucket = settings.S3Bucket.Trim();
            string prefix = (settings.S3Prefix ?? String.Empty).Trim().TrimStart('/');
            Prefix = prefix.Length == 0 || prefix.EndsWith('/') ? prefix : prefix + "/";
            AmazonS3Config config = new AmazonS3Config
            {
                ForcePathStyle = settings.S3ForcePathStyle,
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
            };
            if (!String.IsNullOrWhiteSpace(settings.S3Endpoint))
            {
                config.ServiceURL = settings.S3Endpoint.Trim();
                config.AuthenticationRegion = settings.S3Region;
            }
            else
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(settings.S3Region);
            }

            _Client = String.IsNullOrWhiteSpace(settings.S3AccessKey)
                ? new AmazonS3Client(config)
                : new AmazonS3Client(new BasicAWSCredentials(settings.S3AccessKey, settings.S3SecretKey), config);
        }

        /// <inheritdoc />
        public async Task PutAsync(string key, byte[] content, string contentType, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(content);
            using (MemoryStream ms = new MemoryStream(content, false))
            {
                PutObjectRequest req = new PutObjectRequest
                {
                    BucketName = Bucket,
                    Key = FullKey(key),
                    InputStream = ms,
                    ContentType = contentType,
                    UseChunkEncoding = false,
                    AutoCloseStream = false
                };
                await _Client.PutObjectAsync(req, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task<ArtifactReadResult> OpenReadAsync(string key, long? offset = null, long? length = null, CancellationToken token = default)
        {
            GetObjectRequest req = new GetObjectRequest { BucketName = Bucket, Key = FullKey(key) };
            if (offset.HasValue || length.HasValue)
            {
                long start = Math.Max(0, offset ?? 0);
                req.ByteRange = length.HasValue ? new ByteRange(start, start + Math.Max(0, length.Value) - 1) : new ByteRange("bytes=" + start + "-");
            }

            try
            {
                GetObjectResponse resp = await _Client.GetObjectAsync(req, token).ConfigureAwait(false);
                long returned = resp.Headers.ContentLength;
                long total = returned;
                long first = 0;
                if (!String.IsNullOrEmpty(resp.ContentRange))
                {
                    string[] parts = resp.ContentRange.Replace("bytes", String.Empty, StringComparison.OrdinalIgnoreCase).Trim().Split('/');
                    if (parts.Length == 2 && long.TryParse(parts[1], out long t)) total = t;
                    string[] span = parts[0].Split('-');
                    if (span.Length == 2 && long.TryParse(span[0], out long f)) first = f;
                }

                return new ArtifactReadResult { Stream = resp.ResponseStream, TotalBytes = total, Offset = first, Length = returned };
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                throw new FileNotFoundException("The stored file no longer exists.", key, ex);
            }
        }

        /// <inheritdoc />
        public async Task DeleteAsync(string key, CancellationToken token = default)
        {
            try
            {
                await _Client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = Bucket, Key = FullKey(key) }, token).ConfigureAwait(false);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
            }
        }

        /// <inheritdoc />
        public async Task<string?> GetDownloadUrlAsync(string key, string fileName, int minutes, CancellationToken token = default)
        {
            GetPreSignedUrlRequest req = new GetPreSignedUrlRequest
            {
                BucketName = Bucket,
                Key = FullKey(key),
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddMinutes(Math.Clamp(minutes, 1, 10080))
            };
            if (!String.IsNullOrWhiteSpace(fileName)) req.ResponseHeaderOverrides.ContentDisposition = "attachment; filename=\"" + fileName.Replace("\"", String.Empty) + "\"";
            if (_Client.Config.ServiceURL != null && _Client.Config.ServiceURL.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) req.Protocol = Protocol.HTTP;
            return await _Client.GetPreSignedURLAsync(req).ConfigureAwait(false);
        }

        /// <summary>
        /// Dispose the client.
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
            if (disposing) _Client.Dispose();
            _Disposed = true;
        }

        private string FullKey(string key)
        {
            if (String.IsNullOrWhiteSpace(key)) throw new ArgumentException("The key is empty.", nameof(key));
            string k = key.Replace('\\', '/').TrimStart('/');
            if (k.Contains("../", StringComparison.Ordinal)) throw new ArgumentException("The key escapes the prefix.", nameof(key));
            return Prefix + k;
        }
    }
}
