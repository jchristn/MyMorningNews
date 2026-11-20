namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;

    /// <summary>
    /// Anonymous routes: health and client configuration.
    /// </summary>
    public class HealthTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Health and client config (anonymous)";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public HealthTest()
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
            using (MyMorningNewsClient anon = context.NewClient())
            {
                HealthResponse health = await StepAsync(context, result, "GET health", () => anon.GetHealthAsync(token)).ConfigureAwait(false);
                Helpers.Equal("Healthy", health.Status, "health status");
                Helpers.NotEmpty(health.Version, "server version");
                Helpers.True(health.Components.ContainsKey("database"), "health lists the database component");

                ClientConfigResponse config = await StepAsync(context, result, "GET client-config", () => anon.GetClientConfigAsync(token)).ConfigureAwait(false);
                Helpers.NotEmpty(config.ProductName, "product name");
                Helpers.True(config.Links.ContainsKey("privacy"), "client config has a privacy link");
            }
        }
    }
}
