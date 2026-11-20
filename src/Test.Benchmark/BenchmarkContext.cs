namespace MyMorningNews.Benchmark
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Benchmark.Client;
    using MyMorningNews.Benchmark.Reporting;
    using MyMorningNews.Benchmark.Runners;

    /// <summary>
    /// Shared state for one command: the arguments, one pooled HTTP client with explicit limits, the administration
    /// client, the common options, and provenance capture. Dispose to release the HTTP client.
    /// </summary>
    public class BenchmarkContext : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Arguments.
        /// </summary>
        public BenchmarkArguments Args { get; }

        /// <summary>
        /// Pooled HTTP client.
        /// </summary>
        public HttpClient Http { get; }

        /// <summary>
        /// Server base URL. Default http://127.0.0.1:12500 (--server).
        /// </summary>
        public string ServerUrl { get; }

        /// <summary>
        /// Prometheus exporter URL, or "none". Default http://127.0.0.1:12509/metrics (--metrics-url).
        /// </summary>
        public string MetricsUrl { get; }

        /// <summary>
        /// Stub port. Default 12503 (--stub-port).
        /// </summary>
        public int StubPort { get; }

        /// <summary>
        /// Report directory. Default benchmarks/results (--output-dir).
        /// </summary>
        public string OutputDir { get; }

        /// <summary>
        /// Report label. Default empty (--label).
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Bootstrap resamples. Default 2000 (--resamples).
        /// </summary>
        public int Resamples { get; }

        /// <summary>
        /// Seed for every random choice. Default 20261120 (--seed).
        /// </summary>
        public int Seed { get; }

        /// <summary>
        /// Run timeout. Default 300 s (--run-timeout-seconds).
        /// </summary>
        public TimeSpan RunTimeout { get; }

        /// <summary>
        /// Administration client.
        /// </summary>
        public MyMorningNewsClient Admin { get; }

        /// <summary>
        /// Paid mode settings (disabled unless --paid).
        /// </summary>
        public PaidMode Paid { get; }

        /// <summary>
        /// HTTP client description for the report.
        /// </summary>
        public string HttpDescription { get; }

        /// <summary>
        /// Command start time.
        /// </summary>
        public DateTime StartUtc { get; } = DateTime.UtcNow;

        #endregion

        #region Private-Members

        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate from arguments.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <exception cref="ArgumentNullException">args is null.</exception>
        public BenchmarkContext(BenchmarkArguments args)
        {
            Args = args ?? throw new ArgumentNullException(nameof(args));
            ServerUrl = args.GetString("server", "http://127.0.0.1:12500").TrimEnd('/');
            MetricsUrl = args.GetString("metrics-url", "http://127.0.0.1:12509/metrics");
            StubPort = args.GetInt("stub-port", 12503, 1, 65535);
            OutputDir = RepositoryPaths.Resolve(args.GetString("output-dir", RepositoryPaths.Results()));
            Label = args.GetString("label", String.Empty);
            Resamples = args.GetInt("resamples", 2000, 100, 100000);
            Seed = args.GetInt("seed", 20261120, Int32.MinValue, Int32.MaxValue);
            RunTimeout = TimeSpan.FromSeconds(args.GetInt("run-timeout-seconds", 300, 10, 7200));
            int connections = args.GetInt("max-connections", 256, 1, 4096);
            int timeout = args.GetInt("http-timeout-seconds", 120, 1, 3600);
            string adminKey = args.GetString("admin-key", Environment.GetEnvironmentVariable("MYMORNINGNEWS_BENCH_ADMIN_KEY") ?? "mymorningnews-benchmark-admin-key-not-for-production");
            SocketsHttpHandler handler = new SocketsHttpHandler
            {
                MaxConnectionsPerServer = connections,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
                AllowAutoRedirect = false
            };
            Http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(timeout) };
            HttpDescription = "SocketsHttpHandler, " + connections + " connections per server, " + timeout + " s timeout, 2 min pooled lifetime";
            Admin = new MyMorningNewsClient(Http, ServerUrl, null, adminKey, null);
            Paid = PaidMode.From(args);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Wait for the server's health check to pass.
        /// </summary>
        /// <param name="timeout">How long to wait.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when healthy.</returns>
        public async Task<bool> WaitForServerAsync(TimeSpan timeout, CancellationToken token)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                ApiResult r = await Admin.HealthAsync(token).ConfigureAwait(false);
                if (r.Success) return true;
                await Task.Delay(1000, token).ConfigureAwait(false);
            }

            return false;
        }

        /// <summary>
        /// Capture provenance for a report.
        /// </summary>
        /// <param name="dataset">Dataset name.</param>
        /// <param name="datasetSha256">Dataset hash, or empty.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Provenance.</returns>
        public async Task<Provenance> CaptureProvenanceAsync(string dataset, string datasetSha256, CancellationToken token)
        {
            string root = RepositoryPaths.Root();
            Provenance p = new Provenance
            {
                StartUtc = StartUtc,
                SutUrl = ServerUrl,
                Machine = Environment.MachineName,
                Os = RuntimeInformation.OSDescription + " " + RuntimeInformation.OSArchitecture,
                Cpu = CpuModel() + ", " + Environment.ProcessorCount + " logical cores",
                Runtime = RuntimeInformation.FrameworkDescription,
                Dataset = dataset,
                DatasetSha256 = datasetSha256,
                CommandLine = Args.CommandLine,
                HttpClient = HttpDescription,
                HarnessVersion = typeof(BenchmarkContext).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0] ?? "unknown"
            };

            string? head = ProcessRunner.Run("git", new[] { "rev-parse", "HEAD" }, root);
            string? status = ProcessRunner.Run("git", new[] { "status", "--porcelain", "--untracked-files=all" }, root);
            p.ProductCommit = head == null ? "unknown" : head + (String.IsNullOrEmpty(status) ? String.Empty : "-dirty");
            string? harness = ProcessRunner.Run("git", new[] { "log", "-1", "--format=%H", "--", "src/Test.Benchmark", "benchmarks" }, root);
            string? harnessStatus = ProcessRunner.Run("git", new[] { "status", "--porcelain", "--untracked-files=all", "--", "src/Test.Benchmark", "benchmarks" }, root);
            p.HarnessCommit = (String.IsNullOrEmpty(harness) ? "uncommitted" : harness) + (String.IsNullOrEmpty(harnessStatus) ? String.Empty : "-dirty");

            string? names = ProcessRunner.Run("docker", new[] { "ps", "--filter", "label=com.docker.compose.project=mymorningnews-bench", "--format", "{{.Names}}" });
            if (names != null)
            {
                foreach (string name in names.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    string? image = ProcessRunner.Run("docker", new[] { "inspect", "--format", "{{.Config.Image}} {{.Image}}", name });
                    string? repo = image == null ? null : ProcessRunner.Run("docker", new[] { "image", "inspect", "--format", "{{join .RepoDigests \",\"}}", image.Split(' ')[0] });
                    p.Images[name] = (image ?? "unknown") + (String.IsNullOrEmpty(repo) ? String.Empty : " (" + repo + ")");
                }
            }

            ApiResult info = await Admin.ServerInfoAsync(token).ConfigureAwait(false);
            if (info.Success)
            {
                using (JsonDocument doc = JsonDocument.Parse(info.Body))
                {
                    JsonElement r = doc.RootElement;
                    string version = r.TryGetProperty("version", out JsonElement v) ? v.GetString() ?? "unknown" : "unknown";
                    string runtime = r.TryGetProperty("runtimeVersion", out JsonElement rt) ? rt.GetString() ?? String.Empty : String.Empty;
                    int schema = r.TryGetProperty("schemaVersion", out JsonElement sv) && sv.ValueKind == JsonValueKind.Number ? sv.GetInt32() : 0;
                    p.SutVersion = version + ", schema " + schema.ToString(CultureInfo.InvariantCulture) + (runtime.Length > 0 ? ", " + runtime : String.Empty);
                }
            }

            p.Services["model"] = Paid.Enabled ? Paid.Provider + " " + Paid.Model + " (paid, cap $" + Paid.MaxSpendUsd.ToString("0.00", CultureInfo.InvariantCulture) + ")" : "stub " + Stub.BenchStubServer.ChatModel + " (" + Stub.BenchStubServer.Version + ")";
            p.Services["embeddings"] = "stub " + Stub.BenchStubServer.EmbeddingModel;
            p.Services["search, pages, speech"] = "stub on 127.0.0.1:" + StubPort.ToString(CultureInfo.InvariantCulture);
            return p;
        }

        /// <summary>
        /// Fill a report's provenance and options, summarize it, and write it.
        /// </summary>
        /// <param name="report">Report.</param>
        /// <param name="datasetSha256">Dataset hash, or empty.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON path.</returns>
        public async Task<string> FinishReportAsync(BenchmarkReport report, string datasetSha256, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(report);
            bool scrub = Args.GetFlag("scrub");
            report.Provenance = await CaptureProvenanceAsync(report.Dataset, datasetSha256, token).ConfigureAwait(false);
            report.Label = Label;
            report.Mode = Paid.Enabled ? "paid" : "stub";
            report.Options = new SortedDictionary<string, string>(Args.Used, StringComparer.Ordinal);
            report.DurationSeconds = Math.Round((DateTime.UtcNow - StartUtc).TotalSeconds, 1);
            if (scrub) Scrub(report);
            Summarizer.Summarize(report, Resamples, Seed);
            return ReportWriter.Write(report, OutputDir);
        }

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        /// <param name="disposing">True when called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing) Http.Dispose();
            _Disposed = true;
        }

        #endregion

        #region Private-Methods

        private static void Scrub(BenchmarkReport report)
        {
            string root = RepositoryPaths.Root();
            report.Provenance.Machine = "(scrubbed)";
            report.Provenance.CommandLine = report.Provenance.CommandLine.Replace(root, "<repo>", StringComparison.Ordinal);
            foreach (string key in report.Options.Keys.ToList()) report.Options[key] = report.Options[key].Replace(root, "<repo>", StringComparison.Ordinal);
            foreach (ProbeOutcome p in report.Probes) p.Detail = p.Detail.Replace(root, "<repo>", StringComparison.Ordinal);
        }

        private static string CpuModel()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return ProcessRunner.Run("sysctl", new[] { "-n", "machdep.cpu.brand_string" }) ?? "unknown CPU";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && File.Exists("/proc/cpuinfo"))
            {
                string? line = File.ReadLines("/proc/cpuinfo").FirstOrDefault(l => l.StartsWith("model name", StringComparison.Ordinal));
                if (line != null) return line.Substring(line.IndexOf(':') + 1).Trim();
            }

            return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? RuntimeInformation.ProcessArchitecture.ToString();
        }

        #endregion
    }
}
