namespace MyMorningNews.Core.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Pipeline.Stages;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Core.Telemetry;

    /// <summary>
    /// The research pipeline (plan section 16): runs the stages in order, records a runstages row and a span per stage,
    /// fails the run on a critical stage failure, marks it partially succeeded when a non-critical stage fails, honors
    /// cancellation between stages, and totals the run's tokens and cost from the model call log.
    /// Thread safety: safe for concurrent runs; each run has its own context.
    /// </summary>
    public class ResearchPipeline : IRunExecutor
    {
        #region Public-Members

        /// <summary>
        /// Stages in execution order.
        /// </summary>
        public IReadOnlyList<IRunStage> Stages { get; }

        /// <summary>
        /// The first stage a finished run can be retried from in place. Earlier stages depend on search results and
        /// fetched pages that are not kept in a resumable form, so retrying from them starts a new run instead.
        /// </summary>
        public const string FirstResumableStage = "Synthesize";

        /// <summary>
        /// The default stage names, in order.
        /// </summary>
        public static readonly IReadOnlyList<string> DefaultStageNames = new List<string>
        {
            "AssembleContext", "PlanQueries", "Search", "Filter", "Fetch", "Extract", "Embed", "Dedupe", "Score",
            "Synthesize", "ReviewPredictions", "Predict", "Suggest", "ComposeBriefing", "Notify"
        };

        /// <summary>
        /// Names of the stages a run can be retried from in place, in order.
        /// </summary>
        public IReadOnlyList<string> ResumableStages
        {
            get
            {
                int first = Stages.ToList().FindIndex(s => s.Name == FirstResumableStage);
                return first < 0 ? new List<string>() : Stages.Skip(first).Select(s => s.Name).ToList();
            }
        }

        #endregion

        #region Private-Members

        private readonly PipelineServices _Services;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate with the default stages.
        /// </summary>
        /// <param name="services">Pipeline services.</param>
        /// <param name="stages">Stages to run instead of the defaults (tests). Null uses the defaults.</param>
        /// <exception cref="ArgumentNullException">services is null.</exception>
        public ResearchPipeline(PipelineServices services, IReadOnlyList<IRunStage>? stages = null)
        {
            _Services = services ?? throw new ArgumentNullException(nameof(services));
            Stages = stages ?? new List<IRunStage>
            {
                new AssembleContextStage(),
                new PlanQueriesStage(),
                new SearchStage(),
                new FilterStage(),
                new FetchStage(),
                new ExtractStage(),
                new EmbedStage(),
                new DedupeStage(),
                new ScoreStage(),
                new SynthesizeStage(),
                new ReviewPredictionsStage(),
                new PredictStage(),
                new SuggestStage(),
                new ComposeBriefingStage(),
                new NotifyStage()
            };
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<RunResult> ExecuteAsync(Run run, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(run);
            Stopwatch total = Stopwatch.StartNew();
            Topic topic = await _Services.Database.Topics.ReadAsync(run.TenantId, run.TopicId, token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The run's topic no longer exists.");
            RunContext ctx = new RunContext { Run = run, Topic = topic, NowUtc = DateTime.UtcNow };
            bool partial = false;
            List<string> failed = new List<string>();
            int start = 0;
            if (!String.IsNullOrEmpty(run.ResumeFromStage)) start = await PrepareResumeAsync(ctx, token).ConfigureAwait(false);

            try
            {
                for (int i = start; i < Stages.Count; i++)
                {
                    IRunStage stage = Stages[i];
                    Run? latest = await _Services.Database.Runs.ReadAsync(run.TenantId, run.Id, token).ConfigureAwait(false);
                    if (latest != null && latest.CancelRequested) throw new OperationCanceledException("Cancelled.");
                    RunStage row = await _Services.Database.RunStages.CreateAsync(new RunStage
                    {
                        TenantId = run.TenantId,
                        RunId = run.Id,
                        Stage = stage.Name,
                        Status = StageStatusEnum.Running,
                        StartedUtc = DateTime.UtcNow,
                        SortOrder = i
                    }, token).ConfigureAwait(false);

                    Stopwatch sw = Stopwatch.StartNew();
                    using (Activity? span = MyMorningNewsTelemetry.StartSpan("stage:" + stage.Name))
                    {
                        span?.SetTag("run.id", run.Id);
                        try
                        {
                            StageOutcome outcome;
                            using (RunStageScope.Begin(row.Id))
                            {
                                outcome = await stage.ExecuteAsync(ctx, _Services, token).ConfigureAwait(false);
                            }

                            row.Status = StageStatusEnum.Succeeded;
                            row.ItemsIn = outcome.ItemsIn;
                            row.ItemsOut = outcome.ItemsOut;
                            row.ErrorMessage = outcome.Note;
                        }
                        catch (Exception ex) when (!(ex is OperationCanceledException))
                        {
                            row.Status = StageStatusEnum.Failed;
                            row.ErrorMessage = ex.Message;
                            span?.SetStatus(ActivityStatusCode.Error, ex.Message);
                            if (stage.Critical)
                            {
                                await FinishStageAsync(row, sw, CancellationToken.None).ConfigureAwait(false);
                                throw new InvalidOperationException(stage.Name + " failed: " + ex.Message, ex);
                            }

                            partial = true;
                            failed.Add(stage.Name);
                            _Services.Logging.Warn("[ResearchPipeline] " + stage.Name + " failed for run " + run.Id + ": " + ex.Message);
                        }
                        finally
                        {
                            if (row.Status == StageStatusEnum.Running) row.Status = StageStatusEnum.Failed;
                            await FinishStageAsync(row, sw, CancellationToken.None).ConfigureAwait(false);
                            await PublishTotalsAsync(run, CancellationToken.None).ConfigureAwait(false);
                            MyMorningNewsTelemetry.Record(MyMorningNewsTelemetry.RunStageDuration, sw.Elapsed.TotalSeconds, "stage", stage.Name);
                        }
                    }
                }
            }
            finally
            {
                run.DurationMs = Math.Round(total.Elapsed.TotalMilliseconds, 1);
                await TotalsAsync(run, CancellationToken.None).ConfigureAwait(false);
            }

            string summary = ctx.Summary ?? (ctx.Findings.Count + " new findings.");
            if (failed.Count > 0) summary += "\n\nSome optional steps did not finish: " + String.Join(", ", failed) + ".";
            if (ctx.Notes.Count > 0) summary += "\n\n" + String.Join(" ", ctx.Notes);
            return new RunResult { Summary = summary, PartiallySucceeded = partial || ctx.PartiallySucceeded, BriefingId = ctx.BriefingId, Delivered = ctx.Delivered };
        }

        #endregion

        #region Private-Methods

        private async Task<int> PrepareResumeAsync(RunContext ctx, CancellationToken token)
        {
            Run run = ctx.Run;
            string from = run.ResumeFromStage!;
            run.ResumeFromStage = null;
            await _Services.Database.Runs.UpdateAsync(run, token).ConfigureAwait(false);
            int start = Stages.ToList().FindIndex(s => s.Name == from);
            if (start < 0 || !ResumableStages.Contains(from)) throw new InvalidOperationException("A run cannot be retried from " + from + ".");

            IRunStage? assemble = Stages.FirstOrDefault(s => s.Name == "AssembleContext");
            if (assemble != null) await assemble.ExecuteAsync(ctx, _Services, token).ConfigureAwait(false);
            ctx.Findings = (await _Services.Database.Findings.EnumerateAsync(new FindingFilter { TenantId = run.TenantId, RunId = run.Id, IsDuplicate = false, PageSize = 1000 }, token).ConfigureAwait(false)).Objects
                .OrderBy(f => f.Rank <= 0 ? int.MaxValue : f.Rank).ToList();
            int synthesize = Stages.ToList().FindIndex(s => s.Name == FirstResumableStage);
            if (start > synthesize && !String.IsNullOrWhiteSpace(run.Summary))
            {
                string summary = run.Summary!;
                int cut = summary.IndexOf("\n\nSome optional steps did not finish", StringComparison.Ordinal);
                ctx.Summary = cut >= 0 ? summary.Substring(0, cut) : summary;
            }

            foreach (RunStage old in (await _Services.Database.RunStages.EnumerateAsync(new RunStageFilter { TenantId = run.TenantId, RunId = run.Id, PageSize = 100 }, token).ConfigureAwait(false)).Objects.Where(s => s.SortOrder >= start))
                await _Services.Database.RunStages.DeleteAsync(run.TenantId, old.Id, token).ConfigureAwait(false);
            _Services.Logging.Info("[ResearchPipeline] retrying run " + run.Id + " from " + from);
            return start;
        }

        private async Task FinishStageAsync(RunStage row, Stopwatch sw, CancellationToken token)
        {
            if (row.CompletedUtc.HasValue) return;
            row.CompletedUtc = DateTime.UtcNow;
            row.DurationMs = Math.Round(sw.Elapsed.TotalMilliseconds, 1);
            try
            {
                List<LlmCall> calls = await CallsAsync(row.TenantId, row.RunId, row.Id, token).ConfigureAwait(false);
                row.LlmCallCount = calls.Count;
                row.UnpricedCallCount = calls.Count(c => !c.EstimatedCostUsd.HasValue);
                row.InputTokens = calls.Sum(c => c.InputTokens);
                row.OutputTokens = calls.Sum(c => c.OutputTokens);
                row.CachedInputTokens = calls.Sum(c => c.CachedInputTokens);
                row.ReasoningTokens = calls.Sum(c => c.ReasoningTokens);
                row.EstimatedCostUsd = calls.Any(c => c.EstimatedCostUsd.HasValue) ? calls.Sum(c => c.EstimatedCostUsd ?? 0) : null;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _Services.Logging.Warn("[ResearchPipeline] unable to total the cost of stage " + row.Stage + " in run " + row.RunId + ": " + ex.Message);
            }

            await _Services.Database.RunStages.UpdateAsync(row, token).ConfigureAwait(false);
        }

        private async Task TotalsAsync(Run run, CancellationToken token)
        {
            List<LlmCall> calls = await CallsAsync(run.TenantId, run.Id, null, token).ConfigureAwait(false);
            run.LlmCallCount = calls.Count;
            run.UnpricedCallCount = calls.Count(c => !c.EstimatedCostUsd.HasValue);
            run.InputTokens = calls.Sum(c => c.InputTokens);
            run.OutputTokens = calls.Sum(c => c.OutputTokens);
            run.CachedInputTokens = calls.Sum(c => c.CachedInputTokens);
            run.ReasoningTokens = calls.Sum(c => c.ReasoningTokens);
            run.EstimatedCostUsd = calls.Any(c => c.EstimatedCostUsd.HasValue) ? calls.Sum(c => c.EstimatedCostUsd ?? 0) : null;
        }

        private async Task PublishTotalsAsync(Run run, CancellationToken token)
        {
            // Keep the run's cost current while it runs. Copy the totals onto a fresh read so a cancel request is not lost.
            try
            {
                await TotalsAsync(run, token).ConfigureAwait(false);
                Run? latest = await _Services.Database.Runs.ReadAsync(run.TenantId, run.Id, token).ConfigureAwait(false);
                if (latest == null) return;
                latest.LlmCallCount = run.LlmCallCount;
                latest.UnpricedCallCount = run.UnpricedCallCount;
                latest.InputTokens = run.InputTokens;
                latest.OutputTokens = run.OutputTokens;
                latest.CachedInputTokens = run.CachedInputTokens;
                latest.ReasoningTokens = run.ReasoningTokens;
                latest.EstimatedCostUsd = run.EstimatedCostUsd;
                await _Services.Database.Runs.UpdateAsync(latest, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _Services.Logging.Warn("[ResearchPipeline] unable to update the cost of run " + run.Id + ": " + ex.Message);
            }
        }

        private async Task<List<LlmCall>> CallsAsync(string tenantId, string runId, string? runStageId, CancellationToken token)
        {
            List<LlmCall> all = new List<LlmCall>();
            for (int page = 1; page <= 100; page++)
            {
                EnumerationResult<LlmCall> batch = await _Services.Database.LlmCalls.EnumerateAsync(new LlmCallFilter
                {
                    TenantId = tenantId, RunId = runId, RunStageId = runStageId, PageSize = 1000, PageNumber = page
                }, token).ConfigureAwait(false);
                all.AddRange(batch.Objects);
                if (batch.Objects.Count < 1000) break;
            }

            return all;
        }

        #endregion
    }
}
