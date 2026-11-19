namespace MyMorningNews.Core.Storage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Helpers;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Core.Telemetry;
    using SyslogLogging;

    /// <summary>
    /// Saves, reads, pins, and expires artifacts (plan section 22.1). New artifacts go to the configured store; existing
    /// ones are read from the store they were written to, so switching providers keeps older files readable.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class ArtifactService : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Store new artifacts are written to.
        /// </summary>
        public IArtifactStore Current
        {
            get { return _Settings.Storage.Provider == StorageProviderEnum.S3 && _S3 != null ? _S3 : _Local; }
        }

        #endregion

        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly LocalArtifactStore _Local;
        private readonly IArtifactStore? _S3;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate. The S3 store is created when Storage.S3Bucket is set (or supplied directly for tests).
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging.</param>
        /// <param name="s3">S3 store to use instead of one built from settings; null builds from settings.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public ArtifactService(DatabaseDriverBase database, ServerSettings settings, LoggingModule logging, IArtifactStore? s3 = null)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Local = new LocalArtifactStore(settings.Storage.LocalDirectory);
            _S3 = s3 ?? (String.IsNullOrWhiteSpace(settings.Storage.S3Bucket) ? null : new S3ArtifactStore(settings.Storage));
            if (settings.Storage.Provider == StorageProviderEnum.S3 && _S3 == null)
                _Logging.Warn("[ArtifactService] Storage.Provider is S3 but Storage.S3Bucket is empty; writing to the local directory instead.");
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Store content and record the artifact. Expiry is now plus Storage.ArtifactRetentionDays.
        /// </summary>
        /// <param name="write">Content and links.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Artifact.</returns>
        /// <exception cref="ArgumentNullException">write is null.</exception>
        /// <exception cref="ArgumentException">Tenant or user is empty.</exception>
        public async Task<Artifact> SaveAsync(ArtifactWrite write, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(write);
            if (String.IsNullOrWhiteSpace(write.TenantId) || String.IsNullOrWhiteSpace(write.UserId)) throw new ArgumentException("Tenant and user are required.", nameof(write));
            DateTime now = DateTime.UtcNow;
            string id = IdGenerator.GenerateArtifactId();
            string ext = String.IsNullOrWhiteSpace(write.Extension) ? "bin" : write.Extension.Trim().TrimStart('.').ToLowerInvariant();
            string key = write.TenantId + "/" + now.ToString("yyyy") + "/" + now.ToString("MM") + "/" + id + "." + ext;
            IArtifactStore store = Current;
            await store.PutAsync(key, write.Content, write.ContentType, token).ConfigureAwait(false);
            Artifact a = new Artifact
            {
                Id = id,
                TenantId = write.TenantId,
                UserId = write.UserId,
                Kind = write.Kind,
                TopicId = write.TopicId,
                BriefingId = write.BriefingId,
                ResearchRequestId = write.ResearchRequestId,
                FileName = write.FileName,
                ContentType = write.ContentType,
                Bytes = write.Content.LongLength,
                Sha256 = Convert.ToHexString(SHA256.HashData(write.Content)).ToLowerInvariant(),
                StorageProvider = store.Provider,
                StorageKey = key,
                ExpiresUtc = now.AddDays(_Settings.Storage.ArtifactRetentionDays),
                Pinned = write.Pinned
            };
            MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.Artifacts, "kind", write.Kind.ToString());
            return await _Database.Artifacts.CreateAsync(a, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Open an artifact's content, optionally a byte range.
        /// </summary>
        /// <param name="artifact">Artifact.</param>
        /// <param name="offset">First byte, or null.</param>
        /// <param name="length">Bytes, or null for the rest.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Open stream; the caller disposes it.</returns>
        /// <exception cref="ArtifactExpiredException">Retention deleted the content, or it is missing from the store.</exception>
        public async Task<ArtifactReadResult> OpenAsync(Artifact artifact, long? offset = null, long? length = null, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(artifact);
            if (artifact.DeletedUtc.HasValue) throw new ArtifactExpiredException("This file expired on " + artifact.DeletedUtc.Value.ToString("yyyy-MM-dd") + " under the retention policy.");
            try
            {
                return await StoreFor(artifact).OpenReadAsync(artifact.StorageKey, offset, length, token).ConfigureAwait(false);
            }
            catch (FileNotFoundException ex)
            {
                throw new ArtifactExpiredException("The file is no longer in storage.", ex);
            }
        }

        /// <summary>
        /// Read a whole artifact into memory (small files such as Markdown and HTML).
        /// </summary>
        /// <param name="artifact">Artifact.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Bytes.</returns>
        /// <exception cref="ArtifactExpiredException">The content is gone.</exception>
        public async Task<byte[]> ReadAllAsync(Artifact artifact, CancellationToken token = default)
        {
            using (ArtifactReadResult r = await OpenAsync(artifact, null, null, token).ConfigureAwait(false))
            using (MemoryStream ms = new MemoryStream())
            {
                await r.Stream.CopyToAsync(ms, token).ConfigureAwait(false);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// A presigned download URL when the artifact's store supports one; otherwise null (serve through the server).
        /// </summary>
        /// <param name="artifact">Artifact.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>URL, or null.</returns>
        public async Task<string?> DownloadUrlAsync(Artifact artifact, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(artifact);
            if (artifact.DeletedUtc.HasValue) return null;
            return await StoreFor(artifact).GetDownloadUrlAsync(artifact.StorageKey, artifact.FileName, _Settings.Storage.PresignedUrlMinutes, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Remove content from the store and mark the artifact deleted. The row stays for history.
        /// </summary>
        /// <param name="artifact">Artifact.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated artifact.</returns>
        public async Task<Artifact> DeleteContentAsync(Artifact artifact, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(artifact);
            if (artifact.DeletedUtc.HasValue) return artifact;
            await StoreFor(artifact).DeleteAsync(artifact.StorageKey, token).ConfigureAwait(false);
            artifact.DeletedUtc = DateTime.UtcNow;
            return await _Database.Artifacts.UpdateAsync(artifact, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Pin or unpin a briefing and its files. Pinning counts against Storage.MaxPinnedArtifacts per tenant.
        /// </summary>
        /// <param name="briefing">Briefing.</param>
        /// <param name="pinned">Pinned.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated briefing.</returns>
        /// <exception cref="QuotaExceededException">The tenant already pinned the maximum.</exception>
        public async Task<Briefing> SetBriefingPinnedAsync(Briefing briefing, bool pinned, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(briefing);
            if (briefing.Pinned == pinned) return briefing;
            if (pinned)
            {
                long count = await _Database.Briefings.CountAsync(new BriefingFilter { TenantId = briefing.TenantId, Pinned = true }, token).ConfigureAwait(false);
                if (count >= _Settings.Storage.MaxPinnedArtifacts)
                    throw new QuotaExceededException("You can pin up to " + _Settings.Storage.MaxPinnedArtifacts + " briefings. Unpin one to keep this one.");
            }

            briefing.Pinned = pinned;
            await _Database.Artifacts.SetPinnedForBriefingAsync(briefing.TenantId, briefing.Id, pinned, token).ConfigureAwait(false);
            return await _Database.Briefings.UpdateAsync(briefing, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete content whose retention has passed (unpinned only). Failures are logged and retried on the next pass.
        /// </summary>
        /// <param name="nowUtc">Now.</param>
        /// <param name="batch">Maximum artifacts per pass (1 to 1000).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Artifacts expired.</returns>
        public async Task<int> ExpireAsync(DateTime nowUtc, int batch, CancellationToken token = default)
        {
            List<Artifact> due = await _Database.Artifacts.ListExpiredAsync(nowUtc, batch, token).ConfigureAwait(false);
            int done = 0;
            foreach (Artifact a in due)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    await DeleteContentAsync(a, token).ConfigureAwait(false);
                    done++;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _Logging.Warn("[ArtifactService] could not expire " + a.Id + ": " + ex.Message);
                }
            }

            if (done > 0) MyMorningNewsTelemetry.Add(MyMorningNewsTelemetry.ArtifactsExpired, done);
            return done;
        }

        /// <summary>
        /// Dispose the stores.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Dispose.
        /// </summary>
        /// <param name="disposing">Disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing)
            {
                _Local.Dispose();
                _S3?.Dispose();
            }

            _Disposed = true;
        }

        private IArtifactStore StoreFor(Artifact artifact)
        {
            if (artifact.StorageProvider == StorageProviderEnum.S3)
                return _S3 ?? throw new ArtifactExpiredException("The file is in S3, but S3 storage is no longer configured.");
            return _Local;
        }

        #endregion
    }
}
