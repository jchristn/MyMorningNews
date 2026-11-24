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
    using MyMorningNews.Benchmark.Reporting;
    using MyMorningNews.Benchmark.Stub;

    /// <summary>
    /// The resilience command: each scenario injects one failure into one dependency through the stub (model,
    /// embeddings, search, pages, or speech; slow, down, or rate limited) and checks that the product does what a reader
    /// would want: retries what is transient, degrades what is optional, fails cleanly (a finished run with a message
    /// and JSON from the API, never a hang or an HTML error page) when it must, and recovers when the dependency
    /// returns. Outcomes are deterministic under stubs, so they are part of the CI gate; durations are reported but
    /// not gated. Always runs on stubs.
    /// </summary>
    public static class ResilienceRunner
    {
        #region Public-Members

        /// <summary>
        /// The scenarios.
        /// </summary>
        public static readonly IReadOnlyList<ResilienceScenario> Scenarios = new List<ResilienceScenario>
        {
            new ResilienceScenario { Name = "baseline", Target = FaultTarget.Model, Mode = FaultMode.None, Description = "No fault: the run completes with findings." },
            new ResilienceScenario { Name = "model-429-once", Target = FaultTarget.Model, Mode = FaultMode.RateLimitedOnce, Description = "Every model request is rate limited once: the gateway retries and the run completes." },
            new ResilienceScenario { Name = "model-slow", Target = FaultTarget.Model, Mode = FaultMode.Slow, Description = "Every model response takes 1.5 s longer: the run still completes." },
            new ResilienceScenario { Name = "model-down", Target = FaultTarget.Model, Mode = FaultMode.Down, ExpectStatus = "Failed", ExpectFindings = false, Recovery = true, Description = "The model is down: the run fails cleanly with a message; when the model returns, the next run recovers every finding a healthy run shows (pages the failed run fetched are not lost as already seen)." },
            new ResilienceScenario { Name = "dry-run-first", Target = FaultTarget.Model, Mode = FaultMode.None, DryRunFirst = true, Description = "A dry run (a preview that is not counted in history) comes first: the real run that follows still shows every finding a healthy run shows." },
            new ResilienceScenario { Name = "embeddings-down", Target = FaultTarget.Embeddings, Mode = FaultMode.Down, ExpectStatus = "PartiallySucceeded", Description = "Embeddings are down: embedding is optional, so the run partially succeeds with findings (dedupe falls back to words)." },
            new ResilienceScenario { Name = "search-down", Target = FaultTarget.Search, Mode = FaultMode.Down, ExpectStatus = "Failed", ExpectFindings = false, Recovery = true, Description = "Search is down: the run fails cleanly; when search returns, the next run completes." },
            new ResilienceScenario { Name = "search-429-once", Target = FaultTarget.Search, Mode = FaultMode.RateLimitedOnce, Description = "Every search request is rate limited once (as SearXNG does when an engine throttles): the run retries and completes." },
            new ResilienceScenario { Name = "pages-down", Target = FaultTarget.Pages, Mode = FaultMode.Down, ExpectStatus = "PartiallySucceeded", ExpectFindings = false, Recovery = true, Description = "Every page fetch fails: the run partially succeeds with a warning and no findings; when the pages return, the next run completes with findings." },
            new ResilienceScenario { Name = "tts-down", Target = FaultTarget.Tts, Mode = FaultMode.Down, Audio = true, ExpectAudio = "Failed", Description = "Speech is down: audio fails cleanly with a message and the briefing stays ready." },
            new ResilienceScenario { Name = "tts-429-once", Target = FaultTarget.Tts, Mode = FaultMode.RateLimitedOnce, Audio = true, ExpectAudio = "Ready", Description = "Every speech request is rate limited once: synthesis retries and the audio is made." }
        };

        #endregion

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
            if (ctx.Paid.Enabled) { Console.Error.WriteLine("resilience always runs on stubs; remove --paid."); return ExitCodes.Usage; }
            DatasetLoader loader = new DatasetLoader(ctx.Args.GetString("dataset", "benchmarks/datasets/mymorningnews-synthetic.json"));
            string corpusId = ctx.Args.GetString("corpus", loader.Dataset.Corpora[0].Id);
            List<string> only = ctx.Args.GetList("scenarios", String.Empty);
            int stepTimeout = ctx.Args.GetInt("step-timeout-seconds", 120, 5, 3600);
            BenchmarkCorpus? corpus = loader.Dataset.Corpora.FirstOrDefault(c => c.Id == corpusId);
            if (corpus == null) { Console.Error.WriteLine("Unknown corpus " + corpusId + "."); return ExitCodes.Usage; }
            if (!await ctx.WaitForServerAsync(TimeSpan.FromSeconds(60), token).ConfigureAwait(false)) { Console.Error.WriteLine("The server at " + ctx.ServerUrl + " is not healthy."); return ExitCodes.Failure; }

            BenchmarkReport report = new BenchmarkReport { Kind = "resilience", Dataset = loader.Dataset.Name, Arms = new List<string> { "default" } };
            using (BenchStubServer stub = new BenchStubServer(ctx.StubPort, loader.Dataset))
            {
                stub.Brain.ExpectedGuardrail = await ctx.Admin.ScopeGuardrailAsync(token).ConfigureAwait(false);
                Provisioner provisioner = new Provisioner(ctx, stub, loader.Dataset.Name, loader.Sha256);
                ProductArm arm = ProductArm.Find("default")!;
                int baselineShown = 1;
                foreach (ResilienceScenario s in Scenarios.Where(s => only.Count == 0 || only.Contains(s.Name)))
                {
                    Console.WriteLine("[resilience] " + s.Name);
                    ProbeOutcome o = new ProbeOutcome { Arm = "default", Corpus = "resilience", ProbeId = s.Name, Type = s.Audio ? "audio" : s.Recovery ? "fail-and-recover" : "run", Category = s.Target.ToString().ToLowerInvariant(), Negative = s.ExpectStatus == "Failed" || s.ExpectAudio == "Failed" };
                    try
                    {
                        Workspace ws = await provisioner.CreateAsync(corpus, arm, "res-" + s.Name, token).ConfigureAwait(false);
                        MyMorningNewsClient user = await provisioner.SessionAsync(ws, token).ConfigureAwait(false);
                        if (s.Audio) await AudioAsync(s, o, provisioner, stub, ws, user, corpus, stepTimeout, token).ConfigureAwait(false);
                        else await RunAsync(s, o, provisioner, stub, ws, user, corpus, baselineShown, token).ConfigureAwait(false);
                        if (s.Name == "baseline" && o.Metrics.TryGetValue("shown", out double b)) baselineShown = Math.Max(1, (int)b);
                    }
                    catch (Exception ex) when (ex is TimeoutException || ex is System.Net.Http.HttpRequestException || ex is InvalidOperationException)
                    {
                        o.Score = 0;
                        o.Error = ex.GetType().Name + ": " + ex.Message;
                    }
                    finally
                    {
                        stub.Faults.Clear();
                    }

                    Console.WriteLine("  " + (o.Score >= 1 ? "pass" : "FAIL") + ": " + (o.Error ?? o.Detail));
                    report.Probes.Add(o);
                }

                report.Notes.Add("Faults injected: " + String.Join(", ", stub.Faults.InjectedCounts().Select(kv => kv.Key + " " + kv.Value)) + ". Slow mode adds " + stub.Faults.SlowLatencyMs + " ms.");
            }

            string path = await ctx.FinishReportAsync(report, loader.Sha256, token).ConfigureAwait(false);
            Console.WriteLine("Report: " + path);
            return ExitCodes.Success;
        }

        #endregion

        #region Private-Methods

        private static async Task RunAsync(ResilienceScenario s, ProbeOutcome o, Provisioner provisioner, BenchStubServer stub, Workspace ws, MyMorningNewsClient user, BenchmarkCorpus corpus, int baselineShown, CancellationToken token)
        {
            if (s.DryRunFirst)
            {
                await user.UpdateSearchProviderAsync(ws.ProviderId, stub.Index()!.SearchEndpoint(corpus.Id, 1), token).ConfigureAwait(false);
                RunDto queued = (await user.RunTopicAsync(ws.TopicId, true, token).ConfigureAwait(false)).Expect(202).Json<RunDto>();
                RunDto dry = await user.WaitForRunAsync(queued.Id, TimeSpan.FromMinutes(5), 500, token).ConfigureAwait(false);
                await provisioner.GuardRunAsync(ws, dry, token).ConfigureAwait(false);
                o.Metrics["dryRunShown"] = (await user.GetRunFindingsAsync(dry.Id, token).ConfigureAwait(false)).Count(f => f.Shown);
            }

            stub.Faults.Set(s.Target, s.Mode);
            Stopwatch sw = Stopwatch.StartNew();
            RunDto run = await provisioner.RunRoundAsync(ws, corpus, 1, token).ConfigureAwait(false);
            o.Metrics["runSeconds"] = Math.Round(sw.Elapsed.TotalSeconds, 2);
            stub.Faults.Clear();
            ApiResult read = await user.GetAsync("/v1.0/api/runs/" + run.Id, token).ConfigureAwait(false);
            List<FindingDto> findings = await user.GetRunFindingsAsync(run.Id, token).ConfigureAwait(false);
            List<LlmCallDto> calls = await user.GetRunCallsAsync(run.Id, token).ConfigureAwait(false);
            int shown = findings.Count(f => f.Shown);
            bool statusOk = s.ExpectStatus.Split(',').Contains(run.Status);
            bool findingsOk = !s.ExpectFindings || (s.DryRunFirst ? shown >= baselineShown : shown > 0);
            bool clean = read.Status == 200 && read.IsJson && (run.Status != "Failed" || !String.IsNullOrWhiteSpace(run.ErrorMessage));
            o.Metrics["clean"] = clean ? 1 : 0;
            o.Metrics["retriedCalls"] = calls.Count(c => c.Attempts > 1);
            o.Metrics["shown"] = shown;
            o.Score = statusOk && findingsOk && clean ? 1 : 0;
            o.Detail = run.Status + " (expected " + s.ExpectStatus + "), " + shown + " shown, " + calls.Count(c => c.Attempts > 1) + " model calls retried" + (run.ErrorMessage != null ? "; error: " + run.ErrorMessage : String.Empty);
            if (!s.Recovery) return;

            RunDto again = await provisioner.RunRoundAsync(ws, corpus, 1, token).ConfigureAwait(false);
            List<FindingDto> recovered = await user.GetRunFindingsAsync(again.Id, token).ConfigureAwait(false);
            int recoveredShown = recovered.Count(f => f.Shown);
            bool recoveryOk = again.Status == "Completed" && recoveredShown >= baselineShown;
            o.Metrics["recoveredShown"] = recoveredShown;
            if (!recoveryOk) o.Score = 0;
            o.Detail += "; after recovery: " + again.Status + ", " + recoveredShown + " shown (healthy baseline " + baselineShown + ")" + (again.Summary != null ? " (" + again.Summary.Split('\n')[0] + ")" : String.Empty);
        }

        private static async Task AudioAsync(ResilienceScenario s, ProbeOutcome o, Provisioner provisioner, BenchStubServer stub, Workspace ws, MyMorningNewsClient user, BenchmarkCorpus corpus, int stepTimeout, CancellationToken token)
        {
            RunDto run = await provisioner.RunRoundAsync(ws, corpus, 1, token).ConfigureAwait(false);
            BriefingDto? briefing = (await user.GetRunBriefingsAsync(run.Id, token).ConfigureAwait(false)).FirstOrDefault(b => b.Status == "Ready");
            if (briefing == null) { o.Score = 0; o.Error = "The healthy run produced no ready briefing (" + run.Status + ")."; return; }
            stub.Faults.Set(s.Target, s.Mode);
            Stopwatch sw = Stopwatch.StartNew();
            ApiResult queued = await user.RequestAudioAsync(briefing.Id, token).ConfigureAwait(false);
            MediaDto? media = null;
            DateTime deadline = DateTime.UtcNow.AddSeconds(stepTimeout);
            while (DateTime.UtcNow < deadline)
            {
                media = (await user.GetMediaAsync(briefing.Id, token).ConfigureAwait(false)).OrderByDescending(m => m.CreatedUtc).FirstOrDefault();
                if (media != null && (media.Status == "Ready" || media.Status == "Failed")) break;
                await Task.Delay(500, token).ConfigureAwait(false);
            }

            o.Metrics["audioSeconds"] = Math.Round(sw.Elapsed.TotalSeconds, 2);
            stub.Faults.Clear();
            BriefingDto? after = (await user.GetRunBriefingsAsync(run.Id, token).ConfigureAwait(false)).FirstOrDefault(b => b.Id == briefing.Id);
            bool clean = media != null && (media.Status != "Failed" || !String.IsNullOrWhiteSpace(media.ErrorMessage));
            o.Metrics["clean"] = clean ? 1 : 0;
            o.Score = media?.Status == s.ExpectAudio && clean && after?.Status == "Ready" ? 1 : 0;
            o.Detail = "Audio request " + queued.Status + "; episode " + (media?.Status ?? "missing") + " (expected " + s.ExpectAudio + ")" + (media?.ErrorMessage != null ? ", error: " + media.ErrorMessage : String.Empty) + "; briefing " + (after?.Status ?? "missing");
        }

        #endregion
    }
}
