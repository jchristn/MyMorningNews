namespace MyMorningNews.Server.Workers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Durable billing callback processing and missed-notification recovery.</summary>
    public class BillingWorker
    {
        private readonly ServerContext _Server;

        /// <summary>Construct with the running server context.</summary>
        public BillingWorker(ServerContext server) { _Server = server ?? throw new ArgumentNullException(nameof(server)); }

        /// <summary>Process due events every 15 seconds until canceled. Failed operations retain their inbox leases for recovery.</summary>
        public async Task RunAsync(CancellationToken token)
        {
            DateTime recovery = DateTime.MinValue;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (DateTime.UtcNow >= recovery)
                    {
                        await _Server.Billing.QueueRecoveryAsync(token).ConfigureAwait(false);
                        recovery = DateTime.UtcNow.AddMinutes(1);
                    }
                    await _Server.Billing.ProcessAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                catch (Exception ex) { _Server.Logging.Warn("[BillingWorker] " + ex.GetType().Name + "; pending work will retry."); }
                try { await Task.Delay(TimeSpan.FromSeconds(15), token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
            }
        }
    }
}
