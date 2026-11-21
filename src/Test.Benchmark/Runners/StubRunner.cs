namespace MyMorningNews.Benchmark.Runners
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Benchmark.Datasets;
    using MyMorningNews.Benchmark.Stub;

    /// <summary>
    /// The stub command: runs the stub on its own (model, search, pages, speech) for manual testing, until Ctrl+C.
    /// Faults can be set while it runs with POST /_stub/faults?target=Model&amp;mode=Down. --stub-bind chooses the
    /// listening address (default 127.0.0.1; "*" for every interface) and --stub-host the host name in the URLs it serves
    /// (for a Docker stack, host.docker.internal).
    /// </summary>
    public static class StubRunner
    {
        #region Public-Methods

        /// <summary>
        /// Run the command.
        /// </summary>
        /// <param name="ctx">Context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Exit code.</returns>
        public static async Task<int> RunAsync(BenchmarkContext ctx, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            DatasetLoader loader = new DatasetLoader(ctx.Args.GetString("dataset", "benchmarks/datasets/mymorningnews-synthetic.json"));
            string bind = ctx.Args.GetString("stub-bind", "127.0.0.1");
            string? host = ctx.Args.GetString("stub-host", String.Empty);
            using (BenchStubServer stub = new BenchStubServer(ctx.StubPort, loader.Dataset, bind, String.IsNullOrWhiteSpace(host) ? null : host))
            {
                stub.Faults.SlowLatencyMs = ctx.Args.GetInt("slow-ms", 1500, 0, 120000);
                stub.Faults.BaseModelLatencyMs = ctx.Args.GetInt("model-latency-ms", 0, 0, 60000);
                Console.WriteLine("Stub listening on " + bind + ":" + ctx.StubPort + ", serving " + stub.BaseUrl + " (dataset " + loader.Dataset.Name + ")");
                if (bind == "*" || bind == "+") Console.WriteLine("  Listening on every interface: anyone who can reach this machine on port " + ctx.StubPort + " can use the stub.");
                Console.WriteLine("  model:   " + stub.ModelUrl + " (chat " + BenchStubServer.ChatModel + ", embeddings " + BenchStubServer.EmbeddingModel + ")");
                foreach (BenchmarkCorpus c in loader.Dataset.Corpora)
                    for (int r = 1; r <= c.Rounds; r++) Console.WriteLine("  search:  " + stub.Index()!.SearchEndpoint(c.Id, r) + "   (SearXNG provider endpoint for " + c.Id + " round " + r + ")");
                Console.WriteLine("  speech:  " + stub.ModelUrl + "/audio/speech");
                Console.WriteLine("  state:   " + stub.BaseUrl + "/_stub/state; faults: POST " + stub.BaseUrl + "/_stub/faults?target=Model&mode=Down");
                Console.WriteLine("Press Ctrl+C to stop.");
                try
                {
                    await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }

            return ExitCodes.Success;
        }

        #endregion
    }
}
