namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Serialization;

    /// <summary>
    /// Offline: unknown enum names from a newer server read as the default member instead of failing; known names round-trip.
    /// </summary>
    public class TolerantEnumTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name
        {
            get
            {
                return "Offline: tolerant enums and camelCase JSON";
            }
        }

        /// <summary>
        /// Offline.
        /// </summary>
        public override bool RequiresServer
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TolerantEnumTest()
        {
        }

        /// <summary>
        /// Run the case.
        /// </summary>
        /// <param name="context">Shared state.</param>
        /// <param name="result">Result.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public override Task RunAsync(TestContext context, TestResult result, CancellationToken token)
        {
            Run? run = MyMorningNewsSerializer.Deserialize<Run>("{\"id\":\"run_x\",\"status\":\"SomethingFromTheFuture\",\"trigger\":\"manual\",\"unknownField\":1}");
            Helpers.NotNull(run, "run");
            Helpers.Equal("run_x", run!.Id, "id");
            Helpers.Equal(RunStatusEnum.Queued, run.Status, "unknown status falls back to the first member");
            Helpers.Equal(RunTriggerEnum.Manual, run.Trigger, "case-insensitive enum name");

            Topic topic = new Topic { Name = "x", Status = TopicStatusEnum.Paused, Description = null };
            string json = MyMorningNewsSerializer.Serialize(topic);
            Helpers.True(json.Contains("\"status\":\"Paused\"", System.StringComparison.Ordinal), "enums are written as names");
            Helpers.True(!json.Contains("\"description\"", System.StringComparison.Ordinal), "nulls are omitted");
            Helpers.True(json.Contains("\"name\":\"x\"", System.StringComparison.Ordinal), "names are camelCase");
            return Task.CompletedTask;
        }
    }
}
