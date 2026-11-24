namespace MyMorningNews.Benchmark.Runners
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Benchmark.Client;
    using MyMorningNews.Benchmark.Datasets;
    using MyMorningNews.Benchmark.Metrics;
    using MyMorningNews.Benchmark.Reporting;
    using MyMorningNews.Benchmark.Stub;

    /// <summary>
    /// The e2e command: the whole user-facing flow, repeated (--repeat, default 3) in fresh workspaces. A topic is run;
    /// the run's briefing must be ready; the run email must be sent with an open link, feedback links, and an unsubscribe
    /// link; audio must be made for the briefing; and a feedback link from the email must record feedback when followed.
    /// Each check is a probe (pass or fail, deterministic under stubs); the timings of each step are metrics, and the
    /// server's stage histograms are diffed for attribution.
    /// </summary>
    public static class E2eRunner
    {
        #region Private-Members

        private static readonly Regex _FeedbackLink = new Regex(@"/v1\.0/public/feedback/([A-Za-z0-9_\-]+)\?a=([A-Za-z]+)", RegexOptions.Compiled);

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
            DatasetLoader loader = new DatasetLoader(ctx.Args.GetString("dataset", "benchmarks/datasets/mymorningnews-synthetic.json"));
            string corpusId = ctx.Args.GetString("corpus", loader.Dataset.Corpora[0].Id);
            int repeat = ctx.Args.GetInt("repeat", 3, 1, 50);
            int stepTimeout = ctx.Args.GetInt("step-timeout-seconds", 120, 5, 3600);
            bool audio = !ctx.Args.GetFlag("no-audio");
            BenchmarkCorpus? corpus = loader.Dataset.Corpora.FirstOrDefault(c => c.Id == corpusId);
            if (corpus == null) { Console.Error.WriteLine("Unknown corpus " + corpusId + "."); return ExitCodes.Usage; }
            if (!await ctx.WaitForServerAsync(TimeSpan.FromSeconds(60), token).ConfigureAwait(false)) { Console.Error.WriteLine("The server at " + ctx.ServerUrl + " is not healthy."); return ExitCodes.Failure; }

            BenchmarkReport report = new BenchmarkReport { Kind = "e2e", Dataset = loader.Dataset.Name, Arms = new List<string> { "default" } };
            List<double> totals = new List<double>();
            List<double> toEmail = new List<double>();
            List<double> toAudio = new List<double>();
            using (BenchStubServer stub = new BenchStubServer(ctx.StubPort, loader.Dataset))
            {
                stub.Brain.ExpectedGuardrail = await ctx.Admin.ScopeGuardrailAsync(token).ConfigureAwait(false);
                Provisioner provisioner = new Provisioner(ctx, stub, loader.Dataset.Name, loader.Sha256);
                PrometheusSnapshot before = await PrometheusSnapshot.ScrapeAsync(ctx.Http, ctx.MetricsUrl, token).ConfigureAwait(false);
                ProductArm arm = ProductArm.Find("default")!;
                for (int i = 1; i <= repeat; i++)
                {
                    Console.WriteLine("[e2e] repeat " + i + " of " + repeat);
                    string group = "repeat-" + i.ToString(CultureInfo.InvariantCulture);
                    Workspace ws = await provisioner.CreateAsync(corpus, arm, "e2e" + i, token).ConfigureAwait(false);
                    MyMorningNewsClient user = await provisioner.SessionAsync(ws, token).ConfigureAwait(false);
                    Stopwatch sw = Stopwatch.StartNew();
                    RunDto run = await provisioner.RunRoundAsync(ws, corpus, 1, token).ConfigureAwait(false);
                    double runSeconds = sw.Elapsed.TotalSeconds;
                    ProbeOutcome runProbe = Probe(group, "run", "research");
                    runProbe.Score = run.Status == "Completed" ? 1 : 0;
                    runProbe.Metrics["runSeconds"] = Math.Round(runSeconds, 2);
                    runProbe.Metrics["serverMs"] = Math.Round(run.DurationMs, 1);
                    runProbe.Detail = run.Status + ": " + run.NewFindingCount + " findings, " + run.SourceCount + " sources" + (run.ErrorMessage != null ? "; " + run.ErrorMessage : String.Empty);
                    foreach (RunStageDto s in await user.GetStagesAsync(run.Id, token).ConfigureAwait(false)) runProbe.Metrics["stage." + s.Stage + "Ms"] = Math.Round(s.DurationMs, 1);
                    report.Probes.Add(runProbe);

                    BriefingDto? briefing = await PollAsync(async () => (await user.GetRunBriefingsAsync(run.Id, token).ConfigureAwait(false)).FirstOrDefault(), b => b.Status == "Ready" || b.Status == "Failed", stepTimeout, token).ConfigureAwait(false);
                    if (briefing != null) briefing = await user.GetBriefingAsync(briefing.Id, token).ConfigureAwait(false);
                    ProbeOutcome briefingProbe = Probe(group, "briefing", "briefing");
                    briefingProbe.Score = briefing?.Status == "Ready" && briefing.ItemCount > 0 && !String.IsNullOrWhiteSpace(briefing.Markdown) ? 1 : 0;
                    briefingProbe.Metrics["readySeconds"] = Math.Round(sw.Elapsed.TotalSeconds, 2);
                    briefingProbe.Detail = briefing == null ? "No briefing for the run" : briefing.Status + ", " + briefing.ItemCount + " items, " + briefing.ModelFallbacks + " model fallbacks" + (briefing.ErrorMessage != null ? "; " + briefing.ErrorMessage : String.Empty);
                    report.Probes.Add(briefingProbe);

                    EmailMessageDto? email = await PollAsync(async () => (await user.GetRunEmailsAsync(run.Id, token).ConfigureAwait(false)).FirstOrDefault(e => e.Template == "RunDigest"), e => e.Status == "Sent" || e.Status == "Failed" || e.Status == "Suppressed" || e.Status == "Skipped", stepTimeout, token).ConfigureAwait(false);
                    if (email != null) email = await user.GetEmailAsync(email.Id, token).ConfigureAwait(false);
                    ProbeOutcome emailProbe = Probe(group, "email", "email");
                    string html = email?.RenderedHtml ?? String.Empty;
                    bool open = html.Contains("/dashboard/", StringComparison.Ordinal);
                    bool feedback = _FeedbackLink.IsMatch(html);
                    bool unsubscribe = html.Contains("/v1.0/public/unsubscribe/", StringComparison.Ordinal);
                    emailProbe.Score = email?.Status == "Sent" && open && feedback && unsubscribe ? 1 : 0;
                    emailProbe.Metrics["sentSeconds"] = Math.Round(sw.Elapsed.TotalSeconds, 2);
                    emailProbe.Detail = email == null ? "No run email" : email.Status + "; open link " + Yes(open) + ", feedback links " + Yes(feedback) + ", unsubscribe " + Yes(unsubscribe) + (email.LastError != null ? "; " + email.LastError : String.Empty);
                    if (email?.Status == "Sent") toEmail.Add(sw.Elapsed.TotalMilliseconds);
                    report.Probes.Add(emailProbe);

                    if (audio && briefing?.Status == "Ready")
                    {
                        ApiResult queued = await user.RequestAudioAsync(briefing.Id, token).ConfigureAwait(false);
                        MediaDto? media = queued.Success || queued.Status == 409
                            ? await PollAsync(async () => (await user.GetMediaAsync(briefing.Id, token).ConfigureAwait(false)).OrderByDescending(m => m.CreatedUtc).FirstOrDefault(), m => m.Status == "Ready" || m.Status == "Failed", stepTimeout, token).ConfigureAwait(false)
                            : null;
                        ProbeOutcome audioProbe = Probe(group, "audio", "audio");
                        audioProbe.Score = media?.Status == "Ready" && media.Bytes > 0 ? 1 : 0;
                        audioProbe.Metrics["readySeconds"] = Math.Round(sw.Elapsed.TotalSeconds, 2);
                        audioProbe.Detail = media == null ? "Audio request returned " + queued.Status + ": " + Short(queued.Body) : media.Status + ", " + media.Bytes + " bytes, " + media.SegmentCount + " segments, " + media.ContentType + (media.ErrorMessage != null ? "; " + media.ErrorMessage : String.Empty);
                        if (media?.Status == "Ready") toAudio.Add(sw.Elapsed.TotalMilliseconds);
                        report.Probes.Add(audioProbe);
                    }

                    ProbeOutcome linkProbe = Probe(group, "feedback-link", "feedback");
                    Match link = _FeedbackLink.Match(html);
                    if (!link.Success) { linkProbe.Score = 0; linkProbe.Detail = "No feedback link in the email."; }
                    else
                    {
                        string path = "/v1.0/public/feedback/" + link.Groups[1].Value;
                        ApiResult page = await ctx.Admin.Anonymous().SendAsync(HttpMethod.Get, path + "?a=" + link.Groups[2].Value, null, token).ConfigureAwait(false);
                        ApiResult submit = await ctx.Admin.PostFormAsync(path, new Dictionary<string, string> { { "a", link.Groups[2].Value } }, token).ConfigureAwait(false);
                        ApiResult recorded = await user.GetAsync("/v1.0/api/feedback?topicId=" + ws.TopicId + "&channel=Email&pageSize=50", token).ConfigureAwait(false);
                        long count = recorded.Success ? recorded.Json<PageDto<IdDto>>().TotalRecords : 0;
                        linkProbe.Score = page.Status == 200 && submit.Status == 200 && count > 0 ? 1 : 0;
                        linkProbe.Detail = "page " + page.Status + ", submit " + submit.Status + " (" + link.Groups[2].Value + "), " + count + " email feedback record(s)";
                    }

                    report.Probes.Add(linkProbe);
                    totals.Add(sw.Elapsed.TotalMilliseconds);
                }

                await Task.Delay(2500, token).ConfigureAwait(false);
                PrometheusSnapshot after = await PrometheusSnapshot.ScrapeAsync(ctx.Http, ctx.MetricsUrl, token).ConfigureAwait(false);
                report.Stages = StageMetrics.Diff(before, after, "run_stage_duration", "stage");
                report.Stages.AddRange(StageMetrics.Diff(before, after, "llm_call_duration", "task").Select(s => { s.Name = "model:" + s.Name; return s; }));
                if (!after.Available) report.Notes.Add("The metrics endpoint " + ctx.MetricsUrl + " could not be scraped; no stage breakdown.");
            }

            report.Probes.Add(new ProbeOutcome { Arm = "default", Corpus = "e2e", ProbeId = "timing-flow", Type = "timing", Category = "latency", Applicable = false, Latency = LatencyStats.From(totals), Detail = "Submit to the last check, per repeat." });
            report.Probes.Add(new ProbeOutcome { Arm = "default", Corpus = "e2e", ProbeId = "timing-email", Type = "timing", Category = "latency", Applicable = false, Latency = LatencyStats.From(toEmail), Detail = "Submit to email sent." });
            if (audio) report.Probes.Add(new ProbeOutcome { Arm = "default", Corpus = "e2e", ProbeId = "timing-audio", Type = "timing", Category = "latency", Applicable = false, Latency = LatencyStats.From(toAudio), Detail = "Submit to audio ready." });
            report.Notes.Add("Timings include the server's worker poll intervals (run claim every " + "Scheduler.RunPollSeconds" + ", email delivery every Email.DeliveryIntervalSeconds), so they are an upper bound on pipeline latency. Each check is deterministic under stubs; timings are not, and are excluded from the CI gate.");
            string pathOut = await ctx.FinishReportAsync(report, loader.Sha256, token).ConfigureAwait(false);
            foreach (MetricSummary s in report.Summaries.Where(s => s.Group == "overall" || s.Group.StartsWith("type:", StringComparison.Ordinal))) Console.WriteLine("  " + (s.Group + " " + s.Metric).PadRight(30) + " " + s.Value);
            Console.WriteLine("Report: " + pathOut);
            return ExitCodes.Success;
        }

        #endregion

        #region Private-Methods

        private static ProbeOutcome Probe(string group, string id, string category)
        {
            return new ProbeOutcome { Arm = "default", Corpus = "e2e", ProbeId = group + "-" + id, Type = id, Category = category };
        }

        private static async Task<T?> PollAsync<T>(Func<Task<T?>> read, Func<T, bool> done, int timeoutSeconds, CancellationToken token) where T : class
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            T? last = null;
            while (DateTime.UtcNow < deadline)
            {
                last = await read().ConfigureAwait(false);
                if (last != null && done(last)) return last;
                await Task.Delay(500, token).ConfigureAwait(false);
            }

            return last;
        }

        private static string Yes(bool b)
        {
            return b ? "yes" : "no";
        }

        private static string Short(string s)
        {
            return s.Length > 160 ? s.Substring(0, 160) : s;
        }

        #endregion
    }
}
