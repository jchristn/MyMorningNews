namespace Test.Shared.Suites
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Serialization;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Server;
    using SyslogLogging;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Health route smoke tests against an in-process server.
    /// </summary>
    public static class HealthSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: "Health",
                displayName: "Health",
                cases: new[]
                {
                    new TestCaseDescriptor(
                        suiteId: "Health",
                        caseId: "ReturnsHealthy",
                        displayName: "Health route returns Healthy with version",
                        executeAsync: ReturnsHealthyAsync,
                        tags: new[] { "fast", "smoke" })
                });
        }

        private static async Task ReturnsHealthyAsync(CancellationToken token)
        {
            ServerSettings settings = new ServerSettings();
            settings.Rest.Hostname = "127.0.0.1";
            settings.Rest.Port = PortAllocator.GetFreePort();

            using (LoggingModule logging = new LoggingModule())
            using (MyMorningNewsServer server = new MyMorningNewsServer(settings, logging))
            using (HttpClient client = new HttpClient())
            {
                logging.Settings.EnableConsole = false;
                await server.StartAsync(token).ConfigureAwait(false);
                string json = await client.GetStringAsync("http://127.0.0.1:" + settings.Rest.Port + "/v1.0/api/health", token).ConfigureAwait(false);
                HealthResponse response = Assert.NotNull(Serializer.Deserialize<HealthResponse>(json), "health response");
                Assert.Equal("Healthy", response.Status, "status");
                Assert.True(!String.IsNullOrEmpty(response.Version), "version is present");
                server.Stop();
            }
        }
    }
}
