namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Comparison baselines and system briefings: topic and edition history previews (nothing saved, bounds enforced),
    /// edition history settings, and edition publications read and replaced with an empty list.
    /// </summary>
    public class ComparisonPreviewTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "History previews and edition publications";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ComparisonPreviewTest()
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
            Topic topic = await StepAsync(context, result, "POST topic", () => Fixtures.CreateTopicAsync(context, c, "history-topic", token)).ConfigureAwait(false);

            HistoryPreviewResponse saved = await StepAsync(context, result, "POST topic history preview (saved settings)", () => c.Topics.PreviewHistoryAsync(topic.Id, null, token)).ConfigureAwait(false);
            Helpers.True(saved.Baselines.Count == 0, "a new topic has no baselines");
            HistoryPreviewResponse recent = await StepAsync(context, result, "POST topic history preview (unsaved RecentIssues)",
                () => c.Topics.PreviewHistoryAsync(topic.Id, new HistoryPreviewRequest { HistoryMode = HistoryModeEnum.RecentIssues, HistoryCount = 3 }, token)).ConfigureAwait(false);
            Helpers.Equal("RecentIssues", recent.Mode, "previewed mode");
            await StepAsync(context, result, "POST topic history preview out of bounds (400)",
                () => Helpers.ExpectErrorAsync(() => c.Topics.PreviewHistoryAsync(topic.Id, new HistoryPreviewRequest { HistoryMode = HistoryModeEnum.RecentIssues, HistoryCount = 99 }, token), 400)).ConfigureAwait(false);
            await StepAsync(context, result, "POST history preview of an unknown topic (404)", () => Helpers.ExpectErrorAsync(() => c.Topics.PreviewHistoryAsync("top_missing", null, token), 404)).ConfigureAwait(false);
            TopicDetailResponse unchanged = await StepAsync(context, result, "GET topic after preview", () => c.Topics.GetAsync(topic.Id, token)).ConfigureAwait(false);
            Helpers.Equal(topic.HistoryMode, unchanged.Topic.HistoryMode, "preview saves nothing");

            EditionResponse e = await StepAsync(context, result, "POST edition with history settings", () => c.Editions.CreateAsync(new EditionUpsertRequest
            {
                Name = context.Name("history-edition"),
                AudioEnabled = false,
                EmailEnabled = false,
                Enabled = false,
                HistoryMode = HistoryModeEnum.PreviousIssue,
                HistoryTokenBudget = 4000
            }, token)).ConfigureAwait(false);
            string id = e.Edition.Id;
            context.Cleanup.Add("edition:" + id, ct => c.Editions.DeleteAsync(id, ct));
            Helpers.Equal(HistoryModeEnum.PreviousIssue, e.Edition.HistoryMode, "edition history mode");
            Helpers.Equal(4000, e.Edition.HistoryTokenBudget, "edition history budget");

            HistoryPreviewResponse editionPreview = await StepAsync(context, result, "POST edition history preview", () => c.Editions.PreviewHistoryAsync(id, null, token)).ConfigureAwait(false);
            Helpers.Equal("PreviousIssue", editionPreview.Mode, "edition preview mode");
            Helpers.True(editionPreview.Baselines.Count == 0, "a new edition has no earlier issues");
            await StepAsync(context, result, "POST edition history preview pinned to an unknown issue (400)",
                () => Helpers.ExpectErrorAsync(() => c.Editions.PreviewHistoryAsync(id, new HistoryPreviewRequest { HistoryMode = HistoryModeEnum.SpecificIssue, HistoryBriefingId = "brf_missing" }, token), 400)).ConfigureAwait(false);
            EditionResponse off = await StepAsync(context, result, "PUT edition history mode Off", () => c.Editions.UpdateAsync(id, new EditionUpsertRequest { HistoryMode = HistoryModeEnum.Off }, token)).ConfigureAwait(false);
            Helpers.Equal(HistoryModeEnum.Off, off.Edition.HistoryMode, "edition history mode off");

            EnumerationResult<EditionPublicationResponse> pubs = await StepAsync(context, result, "GET edition publications", () => c.Editions.GetPublicationsAsync(id, null, token)).ConfigureAwait(false);
            Helpers.True(pubs.Objects.Count == 0 && pubs.TotalRecords == 0, "a new edition includes no system briefings");
            EditionResponse cleared = await StepAsync(context, result, "PUT edition publications (empty)", () => c.Editions.SetPublicationsAsync(id, new EditionPublicationsRequest(), token)).ConfigureAwait(false);
            Helpers.True(cleared.Publications.Count == 0, "the edition response lists no publications");
            await StepAsync(context, result, "PUT edition publications with an unsubscribed publication (400)",
                () => Helpers.ExpectErrorAsync(() => c.Editions.SetPublicationsAsync(id, new EditionPublicationsRequest { PublicationIds = new List<string> { "cat_missing" } }, token), 400)).ConfigureAwait(false);
            await StepAsync(context, result, "GET publications of an unknown edition (404)", () => Helpers.ExpectErrorAsync(() => c.Editions.GetPublicationsAsync("edn_missing", null, token), 404)).ConfigureAwait(false);

            await StepAsync(context, result, "DELETE edition", () => c.Editions.DeleteAsync(id, token)).ConfigureAwait(false);
            context.Cleanup.Remove("edition:" + id);
            await StepAsync(context, result, "DELETE topic", () => Fixtures.DeleteTopicAsync(context, c, topic.Id, token)).ConfigureAwait(false);
        }
    }
}
