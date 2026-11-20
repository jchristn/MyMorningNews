namespace MyMorningNews.Benchmark.Runners
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Benchmark.Client;

    /// <summary>
    /// One timed load phase over a set of read operations, closed loop (N workers, each waits for its response) or open
    /// loop (a fixed arrival rate, latency measured from the scheduled send time so stalls are not hidden). Each phase
    /// runs an untimed warmup first and drains in-flight operations at the deadline instead of cancelling them.
    /// Throughput counts only successful operations over the time actually measured.
    /// </summary>
    public class LoadPhase
    {
        #region Public-Members

        /// <summary>
        /// Latencies of successful operations, in milliseconds.
        /// </summary>
        public ConcurrentBag<double> Latencies { get; } = new ConcurrentBag<double>();

        /// <summary>
        /// Failed operations.
        /// </summary>
        public int Errors
        {
            get { return Volatile.Read(ref _Errors); }
        }

        /// <summary>
        /// Error statuses seen, by status (0 means no response).
        /// </summary>
        public ConcurrentDictionary<int, int> ErrorStatuses { get; } = new ConcurrentDictionary<int, int>();

        /// <summary>
        /// Seconds actually measured (from the start until the last in-flight operation finished).
        /// </summary>
        public double MeasuredSeconds { get; private set; } = 0;

        #endregion

        #region Private-Members

        private readonly MyMorningNewsClient _Client;
        private readonly IReadOnlyList<string> _Paths;
        private int _Errors = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <param name="paths">GET paths to cycle through.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException">paths is empty.</exception>
        public LoadPhase(MyMorningNewsClient client, IReadOnlyList<string> paths)
        {
            _Client = client ?? throw new ArgumentNullException(nameof(client));
            _Paths = paths ?? throw new ArgumentNullException(nameof(paths));
            if (paths.Count == 0) throw new ArgumentException("At least one path is required.", nameof(paths));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Closed loop at a concurrency level.
        /// </summary>
        /// <param name="workers">Workers.</param>
        /// <param name="warmup">Warmup length.</param>
        /// <param name="duration">Measured length.</param>
        /// <param name="seed">Seed for the operation order.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task ClosedAsync(int workers, TimeSpan warmup, TimeSpan duration, int seed, CancellationToken token)
        {
            await LoopAsync(workers, warmup, seed, false, token).ConfigureAwait(false);
            Stopwatch sw = Stopwatch.StartNew();
            await LoopAsync(workers, duration, seed + 1, true, token).ConfigureAwait(false);
            MeasuredSeconds = sw.Elapsed.TotalSeconds;
        }

        /// <summary>
        /// Open loop at a fixed arrival rate.
        /// </summary>
        /// <param name="ratePerSecond">Arrivals per second.</param>
        /// <param name="warmup">Warmup length.</param>
        /// <param name="duration">Measured length.</param>
        /// <param name="seed">Seed for the operation order.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task OpenAsync(double ratePerSecond, TimeSpan warmup, TimeSpan duration, int seed, CancellationToken token)
        {
            await ScheduleAsync(ratePerSecond, warmup, seed, false, token).ConfigureAwait(false);
            Stopwatch sw = Stopwatch.StartNew();
            await ScheduleAsync(ratePerSecond, duration, seed + 1, true, token).ConfigureAwait(false);
            MeasuredSeconds = sw.Elapsed.TotalSeconds;
        }

        #endregion

        #region Private-Methods

        private async Task LoopAsync(int workers, TimeSpan length, int seed, bool record, CancellationToken token)
        {
            DateTime deadline = DateTime.UtcNow + length;
            List<Task> tasks = new List<Task>();
            for (int w = 0; w < workers; w++)
            {
                int worker = w;
                tasks.Add(Task.Run(async () =>
                {
                    Random rng = new Random(seed * 7919 + worker);
                    while (DateTime.UtcNow < deadline && !token.IsCancellationRequested)
                    {
                        string path = _Paths[rng.Next(_Paths.Count)];
                        ApiResult r = await _Client.GetAsync(path, CancellationToken.None).ConfigureAwait(false);
                        if (record) Record(r, r.DurationMs);
                    }
                }, token));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private async Task ScheduleAsync(double rate, TimeSpan length, int seed, bool record, CancellationToken token)
        {
            Random rng = new Random(seed);
            double interval = 1000.0 / Math.Max(0.1, rate);
            int total = (int)Math.Floor(length.TotalMilliseconds / interval);
            Stopwatch clock = Stopwatch.StartNew();
            List<Task> inflight = new List<Task>();
            for (int i = 0; i < total && !token.IsCancellationRequested; i++)
            {
                double scheduled = i * interval;
                double wait = scheduled - clock.Elapsed.TotalMilliseconds;
                if (wait > 3) await Task.Delay(TimeSpan.FromMilliseconds(wait - 2), token).ConfigureAwait(false);
                while (clock.Elapsed.TotalMilliseconds < scheduled) Thread.SpinWait(50);
                string path = _Paths[rng.Next(_Paths.Count)];
                inflight.Add(Task.Run(async () =>
                {
                    ApiResult r = await _Client.GetAsync(path, CancellationToken.None).ConfigureAwait(false);
                    if (record) Record(r, clock.Elapsed.TotalMilliseconds - scheduled);
                }, CancellationToken.None));
            }

            await Task.WhenAll(inflight).ConfigureAwait(false);
        }

        private void Record(ApiResult r, double latencyMs)
        {
            if (r.Success) Latencies.Add(latencyMs);
            else
            {
                Interlocked.Increment(ref _Errors);
                ErrorStatuses.AddOrUpdate(r.Status, 1, (k, v) => v + 1);
            }
        }

        #endregion
    }
}
