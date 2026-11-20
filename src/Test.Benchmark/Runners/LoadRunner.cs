namespace MyMorningNews.Benchmark.Runners
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Benchmark.Client;
    using MyMorningNews.Benchmark.Datasets;
    using MyMorningNews.Benchmark.Metrics;
    using MyMorningNews.Benchmark.Reporting;
    using MyMorningNews.Benchmark.Stub;

    /// <summary>
    /// The load command. Phase 1, API reads: the mix a dashboard makes (topics, findings, runs, briefings, the run's
    /// stages), closed loop at each --concurrency level and, with --open-loop, at each --rate. Phase 2, research runs:
    /// --runs topics queued at once, reporting submit-to-finish latency, queue wait, and runs per minute, with the
    /// server's stage histograms diffed for attribution. Every number here depends on the machine; compare only on the
    /// same machine. Excluded from the CI gate.
    /// </summary>
    public static class LoadRunner
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
            if (ctx.Paid.Enabled) { Console.Error.WriteLine("load always runs on stubs; remove --paid."); return ExitCodes.Usage; }
            DatasetLoader loader = new DatasetLoader(ctx.Args.GetString("dataset", "benchmarks/datasets/mymorningnews-synthetic.json"));
            string corpusId = ctx.Args.GetString("corpus", loader.Dataset.Corpora[0].Id);
            List<int> levels = ctx.Args.GetIntList("concurrency", "1,4,16");
            bool open = ctx.Args.GetFlag("open-loop");
            List<int> rates = ctx.Args.GetIntList("rate", "25,100");
            TimeSpan warmup = TimeSpan.FromSeconds(ctx.Args.GetInt("warmup-seconds", 5, 0, 600));
            TimeSpan duration = TimeSpan.FromSeconds(ctx.Args.GetInt("duration-seconds", 15, 1, 3600));
            int runs = ctx.Args.GetInt("runs", 6, 0, 200);
            int modelLatency = ctx.Args.GetInt("model-latency-ms", 0, 0, 60000);
            BenchmarkCorpus? corpus = loader.Dataset.Corpora.FirstOrDefault(c => c.Id == corpusId);
            if (corpus == null) { Console.Error.WriteLine("Unknown corpus " + corpusId + "."); return ExitCodes.Usage; }
            if (!await ctx.WaitForServerAsync(TimeSpan.FromSeconds(60), token).ConfigureAwait(false)) { Console.Error.WriteLine("The server at " + ctx.ServerUrl + " is not healthy."); return ExitCodes.Failure; }

            BenchmarkReport report = new BenchmarkReport { Kind = "load", Dataset = loader.Dataset.Name, Arms = new List<string> { "default" } };
            using (BenchStubServer stub = new BenchStubServer(ctx.StubPort, loader.Dataset))
            {
                stub.Faults.BaseModelLatencyMs = modelLatency;
                Provisioner provisioner = new Provisioner(ctx, stub, loader.Dataset.Name, loader.Sha256);
                ProductArm arm = ProductArm.Find("default")!;
                Console.WriteLine("[load] provisioning the read workspace");
                Workspace ws = await provisioner.CreateAsync(corpus, arm, "load", token).ConfigureAwait(false);
                RunDto seedRun = await provisioner.RunRoundAsync(ws, corpus, 1, token).ConfigureAwait(false);
                MyMorningNewsClient user = await provisioner.SessionAsync(ws, token).ConfigureAwait(false);
                List<string> paths = new List<string>
                {
                    "/v1.0/api/topics?pageSize=25",
                    "/v1.0/api/topics/" + ws.TopicId,
                    "/v1.0/api/findings?topicId=" + ws.TopicId + "&pageSize=25",
                    "/v1.0/api/runs?topicId=" + ws.TopicId + "&pageSize=25",
                    "/v1.0/api/runs/" + seedRun.Id + "/stages",
                    "/v1.0/api/briefings?pageSize=25"
                };

                foreach (int c in levels)
                {
                    Console.WriteLine("[load] closed loop, " + c + " workers, " + duration.TotalSeconds + " s after a " + warmup.TotalSeconds + " s warmup");
                    LoadPhase phase = new LoadPhase(user, paths);
                    await phase.ClosedAsync(c, warmup, duration, ctx.Seed, token).ConfigureAwait(false);
                    report.Probes.Add(Outcome("reads-closed-c" + c, "closed-loop", phase, c));
                }

                if (open)
                {
                    foreach (int rate in rates)
                    {
                        Console.WriteLine("[load] open loop, " + rate + " requests per second");
                        LoadPhase phase = new LoadPhase(user, paths);
                        await phase.OpenAsync(rate, warmup, duration, ctx.Seed, token).ConfigureAwait(false);
                        ProbeOutcome o = Outcome("reads-open-r" + rate, "open-loop", phase, rate);
                        o.Metrics["offeredRate"] = rate;
                        report.Probes.Add(o);
                    }
                }

                if (runs > 0)
                {
                    Console.WriteLine("[load] " + runs + " research runs queued at once");
                    List<Workspace> spaces = new List<Workspace>();
                    for (int i = 1; i <= runs; i++) spaces.Add(await provisioner.CreateAsync(corpus, arm, "load-run" + i, token).ConfigureAwait(false));
                    PrometheusSnapshot before = await PrometheusSnapshot.ScrapeAsync(ctx.Http, ctx.MetricsUrl, token).ConfigureAwait(false);
                    Stopwatch sw = Stopwatch.StartNew();
                    List<Task<RunDto?>> tasks = spaces.Select(async s =>
                    {
                        MyMorningNewsClient c = await provisioner.SessionAsync(s, token).ConfigureAwait(false);
                        await c.UpdateSearchProviderAsync(s.ProviderId, stub.Index()!.SearchEndpoint(corpus.Id, 1), token).ConfigureAwait(false);
                        ApiResult q = await c.RunTopicAsync(s.TopicId, false, token).ConfigureAwait(false);
                        if (!q.Success) return null;
                        try
                        {
                            return await c.WaitForRunAsync(q.Json<RunDto>().Id, ctx.RunTimeout, 250, token).ConfigureAwait(false);
                        }
                        catch (TimeoutException)
                        {
                            return null;
                        }
                    }).ToList();
                    RunDto?[] finished = await Task.WhenAll(tasks).ConfigureAwait(false);
                    double elapsed = sw.Elapsed.TotalSeconds;
                    await Task.Delay(2500, token).ConfigureAwait(false);
                    PrometheusSnapshot after = await PrometheusSnapshot.ScrapeAsync(ctx.Http, ctx.MetricsUrl, token).ConfigureAwait(false);
                    for (int i = 0; i < spaces.Count; i++) if (finished[i] != null) await provisioner.GuardRunAsync(spaces[i], finished[i]!, token).ConfigureAwait(false);
                    List<RunDto> ok = finished.Where(r => r != null && (r.Status == "Completed" || r.Status == "PartiallySucceeded")).Select(r => r!).ToList();
                    ProbeOutcome o = new ProbeOutcome { Arm = "default", Corpus = "load", ProbeId = "runs-" + runs, Type = "runs", Category = "pipeline" };
                    o.Latency = LatencyStats.From(ok.Where(r => r.CompletedUtc.HasValue).Select(r => (r.CompletedUtc!.Value - r.QueuedUtc).TotalMilliseconds));
                    o.Score = runs == 0 ? 1 : (double)ok.Count / runs;
                    o.Metrics["throughput"] = Math.Round(ok.Count / Math.Max(0.001, elapsed) * 60.0, 3);
                    o.Metrics["errors"] = runs - ok.Count;
                    o.Metrics["queueWaitP95Ms"] = LatencyStats.From(ok.Where(r => r.StartedUtc.HasValue).Select(r => (r.StartedUtc!.Value - r.QueuedUtc).TotalMilliseconds)).P95;
                    o.Metrics["serverP95Ms"] = LatencyStats.From(ok.Select(r => r.DurationMs)).P95;
                    o.Detail = ok.Count + " of " + runs + " runs finished in " + elapsed.ToString("0.0", CultureInfo.InvariantCulture) + " s (throughput is runs per minute; latency is queue to completion, server timestamps)";
                    report.Probes.Add(o);
                    report.Stages = StageMetrics.Diff(before, after, "run_stage_duration", "stage");
                    report.Stages.AddRange(StageMetrics.Diff(before, after, "llm_call_duration", "task").Select(s => { s.Name = "model:" + s.Name; return s; }));
                }
            }

            report.Notes.Add("Closed-loop numbers hide stalls (coordinated omission); use --open-loop for latency claims. Latency is client side for reads and from server timestamps for runs. Model calls go to the in-process stub" +
                (modelLatency > 0 ? " with " + modelLatency + " ms added per call" : " with no added latency") + ", so run throughput reflects the server and database, not a model.");
            report.Notes.Add("The server's MaxConcurrentRuns and Scheduler.RunPollSeconds bound run throughput; record them with the result.");
            string path = await ctx.FinishReportAsync(report, loader.Sha256, token).ConfigureAwait(false);
            foreach (ProbeOutcome p in report.Probes.Where(p => p.Latency != null))
                Console.WriteLine("  " + p.ProbeId.PadRight(20) + " p50 " + p.Latency!.P50 + " ms, p95 " + p.Latency.P95 + " ms, p99 " + p.Latency.P99 + " ms, " + String.Join(", ", p.Metrics.Select(kv => kv.Key + " " + kv.Value.ToString("0.###", CultureInfo.InvariantCulture))));
            Console.WriteLine("Report: " + path);
            return ExitCodes.Success;
        }

        #endregion

        #region Private-Methods

        private static ProbeOutcome Outcome(string id, string type, LoadPhase phase, int level)
        {
            List<double> lat = phase.Latencies.ToList();
            int total = lat.Count + phase.Errors;
            ProbeOutcome o = new ProbeOutcome { Arm = "default", Corpus = "load", ProbeId = id, Type = type, Category = "api-reads" };
            o.Latency = LatencyStats.From(lat);
            o.Score = total == 0 ? 0 : (double)lat.Count / total;
            o.Metrics["throughput"] = Math.Round(lat.Count / Math.Max(0.001, phase.MeasuredSeconds), 2);
            o.Metrics["errors"] = phase.Errors;
            o.Metrics["level"] = level;
            o.Detail = lat.Count + " ok, " + phase.Errors + " errors" + (phase.ErrorStatuses.Count > 0 ? " (" + String.Join(", ", phase.ErrorStatuses.Select(kv => kv.Key + ": " + kv.Value)) + ")" : String.Empty) +
                " over " + phase.MeasuredSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s; throughput is successful requests per second";
            return o;
        }

        #endregion
    }
}
