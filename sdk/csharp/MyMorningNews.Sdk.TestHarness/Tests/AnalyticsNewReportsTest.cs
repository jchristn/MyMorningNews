namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;

    /// <summary>
    /// Overview and email analytics. Skipped when the server is older and does not serve them (404).
    /// </summary>
    public class AnalyticsNewReportsTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Analytics overview and email reports";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AnalyticsNewReportsTest()
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
            OverviewReport overview;
            try
            {
                overview = await c.Analytics.OverviewAsync(null, token).ConfigureAwait(false);
            }
            catch (MyMorningNewsException ex) when (ex.StatusCode == 404)
            {
                throw new SkipTestException("The server does not serve analytics/overview yet.");
            }

            Helpers.True(overview.ToUtc > overview.FromUtc, "overview window");
            EmailReport email = await StepAsync(context, result, "GET analytics/email", () => c.Analytics.EmailAsync(null, token)).ConfigureAwait(false);
            Helpers.True(email.Total >= email.Sent, "email totals");
        }
    }
}
