namespace MyMorningNews.Server.Workers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Catalog;

    /// <summary>Deliver newly published shared issues through a durable, account-scoped queue.</summary>
    public class CatalogDeliveryWorker
    {
        private readonly ServerContext _Server;
        private readonly CatalogDeliveryService _Delivery;

        /// <summary>Bind to the running server's services.</summary>
        public CatalogDeliveryWorker(ServerContext server)
        {
            _Server = server ?? throw new ArgumentNullException(nameof(server));
            _Delivery = new CatalogDeliveryService(server.Database, server.Settings, server.Email, server.Push);
        }

        /// <summary>Discover and deliver every 15 seconds until canceled; leases survive worker restarts.</summary>
        public async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try { await _Delivery.ProcessAsync(token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                catch (Exception ex) { _Server.Logging.Warn("[CatalogDeliveryWorker] " + ex.GetType().Name + "; pending deliveries will retry."); }
                try { await Task.Delay(TimeSpan.FromSeconds(15), token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
            }
        }
    }
}
