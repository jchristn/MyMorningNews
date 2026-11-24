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
    /// Refinement sessions without model calls: start, read, list, accept sources, abandon (deletes the draft), and refine an
    /// existing topic.
    /// </summary>
    public class RefinementTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Refinement start, get, list, accept sources, abandon";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RefinementTest()
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
            await StepAsync(context, result, "POST refinement-session with no seed (400)", async () =>
            {
                try
                {
                    await c.Refinement.StartAsync(new RefinementStartRequest { SeedText = " " }, token).ConfigureAwait(false);
                }
                catch (SetupRequiredException ex)
                {
                    throw new SkipTestException("the workspace is not set up for model work (409 SetupRequired, check "
                        + Helpers.NotEmpty(ex.Check, "setup check key") + "); store a model key with Setup.SetModelKeyAsync first");
                }
                catch (MyMorningNewsException ex) when (ex.StatusCode == 400)
                {
                    return;
                }

                throw new HarnessAssertionException("Expected HTTP 400 but the call succeeded.");
            }).ConfigureAwait(false);

            RefinementStateResponse state = await StepAsync(context, result, "POST refinement-session", () => c.Refinement.StartAsync(new RefinementStartRequest
            {
                SeedText = context.Name("wizard") + " follow vector database benchmarks",
                Goal = "Harness check"
            }, token)).ConfigureAwait(false);
            string sessionId = state.Session.Id;
            string topicId = state.Topic.Id;
            context.Cleanup.Add("topic:" + topicId, ct => c.Topics.DeleteAsync(topicId, ct));
            Helpers.Equal(TopicStatusEnum.Draft, state.Topic.Status, "wizard topic starts as Draft");

            RefinementStateResponse read = await StepAsync(context, result, "GET refinement-session", () => c.Refinement.GetAsync(sessionId, token)).ConfigureAwait(false);
            Helpers.Equal(topicId, read.Topic.Id, "session topic");

            EnumerationResult<RefinementSession> list = await StepAsync(context, result, "GET refinement-sessions (topic filter)",
                () => c.Refinement.ListAsync(new RefinementSessionListOptions { TopicId = topicId }, token)).ConfigureAwait(false);
            Helpers.Contains(list.Objects, s => s.Id == sessionId, "the session");

            RefinementStateResponse accepted = await StepAsync(context, result, "POST sources/accept", () => c.Refinement.AcceptSourcesAsync(sessionId, new SourceAcceptRequest
            {
                FeedUrls = new List<string> { "https://example.com/feed.xml" },
                SeedUrls = new List<string> { "https://example.com/" }
            }, token)).ConfigureAwait(false);
            Helpers.Contains(accepted.Constraints, k => k.Kind == ConstraintKindEnum.FeedUrl && k.Value == "https://example.com/feed.xml", "the accepted feed");

            await StepAsync(context, result, "POST abandon", () => c.Refinement.AbandonAsync(sessionId, token)).ConfigureAwait(false);
            await StepAsync(context, result, "GET abandoned draft topic (404)", () => Helpers.ExpectErrorAsync(() => c.Topics.GetAsync(topicId, token), 404)).ConfigureAwait(false);
            context.Cleanup.Remove("topic:" + topicId);

            Topic existing = await StepAsync(context, result, "POST topic", () => Fixtures.CreateTopicAsync(context, c, "refine", token)).ConfigureAwait(false);
            await StepAsync(context, result, "POST resume (refine an Active topic)", () => c.Topics.ResumeAsync(existing.Id, token)).ConfigureAwait(false);
            RefinementStateResponse again = await StepAsync(context, result, "POST topic refine", () => c.Topics.RefineAsync(existing.Id, token)).ConfigureAwait(false);
            Helpers.Equal(existing.Id, again.Topic.Id, "refine uses the existing topic");
            await StepAsync(context, result, "POST abandon refine session", () => c.Refinement.AbandonAsync(again.Session.Id, token)).ConfigureAwait(false);
            TopicDetailResponse kept = await StepAsync(context, result, "GET topic after abandoning its refine session", () => c.Topics.GetAsync(existing.Id, token)).ConfigureAwait(false);
            Helpers.Equal(existing.Id, kept.Topic.Id, "an Active topic survives an abandoned refine session");
            await StepAsync(context, result, "DELETE topic", () => Fixtures.DeleteTopicAsync(context, c, existing.Id, token)).ConfigureAwait(false);
            await StepAsync(context, result, "GET unknown session (404)", () => Helpers.ExpectErrorAsync(() => c.Refinement.GetAsync("rfs_missing", token), 404)).ConfigureAwait(false);
        }
    }
}
