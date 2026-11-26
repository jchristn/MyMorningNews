namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Topic keep order: read, choose a topic first, reject duplicates and unknown topics, clear, and restore the original
    /// order afterward.
    /// </summary>
    public class TopicSlotsTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Topic slots get, set, clear";

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
            List<string> keep = original.Topics.Where(t => t.Chosen).OrderBy(t => t.Position).Select(t => t.TopicId).ToList();
            context.Cleanup.Add("topic-slots", ct => c.Account.SetTopicSlotsAsync(new TopicSlotsRequest { KeepTopicIds = keep }, ct));

            Topic topic = await StepAsync(context, result, "POST topic", () => Fixtures.CreateTopicAsync(context, c, "slot-topic", token)).ConfigureAwait(false);
            TopicSlotsResponse chosen = await StepAsync(context, result, "PUT topic slots", () => c.Account.SetTopicSlotsAsync(new TopicSlotsRequest { KeepTopicIds = new List<string> { topic.Id } }, token)).ConfigureAwait(false);
            Helpers.True(chosen.Customized, "the keep order is customized");
            TopicSlotResponse? first = chosen.Topics.FirstOrDefault(t => t.TopicId == topic.Id);
            Helpers.True(first != null && first.Position == 1 && first.Chosen, "the chosen topic is kept first");
            await StepAsync(context, result, "PUT topic slots with a duplicate (400)",
                () => Helpers.ExpectErrorAsync(() => c.Account.SetTopicSlotsAsync(new TopicSlotsRequest { KeepTopicIds = new List<string> { topic.Id, topic.Id } }, token), 400)).ConfigureAwait(false);
            await StepAsync(context, result, "PUT topic slots with an unknown topic (400)",
                () => Helpers.ExpectErrorAsync(() => c.Account.SetTopicSlotsAsync(new TopicSlotsRequest { KeepTopicIds = new List<string> { "top_missing" } }, token), 400)).ConfigureAwait(false);

            TopicSlotsResponse cleared = await StepAsync(context, result, "PUT topic slots (clear)", () => c.Account.ClearTopicSlotsAsync(token)).ConfigureAwait(false);
            Helpers.True(!cleared.Customized && cleared.Topics.All(t => !t.Chosen), "the keep order is cleared");

            await StepAsync(context, result, "PUT topic slots (restore)", () => c.Account.SetTopicSlotsAsync(new TopicSlotsRequest { KeepTopicIds = keep }, token)).ConfigureAwait(false);
            context.Cleanup.Remove("topic-slots");
            await StepAsync(context, result, "DELETE topic", () => Fixtures.DeleteTopicAsync(context, c, topic.Id, token)).ConfigureAwait(false);
        }
    }
}
