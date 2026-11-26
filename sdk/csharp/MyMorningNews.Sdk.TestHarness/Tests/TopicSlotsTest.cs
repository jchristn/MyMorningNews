namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Topic keep order: read the summary and the paged topic list, choose a topic first, reject duplicates and unknown
    /// topics, clear, and restore the original order afterward.
    /// </summary>
    public class TopicSlotsTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Topic slots get, list, set, clear";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TopicSlotsTest()
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
            TopicSlotsResponse original = await StepAsync(context, result, "GET topic slots", () => c.Account.GetTopicSlotsAsync(token)).ConfigureAwait(false);
            List<TopicSlotResponse> originalTopics = await StepAsync(context, result, "GET topic slot topics (all pages)", () => AllTopicsAsync(c, token)).ConfigureAwait(false);
            List<string> keep = originalTopics.Where(t => t.Chosen).OrderBy(t => t.Position).Select(t => t.TopicId).ToList();
            context.Cleanup.Add("topic-slots", ct => c.Account.SetTopicSlotsAsync(new TopicSlotsRequest { KeepTopicIds = keep }, ct));
            Helpers.Equal(original.Customized, keep.Count > 0, "customized matches the chosen topics");

            Topic topic = await StepAsync(context, result, "POST topic", () => Fixtures.CreateTopicAsync(context, c, "slot-topic", token)).ConfigureAwait(false);
            TopicSlotsResponse chosen = await StepAsync(context, result, "PUT topic slots", () => c.Account.SetTopicSlotsAsync(new TopicSlotsRequest { KeepTopicIds = new List<string> { topic.Id } }, token)).ConfigureAwait(false);
            Helpers.True(chosen.Customized, "the keep order is customized");
            EnumerationResult<TopicSlotResponse> firstPage = await StepAsync(context, result, "GET topic slot topics (page 1, size 1)", () => c.Account.GetTopicSlotTopicsAsync(new ListOptions { PageSize = 1 }, token)).ConfigureAwait(false);
            TopicSlotResponse? first = firstPage.Objects.FirstOrDefault();
            Helpers.True(first != null && first.TopicId == topic.Id && first.Position == 1 && first.Chosen, "the chosen topic is kept first");
            Helpers.Equal(chosen.Counted, firstPage.TotalRecords, "the topic list covers every counted topic");
            EnumerationResult<TopicSlotResponse> searched = await StepAsync(context, result, "GET topic slot topics (search)", () => c.Account.GetTopicSlotTopicsAsync(new ListOptions { Search = topic.Name, PageSize = 10 }, token)).ConfigureAwait(false);
            Helpers.True(searched.Objects.Any(t => t.TopicId == topic.Id && t.Position == 1), "search matches the name and keeps the whole-order position");
            await StepAsync(context, result, "PUT topic slots with a duplicate (400)",
                () => Helpers.ExpectErrorAsync(() => c.Account.SetTopicSlotsAsync(new TopicSlotsRequest { KeepTopicIds = new List<string> { topic.Id, topic.Id } }, token), 400)).ConfigureAwait(false);
            await StepAsync(context, result, "PUT topic slots with an unknown topic (400)",
                () => Helpers.ExpectErrorAsync(() => c.Account.SetTopicSlotsAsync(new TopicSlotsRequest { KeepTopicIds = new List<string> { "top_missing" } }, token), 400)).ConfigureAwait(false);

            TopicSlotsResponse cleared = await StepAsync(context, result, "PUT topic slots (clear)", () => c.Account.ClearTopicSlotsAsync(token)).ConfigureAwait(false);
            List<TopicSlotResponse> clearedTopics = await StepAsync(context, result, "GET topic slot topics after clear", () => AllTopicsAsync(c, token)).ConfigureAwait(false);
            Helpers.True(!cleared.Customized && clearedTopics.All(t => !t.Chosen), "the keep order is cleared");

            await StepAsync(context, result, "PUT topic slots (restore)", () => c.Account.SetTopicSlotsAsync(new TopicSlotsRequest { KeepTopicIds = keep }, token)).ConfigureAwait(false);
            context.Cleanup.Remove("topic-slots");
            await StepAsync(context, result, "DELETE topic", () => Fixtures.DeleteTopicAsync(context, c, topic.Id, token)).ConfigureAwait(false);
        }

        private static async Task<List<TopicSlotResponse>> AllTopicsAsync(MyMorningNewsClient client, CancellationToken token)
        {
            List<TopicSlotResponse> all = new List<TopicSlotResponse>();
            int pageNumber = 1;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                EnumerationResult<TopicSlotResponse> page = await client.Account.GetTopicSlotTopicsAsync(new ListOptions { PageNumber = pageNumber, PageSize = 1000 }, token).ConfigureAwait(false);
                all.AddRange(page.Objects);
                if (pageNumber >= page.TotalPages || !page.Objects.Any()) break;
                pageNumber++;
            }
            return all;
        }
    }
}
