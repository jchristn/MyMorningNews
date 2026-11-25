namespace MyMorningNews.Server.Workers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Bounded crawl content retention and integrity verification, independent of artifact retention.</summary>
    public class CrawlCacheWorker
    {
        private readonly ServerContext _Server;
        /// <summary>Create the worker.</summary>
        /// <param name="server">Server context.</param>
        public CrawlCacheWorker(ServerContext server) { _Server = server ?? throw new ArgumentNullException(nameof(server)); }

        /// <summary>Run cleanup until server cancellation. Failures are retried on the next interval.</summary>
        /// <param name="token">Server cancellation token.</param>
        public async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_Server.Fetch.Cache != null) await _Server.Fetch.Cache.CleanupAsync(token).ConfigureAwait(false);
                    await Task.Delay(TimeSpan.FromSeconds(_Server.Settings.CrawlCache.CleanupIntervalSeconds), token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                catch (Exception ex)
                {
                    _Server.Logging.Warn("[CrawlCacheWorker] cleanup failed (" + ex.GetType().Name + "); retrying next interval.");
                    try { await Task.Delay(TimeSpan.FromSeconds(_Server.Settings.CrawlCache.CleanupIntervalSeconds), token).ConfigureAwait(false); }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                }
            }
        }
    }
}
