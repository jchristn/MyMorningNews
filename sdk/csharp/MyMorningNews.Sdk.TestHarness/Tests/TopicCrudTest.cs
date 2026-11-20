namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Topics: create, read, update, list, lifecycle, duplicate, ranking recommendation, delete, and 404 after delete.
    /// </summary>
    public class TopicCrudTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Topics CRUD and lifecycle";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TopicCrudTest()
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
            await StepAsync(context, result, "POST topic without a name (400)",
                () => Helpers.ExpectErrorAsync(() => c.Topics.CreateAsync(new TopicUpsertRequest { Description = "no name" }, token), 400, "ValidationFailed")).ConfigureAwait(false);

            Topic topic = await StepAsync(context, result, "POST topic", () => Fixtures.CreateTopicAsync(context, c, "topic", token)).ConfigureAwait(false);
            Helpers.Equal(TopicStatusEnum.Draft, topic.Status, "new topic status");
            Helpers.True(topic.Id.StartsWith("top_", System.StringComparison.Ordinal), "topic id prefix");

            TopicDetailResponse detail = await StepAsync(context, result, "GET topic", () => c.Topics.GetAsync(topic.Id, token)).ConfigureAwait(false);
            Helpers.Equal(topic.Name, detail.Topic.Name, "read back name");

            string renamed = topic.Name + "-renamed";
            Topic updated = await StepAsync(context, result, "PUT topic", () => c.Topics.UpdateAsync(topic.Id, new TopicUpsertRequest
            {
                Name = renamed,
                NoveltyPreference = 80,
                SeedQueries = new List<string> { "postgres release notes", "sqlite release" }
            }, token)).ConfigureAwait(false);
            Helpers.Equal(renamed, updated.Name, "updated name");
            Helpers.Equal(80, updated.NoveltyPreference, "updated novelty");
            Helpers.Equal(2, updated.SeedQueries.Count, "seed queries");

            EnumerationResult<Topic> page = await StepAsync(context, result, "GET topics (search)",
                () => c.Topics.ListAsync(new TopicListOptions { Search = renamed, Mine = true, PageSize = 50 }, token)).ConfigureAwait(false);
            Helpers.Contains(page.Items, t => t.Id == topic.Id, "the topic in the list");
            Helpers.True(page.TotalCount >= 1 && page.PageSize == 50, "page metadata");

            Topic resumed = await StepAsync(context, result, "POST resume (draft with brief becomes Active)", () => c.Topics.ResumeAsync(topic.Id, token)).ConfigureAwait(false);
            Helpers.Equal(TopicStatusEnum.Active, resumed.Status, "resumed status");
            Topic paused = await StepAsync(context, result, "POST pause", () => c.Topics.PauseAsync(topic.Id, token)).ConfigureAwait(false);
            Helpers.Equal(TopicStatusEnum.Paused, paused.Status, "paused status");
            Topic archived = await StepAsync(context, result, "POST archive", () => c.Topics.ArchiveAsync(topic.Id, token)).ConfigureAwait(false);
            Helpers.Equal(TopicStatusEnum.Archived, archived.Status, "archived status");

            TopicDetailResponse copy = await StepAsync(context, result, "POST duplicate", () => c.Topics.DuplicateAsync(topic.Id, token)).ConfigureAwait(false);
            context.Cleanup.Add("topic:" + copy.Topic.Id, ct => c.Topics.DeleteAsync(copy.Topic.Id, ct));
            Helpers.True(copy.Topic.Id != topic.Id && copy.Topic.Name.EndsWith("(copy)", System.StringComparison.Ordinal), "duplicate is a new draft named (copy)");
            await StepAsync(context, result, "DELETE duplicate", () => Fixtures.DeleteTopicAsync(context, c, copy.Topic.Id, token)).ConfigureAwait(false);

            RankingRecommendation ranking = await StepAsync(context, result, "GET ranking-recommendation", () => c.Topics.GetRankingRecommendationAsync(topic.Id, token)).ConfigureAwait(false);
            Helpers.NotNull(ranking, "ranking recommendation");

            await StepAsync(context, result, "DELETE topic", () => Fixtures.DeleteTopicAsync(context, c, topic.Id, token)).ConfigureAwait(false);
            await StepAsync(context, result, "GET deleted topic (404)", () => Helpers.ExpectErrorAsync(() => c.Topics.GetAsync(topic.Id, token), 404, "NotFound")).ConfigureAwait(false);
        }
    }
}
