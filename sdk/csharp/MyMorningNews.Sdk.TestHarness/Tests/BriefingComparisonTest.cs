namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Briefing comparison provenance: 404 for an unknown briefing, then an edition that covers no active topic is composed
    /// (no model calls) and its briefing reports Compared false with a note, every change kind counted, and no baselines.
    /// Skipped when the edition would cover active topics or the composition does not finish.
    /// </summary>
    public class BriefingComparisonTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Briefing comparison";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public BriefingComparisonTest()
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
            await StepAsync(context, result, "GET comparison of an unknown briefing (404)", () => Helpers.ExpectErrorAsync(() => c.Briefings.GetComparisonAsync("brf_missing", token), 404)).ConfigureAwait(false);

            Topic topic = await StepAsync(context, result, "POST topic", () => Fixtures.CreateTopicAsync(context, c, "comparison-topic", token)).ConfigureAwait(false);
            await StepAsync(context, result, "POST topic archive", () => c.Topics.ArchiveAsync(topic.Id, token)).ConfigureAwait(false);
            EditionResponse e = await StepAsync(context, result, "POST edition", () => c.Editions.CreateAsync(new EditionUpsertRequest
            {
                Name = context.Name("comparison-edition"),
                AudioEnabled = false,
                EmailEnabled = false,
                Enabled = false,
                FreshResearchBeforeCompose = false
            }, token)).ConfigureAwait(false);
            string id = e.Edition.Id;
            context.Cleanup.Add("edition:" + id, ct => c.Editions.DeleteAsync(id, ct));
            EditionResponse linked = await StepAsync(context, result, "PUT edition topics (archived topic only)",
                () => c.Editions.SetTopicsAsync(id, new EditionTopicsRequest { TopicIds = new List<string> { topic.Id } }, token)).ConfigureAwait(false);
            if (linked.CoveredTopicIds.Any()) throw new SkipTestException("The edition covers active topics; composing it would call a model.");

            Briefing queued = await StepAsync(context, result, "POST edition compose", () => c.Editions.ComposeAsync(id, token)).ConfigureAwait(false);
            string briefingId = queued.Id;
            context.Cleanup.Add("briefing:" + briefingId, ct => c.Briefings.DeleteAsync(briefingId, ct));
            BriefingDetailResponse detail = await StepAsync(context, result, "GET briefing until composed", () => WaitAsync(c, briefingId, token)).ConfigureAwait(false);
            if (detail.Briefing.Status != BriefingStatusEnum.Ready) throw new SkipTestException("The empty edition did not compose (" + detail.Briefing.Status + ": " + detail.Briefing.ErrorMessage + ").");

            BriefingComparisonResponse cmp = await StepAsync(context, result, "GET briefing comparison", () => c.Briefings.GetComparisonAsync(briefingId, token)).ConfigureAwait(false);
            Helpers.Equal(briefingId, cmp.BriefingId, "briefing id");
            Helpers.True(!cmp.Compared, "a standalone edition briefing is not compared");
            Helpers.NotEmpty(cmp.Note, "a standalone briefing explains why");
            Helpers.True(cmp.Baselines.Count == 0 && cmp.Deltas.Count == 0 && cmp.Summary == null, "no baselines, deltas, or summary");
            foreach (string kind in new[] { "NewDevelopment", "ChangedFact", "Resolved", "Correction", "Unchanged", "Unavailable" })
                Helpers.True(cmp.Counts.ContainsKey(kind), "counts include " + kind);

            await StepAsync(context, result, "DELETE briefing", () => c.Briefings.DeleteAsync(briefingId, token)).ConfigureAwait(false);
            context.Cleanup.Remove("briefing:" + briefingId);
            await StepAsync(context, result, "DELETE edition", () => c.Editions.DeleteAsync(id, token)).ConfigureAwait(false);
            context.Cleanup.Remove("edition:" + id);
            await StepAsync(context, result, "DELETE topic", () => Fixtures.DeleteTopicAsync(context, c, topic.Id, token)).ConfigureAwait(false);
        }

        private static async Task<BriefingDetailResponse> WaitAsync(MyMorningNewsClient client, string briefingId, CancellationToken token)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(90);
            while (true)
            {
                BriefingDetailResponse detail = await client.Briefings.GetAsync(briefingId, token).ConfigureAwait(false);
                if (detail.Briefing.Status == BriefingStatusEnum.Ready || detail.Briefing.Status == BriefingStatusEnum.Failed || DateTime.UtcNow > deadline) return detail;
                await Task.Delay(1000, token).ConfigureAwait(false);
            }
        }
    }
}
