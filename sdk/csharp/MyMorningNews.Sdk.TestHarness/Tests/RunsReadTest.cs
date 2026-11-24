namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Resources;

    /// <summary>
    /// Runs, read only: list, read, stages, costs, queries, results, email preview, and WaitAsync on a finished run.
    /// </summary>
    public class RunsReadTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Runs list, get, stages, costs, queries, results, email preview, wait, events, retry";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RunsReadTest()
        {
        }

        /// <summary>
        /// Run the case.
        /// </summary>
        /// <param name="context">Shared state.</param>
        /// <param name="result">Result.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public override async Task RunAsync(TestContext context, TestResult result, CancellationToken token)
        {
            MyMorningNewsClient c = context.RequireAdmin();
            await StepAsync(context, result, "GET unknown run (404)", () => Helpers.ExpectErrorAsync(() => c.Runs.GetAsync("run_missing", token), 404)).ConfigureAwait(false);
            await StepAsync(context, result, "Events for an unknown run (404)", () => Helpers.ExpectErrorAsync(() => DrainAsync(c, "run_missing", token), 404)).ConfigureAwait(false);
            await StepAsync(context, result, "Costs of an unknown run (404)", () => Helpers.ExpectErrorAsync(() => c.Runs.GetCostsAsync("run_missing", token), 404)).ConfigureAwait(false);
            await StepAsync(context, result, "GET runs sorted by cost", () => c.Runs.ListAsync(new RunListOptions { PageSize = 5, SortBy = "estimatedCostUsd" }, token)).ConfigureAwait(false);
            await StepAsync(context, result, "Retry an unknown run (404)", () => Helpers.ExpectErrorAsync(() => c.Runs.RetryAsync("run_missing", "Predict", token), 404)).ConfigureAwait(false);
            EnumerationResult<Run> runs = await StepAsync(context, result, "GET runs", () => c.Runs.ListAsync(new RunListOptions { PageSize = 25 }, token)).ConfigureAwait(false);
            Run? finished = null;
            foreach (Run candidate in runs.Objects.Where(r => RunsResource.IsTerminal(r.Status)))
            {
                try
                {
                    await c.Topics.GetAsync(candidate.TopicId, token).ConfigureAwait(false);
                    finished = candidate;
                    break;
                }
                catch (MyMorningNewsException ex) when (ex.StatusCode == 404)
                {
                }
            }

            if (finished == null) return;

            Run run = await StepAsync(context, result, "GET run", () => c.Runs.GetAsync(finished.Id, token)).ConfigureAwait(false);
            Helpers.Equal(finished.Id, run.Id, "run id");
            List<RunStage> stages = (await StepAsync(context, result, "GET run stages", () => c.Runs.ListStagesAsync(run.Id, options: new MyMorningNews.Sdk.Options.EnumerationQuery { PageSize = 1000 }, token: token)).ConfigureAwait(false)).Objects;
            Helpers.True(stages.All(s => s.RunId == run.Id), "stages belong to the run");
            RunCostReport costs = await StepAsync(context, result, "GET run costs", () => c.Runs.GetCostsAsync(run.Id, token)).ConfigureAwait(false);
            Helpers.Equal(run.Id, costs.RunId, "cost report run id");
            Helpers.Equal("USD", costs.Currency, "cost report currency");
            Helpers.Equal(stages.Count, costs.Stages.Count, "a cost row per stage");
            Helpers.True(costs.Stages.Sum(s => s.LlmCallCount) + (costs.Unattributed?.LlmCallCount ?? 0) == costs.Totals.LlmCallCount, "stage and unattributed calls add up to the total");
            RunStage? costed = stages.FirstOrDefault(s => s.LlmCallCount > 0);
            if (costed != null)
            {
                EnumerationResult<LlmCall> stageCalls = await StepAsync(context, result, "GET llm-calls by stage", () => c.LlmCalls.ListAsync(new LlmCallListOptions { RunStageId = costed.Id, PageSize = 100 }, token)).ConfigureAwait(false);
                Helpers.True(stageCalls.Objects.All(x => x.RunStageId == costed.Id), "calls belong to the stage");
            }

            await StepAsync(context, result, "GET run queries", () => c.Runs.ListQueriesAsync(run.Id, options: new MyMorningNews.Sdk.Options.EnumerationQuery { PageSize = 1000 }, token: token)).ConfigureAwait(false);
            await StepAsync(context, result, "GET run results", () => c.Runs.ListResultsAsync(run.Id, null, options: new MyMorningNews.Sdk.Options.EnumerationQuery { PageSize = 1000 }, token: token)).ConfigureAwait(false);
            EmailPreview preview = await StepAsync(context, result, "GET run email-preview", () => c.Runs.GetEmailPreviewAsync(run.Id, token)).ConfigureAwait(false);
            Helpers.NotEmpty(preview.Subject, "email preview subject");
            Run waited = await StepAsync(context, result, "WaitAsync on a finished run", () => c.Runs.WaitAsync(run.Id, TimeSpan.FromSeconds(10), null, token)).ConfigureAwait(false);
            Helpers.Equal(run.Status, waited.Status, "wait returns the terminal status");
            List<RunEvent> events = await StepAsync(context, result, "Stream events of a finished run", () => DrainAsync(c, run.Id, token)).ConfigureAwait(false);
            Helpers.True(events.Count(e => e.Event == "stage") == stages.Count && events.Last().Event == "done" && events.Last().AsRun()?.Id == run.Id, "stages replayed, then done with the run");
            await StepAsync(context, result, "Retry from an unknown stage (400)", () => Helpers.ExpectErrorAsync(() => c.Runs.RetryAsync(run.Id, "Bogus", token), 400)).ConfigureAwait(false);
        }

        private static async Task<List<RunEvent>> DrainAsync(MyMorningNewsClient c, string runId, CancellationToken token)
        {
            List<RunEvent> events = new List<RunEvent>();
            await foreach (RunEvent e in c.Runs.StreamEventsAsync(runId, token).ConfigureAwait(false)) events.Add(e);
            return events;
        }
    }
}
