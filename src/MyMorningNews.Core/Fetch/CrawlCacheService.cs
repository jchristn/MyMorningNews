namespace MyMorningNews.Core.Fetch
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database.Interfaces;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;
    using MyMorningNews.Core.Serialization;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Core.Telemetry;
    using SyslogLogging;

    /// <summary>Shared disposable page cache. Callers must check current access/robots policy before lookup. Thread safe.</summary>
    public class CrawlCacheService : IDisposable
    {
        private readonly ICrawlCacheMethods _Database;
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly string _Namespace;
        private readonly string _StorageIdentity;
        private readonly Lazy<ICrawlContentStore> _Store;
        private bool _Disposed;

        /// <summary>Create the cache; connection configuration is captured and content storage opens lazily.</summary>
        /// <param name="database">Crawl metadata operations.</param>
        /// <param name="settings">Live settings. Storage changes require restart.</param>
        /// <param name="logging">Logger; content and URLs are never logged.</param>
        /// <param name="store">Optional owned store for integration tests.</param>
        public CrawlCacheService(ICrawlCacheMethods database, ServerSettings settings, LoggingModule logging, ICrawlContentStore? store = null)
        {
            ArgumentNullException.ThrowIfNull(database);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(logging);
            settings.CrawlCache.Validate();
            _Database = database; _Settings = settings; _Logging = logging;
            _Namespace = settings.CrawlCache.StorageNamespace;
            CrawlCacheSettings captured = Serializer.Deserialize<CrawlCacheSettings>(Serializer.Serialize(settings.CrawlCache))!;
            CrawlCacheStorageSettings location = captured.Storage;
            _StorageIdentity = CrawlCacheKey.Hash(location.Provider + "\n" + (location.Provider == "Disk" ? Path.GetFullPath(location.Directory)
                : location.S3Endpoint + "\n" + location.S3Region + "\n" + location.S3Bucket + "\n" + location.S3Prefix.TrimEnd('/')));
            _Store = new Lazy<ICrawlContentStore>(() => store ?? new BlobjectCrawlContentStore(captured));
        }

        /// <summary>Retrieve cached content or perform a fresh origin fetch. Cache failures never hide a successful origin result.</summary>
        /// <param name="url">Eligible requested URL.</param>
        /// <param name="forceRefresh">True always performs a new origin retrieval, even while another fill is running.</param>
        /// <param name="fetch">Origin retrieval callback, invoked at most once per call.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Fetch result with original timestamp and explicit cache provenance.</returns>
        public async Task<FetchResult> FetchAsync(string url, bool forceRefresh, Func<CancellationToken, Task<FetchResult>> fetch, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(fetch);
            token.ThrowIfCancellationRequested();
            CrawlCacheSettings s = _Settings.CrawlCache;
            string? normalized = CrawlCacheKey.Normalize(url);
            if (!s.Enabled || normalized == null)
            {
                FetchResult bypass = await fetch(token).ConfigureAwait(false);
                bypass.CacheStatus = forceRefresh ? "Forced" : "Bypass";
                Event(bypass.CacheStatus);
                return bypass;
            }
            string profile = CrawlCacheKey.Profile(_Settings.Search);
            string key = CrawlCacheKey.Hash(normalized + "\n" + profile);
            CrawlCacheLease? lease = null;
            bool cacheError = false;
            try
            {
                await BindAsync(token).ConfigureAwait(false);
                Stopwatch wait = Stopwatch.StartNew();
                do
                {
                    if (!forceRefresh)
                    {
                        FetchResult? hit = await ReadAsync(url, normalized, key, profile, token).ConfigureAwait(false);
                        if (hit != null) { Event("Hit"); return hit; }
                    }
                    lease = await OperationAsync(t => _Database.AcquireAsync(_Namespace, key, normalized, profile, s, t), token).ConfigureAwait(false);
                    if (lease != null) break;
                    if (wait.ElapsedMilliseconds >= s.WaitForFillMs) break;
                    await Task.Delay(Math.Min(100, Math.Max(1, s.WaitForFillMs - (int)wait.ElapsedMilliseconds)), token).ConfigureAwait(false);
                } while (true);
                // A different owner may have published between our miss and acquisition.
                if (lease != null && !forceRefresh)
                {
                    FetchResult? hit = await ReadAsync(url, normalized, key, profile, token).ConfigureAwait(false);
                    if (hit != null)
                    {
                        await ReleaseAsync(lease).ConfigureAwait(false);
                        Event("Hit"); return hit;
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                if (lease != null) await ReleaseAsync(lease).ConfigureAwait(false);
                throw;
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                Warn(ex); cacheError = true;
            }
            using (CancellationTokenSource heartbeat = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                Task renew = lease == null ? Task.CompletedTask : RenewAsync(lease, heartbeat.Token);
                try
                {
                    FetchResult result = await fetch(token).ConfigureAwait(false);
                    result.CacheStatus = forceRefresh ? "Forced" : cacheError ? "Error" : "Miss";
                    Event(result.CacheStatus);
                    if (lease != null && s.Enabled && result.Disposition == FetchDispositionEnum.Fetched && result.RawData != null
                        && !String.IsNullOrWhiteSpace(result.Text) && result.CacheExpiresUtc > DateTime.UtcNow
                        && result.RawData.LongLength <= _Settings.Search.MaxFetchBytes)
                    {
                        try { result.CacheUpdated = await WriteAsync(lease, normalized, profile, result, token).ConfigureAwait(false); }
                        catch (Exception ex) when (!token.IsCancellationRequested) { Warn(ex); }
                    }
                    return result;
                }
                finally
                {
                    heartbeat.Cancel();
                    await renew.ConfigureAwait(false);
                    if (lease != null) await ReleaseAsync(lease).ConfigureAwait(false);
                }
            }
        }

        private async Task<FetchResult?> ReadAsync(string requested, string normalized, string key, string profile, CancellationToken token)
        {
            CrawlCacheGeneration? g = await OperationAsync(t => _Database.ReadAsync(_Namespace, key, normalized, profile, t), token).ConfigureAwait(false);
            if (g == null) return null;
            CrawlCacheSettings s = _Settings.CrawlCache;
            if (g.ExpiresUtc <= DateTime.UtcNow || g.FetchedUtc.AddSeconds(s.FreshnessSeconds) <= DateTime.UtcNow
                || g.RawBytes > _Settings.Search.MaxFetchBytes || g.RawBytes + g.TextBytes > s.MaxEntryBytes)
            {
                await OperationAsync(t => _Database.InvalidateAsync(g.Id, t), token).ConfigureAwait(false);
                Event("Expired"); return null;
            }
            try
            {
                CheckKeys(g);
                byte[] bytes = await OperationAsync(t => _Store.Value.ReadAsync(g.TextKey, Math.Min(s.MaxEntryBytes, g.TextBytes), t), token).ConfigureAwait(false);
                if (bytes.LongLength != g.TextBytes || Hash(bytes) != g.TextHash) throw new InvalidDataException("Invalid crawl object.");
                return new FetchResult
                {
                    Url = requested, FinalUrl = g.FinalUrl, CanonicalUrl = UrlCanonicalizer.Canonicalize(g.FinalUrl), Title = g.Title,
                    Text = Encoding.UTF8.GetString(bytes), Bytes = g.RawBytes, ContentHash = g.RawHash, ContentType = g.ContentType,
                    StatusCode = g.StatusCode, FetchedUtc = g.FetchedUtc, Disposition = FetchDispositionEnum.Fetched, CacheStatus = "Hit", NetworkBytes = 0
                };
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                await OperationAsync(t => _Database.InvalidateAsync(g.Id, t), token).ConfigureAwait(false);
                Warn(ex); return null;
            }
        }

        private async Task<bool> WriteAsync(CrawlCacheLease lease, string normalized, string profile, FetchResult result, CancellationToken token)
        {
            CrawlCacheSettings s = _Settings.CrawlCache;
            if ((result.Title?.Length ?? 0) > 4096 || (result.ContentType?.Length ?? 0) > 256) return false;
            byte[] raw = result.RawData!;
            if (Encoding.UTF8.GetByteCount(result.Text!) + raw.LongLength > s.MaxEntryBytes) return false;
            byte[] text = Encoding.UTF8.GetBytes(result.Text!);
            string id = IdGenerator.Generate(IdPrefixes.CrawlCache);
            string prefix = _Namespace + "/" + lease.EntryId + "/" + id + "/";
            CrawlCacheGeneration g = new CrawlCacheGeneration
            {
                Id = id, EntryId = lease.EntryId, Namespace = _Namespace, Url = normalized, Profile = profile,
                RawKey = prefix + "raw", TextKey = prefix + "content.md", RawHash = Hash(raw), TextHash = Hash(text), RawBytes = raw.LongLength, TextBytes = text.LongLength,
                FetchedUtc = result.FetchedUtc, ExpiresUtc = result.CacheExpiresUtc!.Value, FinalUrl = result.FinalUrl ?? normalized,
                ContentType = result.ContentType ?? "application/octet-stream", Title = result.Title ?? String.Empty, StatusCode = result.StatusCode ?? 200
            };
            if (!await OperationAsync(t => _Database.ReserveAsync(lease, g, s, t), token).ConfigureAwait(false)) { Event("Capacity"); return false; }
            await OperationAsync(t => _Store.Value.WriteAsync(g.RawKey, g.ContentType, raw, t), token).ConfigureAwait(false);
            await OperationAsync(t => _Store.Value.WriteAsync(g.TextKey, "text/markdown; charset=utf-8", text, t), token).ConfigureAwait(false);
            bool published = await OperationAsync(t => _Database.PublishAsync(lease, id, t), token).ConfigureAwait(false);
            Event(published ? "Published" : "Fenced");
            return published;
        }

        /// <summary>Run one bounded cleanup and integrity pass, even when new caching is disabled.</summary>
        /// <param name="token">Cancellation token.</param>
        public async Task CleanupAsync(CancellationToken token = default)
        {
            await BindAsync(token).ConfigureAwait(false);
            List<CrawlCacheGeneration> batch = await OperationAsync(t => _Database.ClaimCleanupAsync(_Namespace, _Settings.CrawlCache, t), token).ConfigureAwait(false);
            foreach (CrawlCacheGeneration g in batch)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    CheckKeys(g);
                    await OperationAsync(t => _Store.Value.DeleteAsync(g.RawKey, t), token).ConfigureAwait(false);
                    await OperationAsync(t => _Store.Value.DeleteAsync(g.TextKey, t), token).ConfigureAwait(false);
                    await OperationAsync(t => _Database.CompleteCleanupAsync(g, t), token).ConfigureAwait(false);
                    Event("Evicted");
                }
                catch (Exception ex) when (!token.IsCancellationRequested) { Warn(ex); Event("CleanupError"); }
            }
            CrawlCacheGeneration? check = await OperationAsync(t => _Database.ClaimVerificationAsync(_Namespace, t), token).ConfigureAwait(false);
            if (check != null)
            {
                try
                {
                    CheckKeys(check);
                    byte[] raw = await OperationAsync(t => _Store.Value.ReadAsync(check.RawKey, Math.Min(check.RawBytes, _Settings.CrawlCache.MaxEntryBytes), t), token).ConfigureAwait(false);
                    byte[] text = await OperationAsync(t => _Store.Value.ReadAsync(check.TextKey, Math.Min(check.TextBytes, _Settings.CrawlCache.MaxEntryBytes), t), token).ConfigureAwait(false);
                    if (raw.LongLength != check.RawBytes || text.LongLength != check.TextBytes || Hash(raw) != check.RawHash || Hash(text) != check.TextHash) throw new InvalidDataException("Invalid crawl content.");
                }
                catch (Exception ex) when (!token.IsCancellationRequested)
                {
                    await OperationAsync(t => _Database.InvalidateAsync(check.Id, t), token).ConfigureAwait(false); Warn(ex);
                }
            }
            long[] usage = await OperationAsync(t => _Database.UsageAsync(_Namespace, t), token).ConfigureAwait(false);
            MyMorningNewsTelemetry.Record(MyMorningNewsTelemetry.CrawlCacheBytes, usage[0]);
            MyMorningNewsTelemetry.Record(MyMorningNewsTelemetry.CrawlCacheRecords, usage[1], "kind", "generation");
            MyMorningNewsTelemetry.Record(MyMorningNewsTelemetry.CrawlCacheRecords, usage[2], "kind", "entry");
        }

        private async Task BindAsync(CancellationToken token)
        {
            if (!await OperationAsync(t => _Database.BindAsync(_Namespace, _StorageIdentity, t), token).ConfigureAwait(false))
                throw new InvalidOperationException("Crawl cache storage location changed; configure a new namespace.");
        }

        private void CheckKeys(CrawlCacheGeneration g)
        {
            string prefix = _Namespace + "/" + g.EntryId + "/" + g.Id + "/";
            if (g.Namespace != _Namespace || g.RawKey != prefix + "raw" || g.TextKey != prefix + "content.md") throw new InvalidDataException("Crawl object namespace mismatch.");
        }

        private async Task RenewAsync(CrawlCacheLease lease, CancellationToken token)
        {
            try
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _Settings.CrawlCache.LeaseSeconds / 3)), token).ConfigureAwait(false);
                    if (!await OperationAsync(t => _Database.RenewAsync(lease, _Settings.CrawlCache.LeaseSeconds, t), token).ConfigureAwait(false)) return;
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex) { Warn(ex); }
        }

        private async Task ReleaseAsync(CrawlCacheLease lease)
        {
            try { await OperationAsync(t => _Database.ReleaseAsync(lease, t), CancellationToken.None).ConfigureAwait(false); }
            catch (Exception ex) { Warn(ex); }
        }

        private async Task<T> OperationAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken token)
        {
            Stopwatch sw = Stopwatch.StartNew();
            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(_Settings.CrawlCache.OperationTimeoutMs);
                try { return await operation(timeout.Token).ConfigureAwait(false); }
                finally { MyMorningNewsTelemetry.Record(MyMorningNewsTelemetry.CrawlCacheDuration, sw.Elapsed.TotalSeconds); }
            }
        }
        private Task OperationAsync(Func<CancellationToken, Task> operation, CancellationToken token)
        {
            return OperationAsync(async t => { await operation(t).ConfigureAwait(false); return true; }, token);
        }
        private static string Hash(byte[] bytes) { return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(); }
        private static void Event(string outcome) { MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.CrawlCacheEvents, "outcome", outcome); }
        private void Warn(Exception ex) { Event("Error"); _Logging.Warn("[CrawlCache] operation failed (" + ex.GetType().Name + "); continuing without cache."); }

        /// <inheritdoc />
        public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
        /// <summary>Dispose owned storage after active requests and cleanup have stopped.</summary>
        /// <param name="disposing">Whether to release managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing && _Store.IsValueCreated) _Store.Value.Dispose();
            _Disposed = true;
        }
    }
}
