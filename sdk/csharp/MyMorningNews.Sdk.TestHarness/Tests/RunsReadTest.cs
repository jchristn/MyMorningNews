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
    /// Runs, read only: list, read, stages, queries, results, email preview, and WaitAsync on a finished run.
    /// </summary>
    public class RunsReadTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Runs list, get, stages, queries, results, email preview, wait";

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
            EnumerationResult<Run> runs = await StepAsync(context, result, "GET runs", () => c.Runs.ListAsync(new RunListOptions { PageSize = 25 }, token)).ConfigureAwait(false);
            Run? finished = null;
            foreach (Run candidate in runs.Items.Where(r => RunsResource.IsTerminal(r.Status)))
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
            List<RunStage> stages = await StepAsync(context, result, "GET run stages", () => c.Runs.ListStagesAsync(run.Id, token)).ConfigureAwait(false);
            Helpers.True(stages.All(s => s.RunId == run.Id), "stages belong to the run");
            await StepAsync(context, result, "GET run queries", () => c.Runs.ListQueriesAsync(run.Id, token)).ConfigureAwait(false);
            await StepAsync(context, result, "GET run results", () => c.Runs.ListResultsAsync(run.Id, null, token)).ConfigureAwait(false);
            EmailPreview preview = await StepAsync(context, result, "GET run email-preview", () => c.Runs.GetEmailPreviewAsync(run.Id, token)).ConfigureAwait(false);
            Helpers.NotEmpty(preview.Subject, "email preview subject");
            Run waited = await StepAsync(context, result, "WaitAsync on a finished run", () => c.Runs.WaitAsync(run.Id, TimeSpan.FromSeconds(10), null, token)).ConfigureAwait(false);
            Helpers.Equal(run.Status, waited.Status, "wait returns the terminal status");
        }
    }
}
