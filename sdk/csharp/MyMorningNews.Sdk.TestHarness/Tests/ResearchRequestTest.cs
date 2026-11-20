namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// With --with-run only: create (plan) a one-time request, read it, cancel it before it starts, and delete it.
    /// </summary>
    public class ResearchRequestTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "One-time request create, get, cancel, delete";

        /// <summary>
        /// Calls real models.
        /// </summary>
        public override bool RequiresRun => true;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ResearchRequestTest()
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
            ResearchRequestResponse created = await StepAsync(context, result, "POST request", () => c.Requests.CreateAsync(new ResearchRequestCreateRequest
            {
                RequestText = context.Name("request") + " What changed in the latest PostgreSQL release?",
                SkipQuestions = true,
                RunAtUtc = System.DateTime.UtcNow.AddDays(7)
            }, token)).ConfigureAwait(false);
            string id = created.Request.Id;
            context.Cleanup.Add("request:" + id, ct => c.Requests.DeleteAsync(id, ct));
            ResearchRequestResponse read = await StepAsync(context, result, "GET request", () => c.Requests.GetAsync(id, token)).ConfigureAwait(false);
            Helpers.Equal(id, read.Request.Id, "request id");
            await StepAsync(context, result, "POST cancel", () => c.Requests.CancelAsync(id, token)).ConfigureAwait(false);
            await StepAsync(context, result, "DELETE request", () => c.Requests.DeleteAsync(id, token)).ConfigureAwait(false);
            context.Cleanup.Remove("request:" + id);
            await StepAsync(context, result, "GET deleted request (404)", () => Helpers.ExpectErrorAsync(() => c.Requests.GetAsync(id, token), 404)).ConfigureAwait(false);
        }
    }
}
