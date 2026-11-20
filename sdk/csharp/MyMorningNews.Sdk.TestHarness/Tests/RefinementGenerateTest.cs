namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// With --with-run only: the model-backed wizard steps (dimensions, questions, an answer, context), then abandon.
    /// </summary>
    public class RefinementGenerateTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Refinement generate dimensions, questions, answer, context";

        /// <summary>
        /// Calls real models.
        /// </summary>
        public override bool RequiresRun => true;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RefinementGenerateTest()
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
            RefinementStateResponse state = await StepAsync(context, result, "POST refinement-session", () => c.Refinement.StartAsync(new RefinementStartRequest
            {
                SeedText = context.Name("wizard") + " track open source vector database releases"
            }, token)).ConfigureAwait(false);
            string sid = state.Session.Id;
            string topicId = state.Topic.Id;
            context.Cleanup.Add("topic:" + topicId, ct => c.Topics.DeleteAsync(topicId, ct));
            RefinementStateResponse dims = await StepAsync(context, result, "POST dimensions", () => c.Refinement.GenerateDimensionsAsync(sid, null, token)).ConfigureAwait(false);
            Helpers.True(dims.Dimensions.Count > 0, "dimensions proposed");
            RefinementStateResponse qs = await StepAsync(context, result, "POST questions", () => c.Refinement.GenerateQuestionsAsync(sid, new RefinementGenerateRequest(), token)).ConfigureAwait(false);
            RefinementQuestionResponse? q = qs.Questions.FirstOrDefault();
            if (q != null)
                await StepAsync(context, result, "PUT question (skip)", () => c.Refinement.AnswerAsync(sid, q.Question.Id, new QuestionAnswerRequest { Skipped = true }, token)).ConfigureAwait(false);
            await StepAsync(context, result, "POST context", () => c.Refinement.GenerateContextAsync(sid, token)).ConfigureAwait(false);
            await StepAsync(context, result, "POST abandon", () => c.Refinement.AbandonAsync(sid, token)).ConfigureAwait(false);
            await c.Topics.DeleteAsync(topicId, token).ConfigureAwait(false);
            context.Cleanup.Remove("topic:" + topicId);
        }
    }
}
