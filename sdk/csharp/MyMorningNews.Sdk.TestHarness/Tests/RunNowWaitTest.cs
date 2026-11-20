namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Requests;
    using MyMorningNews.Sdk.Resources;

    /// <summary>
    /// With --with-run only: queue a dry run for a new topic, wait for it, read its stages, and delete the topic. This calls the
    /// configured model and search providers.
    /// </summary>
    public class RunNowWaitTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Run now (dry run) and wait";

        /// <summary>
        /// Calls real models.
        /// </summary>
        public override bool RequiresRun => true;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RunNowWaitTest()
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
            Topic topic = await StepAsync(context, result, "POST topic", () => Fixtures.CreateTopicAsync(context, c, "run", token)).ConfigureAwait(false);
            await StepAsync(context, result, "PUT topic seed queries", () => c.Topics.UpdateAsync(topic.Id, new TopicUpsertRequest { SeedQueries = new List<string> { "PostgreSQL release" } }, token)).ConfigureAwait(false);
            Run queued = await StepAsync(context, result, "POST topic run (dry run)", () => c.Topics.RunAsync(topic.Id, true, token)).ConfigureAwait(false);
            Helpers.True(queued.IsDryRun, "dry run");
            Run done = await StepAsync(context, result, "WaitAsync", () => c.Runs.WaitAsync(queued.Id, TimeSpan.FromMinutes(20), TimeSpan.FromSeconds(3), token)).ConfigureAwait(false);
            Helpers.True(RunsResource.IsTerminal(done.Status), "terminal status");
            List<RunStage> stages = await StepAsync(context, result, "GET run stages", () => c.Runs.ListStagesAsync(done.Id, token)).ConfigureAwait(false);
            Helpers.True(stages.Count > 0, "stages recorded");
            await StepAsync(context, result, "DELETE topic", () => Fixtures.DeleteTopicAsync(context, c, topic.Id, token)).ConfigureAwait(false);
        }
    }
}
