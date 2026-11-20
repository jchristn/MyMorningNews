namespace MyMorningNews.Benchmark.Runners
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Benchmark.Client;
    using MyMorningNews.Benchmark.Datasets;
    using MyMorningNews.Benchmark.Metrics;
    using MyMorningNews.Benchmark.Reference;
    using MyMorningNews.Benchmark.Reporting;
    using MyMorningNews.Benchmark.Stub;

    /// <summary>
    /// The ingest and quality commands. Both research every corpus in every product arm (reusing intact workspaces
    /// unless --reingest); ingest reports the load path (search, fetch, extract, reachability, run time) and quality
    /// scores every probe in every arm, including the two reference arms the harness computes without the product.
    /// </summary>
    public static class QualityRunner
    {
        #region Public-Methods

        /// <summary>
        /// Run the command.
        /// </summary>
        /// <param name="ctx">Context.</param>
        /// <param name="kind">ingest or quality.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Exit code.</returns>
        public static async Task<int> RunAsync(BenchmarkContext ctx, string kind, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            DatasetLoader loader = new DatasetLoader(ctx.Args.GetString("dataset", "benchmarks/datasets/mymorningnews-synthetic.json"));
            List<string> armNames = ctx.Args.GetList("arms", "default,llm-only");
            bool references = !ctx.Args.GetFlag("no-reference");
            bool reingest = ctx.Args.GetFlag("reingest");
            List<string> only = ctx.Args.GetList("corpora", String.Empty);
            List<string> problems = DatasetValidator.Validate(loader.Dataset);
            if (problems.Count > 0)
            {
                Console.Error.WriteLine("Dataset " + loader.Path + " is invalid:\n  " + String.Join("\n  ", problems));
                return ExitCodes.Failure;
            }

            List<ProductArm> arms = new List<ProductArm>();
            foreach (string n in armNames)
            {
                ProductArm? a = ProductArm.Find(n);
                if (a == null) { Console.Error.WriteLine("Unknown arm " + n + ". Known: " + String.Join(", ", ProductArm.All.Select(x => x.Name))); return ExitCodes.Usage; }
                arms.Add(a);
            }

            if (!await ctx.WaitForServerAsync(TimeSpan.FromSeconds(60), token).ConfigureAwait(false))
            {
                Console.Error.WriteLine("The server at " + ctx.ServerUrl + " is not healthy. Start it with benchmarks/start-bench-server.sh (or .bat).");
                return ExitCodes.Failure;
            }

            List<BenchmarkCorpus> corpora = loader.Dataset.Corpora.Where(c => only.Count == 0 || only.Contains(c.Id)).ToList();
            BenchmarkReport report = new BenchmarkReport { Kind = kind, Dataset = loader.Dataset.Name };
            using (BenchStubServer stub = new BenchStubServer(ctx.StubPort, loader.Dataset))
            {
                stub.Brain.ExpectedGuardrail = await ctx.Admin.ScopeGuardrailAsync(token).ConfigureAwait(false);
                if (stub.Brain.ExpectedGuardrail.Length == 0) report.Notes.Add("The scope guardrail could not be read from /v1.0/api/settings, so guardrail probes accept any system prompt.");
                Provisioner provisioner = new Provisioner(ctx, stub, loader.Dataset.Name, loader.Sha256);
                PrometheusSnapshot before = await PrometheusSnapshot.ScrapeAsync(ctx.Http, ctx.MetricsUrl, token).ConfigureAwait(false);
                Dictionary<string, CorpusRun> runs = new Dictionary<string, CorpusRun>(StringComparer.Ordinal);
                try
                {
                    foreach (ProductArm arm in arms)
                    {
                        foreach (BenchmarkCorpus corpus in corpora)
                        {
                            Console.WriteLine("[" + kind + "] " + corpus.Id + " / " + arm.Name);
                            CorpusRun run = await CorpusRunner.RunAsync(provisioner, stub.Index()!, corpus, arm, reingest, Console.WriteLine, token).ConfigureAwait(false);
                            runs[arm.Name + "/" + corpus.Id] = run;
                            report.IncompleteReasons.AddRange(run.IncompleteReasons);
                        }
                    }
                }
                catch (SpendCapReachedException ex)
                {
                    report.IncompleteReasons.Add(ex.Message);
                }

                await Task.Delay(2500, token).ConfigureAwait(false);
                PrometheusSnapshot after = await PrometheusSnapshot.ScrapeAsync(ctx.Http, ctx.MetricsUrl, token).ConfigureAwait(false);
                report.Stages = StageMetrics.Diff(before, after, "run_stage_duration", "stage");
                if (!after.Available) report.Notes.Add("The metrics endpoint " + ctx.MetricsUrl + " could not be scraped; no stage breakdown.");
                if (runs.Values.All(r => r.Reused)) report.Notes.Add("Every workspace was reused, so the stage breakdown covers no runs. Pass --reingest for fresh timings.");
                report.Arms = arms.Select(a => a.Name).ToList();

                if (kind == "ingest") Ingest(report, runs, stub.Index()!, corpora, arms);
                else
                {
                    foreach (ProductArm arm in arms)
                    {
                        foreach (BenchmarkCorpus corpus in corpora)
                        {
                            if (!runs.TryGetValue(arm.Name + "/" + corpus.Id, out CorpusRun? run)) continue;
                            QualityScorer scorer = new QualityScorer(corpus, stub.Index()!);
                            bool stubModel = !ctx.Paid.Enabled;
                            report.Probes.AddRange(scorer.Score(arm.Name, run.Rounds, run.Failures, stubModel ? run.Workspace.Quotes : null, stubModel ? run.Workspace.Chats : null, false));
                        }
                    }

                    if (references)
                    {
                        ReferenceArms reference = new ReferenceArms(stub.Index()!, stub.Brain);
                        foreach (string arm in ProductArm.ReferenceArms)
                        {
                            report.Arms.Add(arm);
                            foreach (BenchmarkCorpus corpus in corpora)
                                report.Probes.AddRange(new QualityScorer(corpus, stub.Index()!).Score(arm, reference.Build(arm, corpus), new Dictionary<int, string>(), null, null, true));
                        }
                    }

                    report.Notes.Add("Stub model: relevance is the cosine of feature-hashed word vectors (extract floor " + stub.Brain.ExtractFloor.ToString("0.00", CultureInfo.InvariantCulture) +
                        ", score saturation " + stub.Brain.ScoreSaturation.ToString("0.00", CultureInfo.InvariantCulture) + "); embeddings are " + BenchStubServer.EmbeddingDimensions + "-dimension feature hashes. Numbers measure the product's pipeline under that model, not a real model's judgment.");
                }
            }

            report.Incomplete = report.IncompleteReasons.Count > 0;
            string path = await ctx.FinishReportAsync(report, loader.Sha256, token).ConfigureAwait(false);
            Console.WriteLine();
            foreach (MetricSummary s in report.Summaries.Where(s => s.Group == "overall" || s.Group == "negative" || s.Group == "type:ranking" && s.Metric == "ndcg10"))
                Console.WriteLine("  " + s.Arm.PadRight(20) + " " + (s.Group + " " + s.Metric).PadRight(28) + " " + s.Value + " n=" + s.Value.N);
            Console.WriteLine("Report: " + path);
            if (report.Incomplete)
            {
                Console.WriteLine("Incomplete run: " + report.IncompleteReasons.Count + " failures. These results are not comparable.");
                return ExitCodes.Incomplete;
            }

            return ExitCodes.Success;
        }

        #endregion

        #region Private-Methods

        private static void Ingest(BenchmarkReport report, Dictionary<string, CorpusRun> runs, DatasetIndex index, List<BenchmarkCorpus> corpora, List<ProductArm> arms)
        {
            foreach (ProductArm arm in arms)
            {
                foreach (BenchmarkCorpus corpus in corpora)
                {
                    if (!runs.TryGetValue(arm.Name + "/" + corpus.Id, out CorpusRun? run)) continue;
                    QualityScorer gains = new QualityScorer(corpus, index);
                    for (int round = 1; round <= corpus.Rounds; round++)
                    {
                        ProbeOutcome o = new ProbeOutcome { Arm = arm.Name, Corpus = corpus.Id, ProbeId = "load-r" + round, Type = "load-path", Category = "ingest", Round = round };
                        if (!run.Rounds.TryGetValue(round, out RoundData? data))
                        {
                            o.Score = 0;
                            o.Error = run.Failures.TryGetValue(round, out string? why) ? why : "The round did not run.";
                            report.Probes.Add(o);
                            continue;
                        }

                        List<SearchResultDto> served = data.Results.Where(r => data.ItemByResult.TryGetValue(r.Id, out string? i) && index.Item(corpus.Id, i)?.Url == null).ToList();
                        int fetched = data.Results.Count(r => r.Disposition == "Fetched");
                        int failed = served.Count(r => r.Disposition == "FetchFailed");
                        List<string> reachableNeeded = index.ItemsInRound(corpus.Id, round).Where(i => gains.Gain(round, i.AliasOf ?? i.Id) >= 2).Select(i => i.AliasOf ?? i.Id).Distinct().ToList();
                        int reachable = reachableNeeded.Count(i => data.Results.Any(r => r.Disposition == "Fetched" && data.ItemByResult.TryGetValue(r.Id, out string? id) && (index.Item(corpus.Id, id)?.AliasOf ?? id) == i));
                        List<StubQuoteRecord> quotes = run.Workspace.Quotes.Where(q => q.Corpus == corpus.Id).ToList();
                        o.Score = failed == 0 && (data.Run?.Status == "Completed" || data.Run?.Status == "PartiallySucceeded") ? 1 : 0;
                        o.Metrics["results"] = data.Results.Count;
                        o.Metrics["fetched"] = fetched;
                        o.Metrics["fetchFailed"] = failed;
                        o.Metrics["findings"] = data.Findings.Count(f => !f.IsDuplicate);
                        o.Metrics["duplicates"] = data.Findings.Count(f => f.IsDuplicate);
                        o.Metrics["shown"] = data.Shown().Count;
                        o.Metrics["reachability"] = reachableNeeded.Count == 0 ? 1 : (double)reachable / reachableNeeded.Count;
                        o.Metrics["evidence"] = data.Evidence.Values.Sum(e => e.Count(x => x.Verified));
                        if (data.Run != null) o.Metrics["serverMs"] = Math.Round(data.Run.DurationMs, 1);
                        if (run.RunSeconds.TryGetValue(round, out double secs)) o.Metrics["submitToDoneSeconds"] = secs;
                        if (data.Run?.StartedUtc != null) o.Metrics["queueWaitSeconds"] = Math.Round((data.Run.StartedUtc.Value - data.Run.QueuedUtc).TotalSeconds, 2);
                        o.Detail = data.Run?.Status + "; " + data.Results.Count + " results, " + fetched + " fetched, " + failed + " failed, reachability " + reachable + "/" + reachableNeeded.Count +
                            (run.Reused ? " (reused workspace)" : String.Empty) + (quotes.Count > 0 ? "; " + quotes.Count + " quotes returned" : String.Empty);
                        report.Probes.Add(o);
                    }
                }
            }
        }

        #endregion
    }
}
