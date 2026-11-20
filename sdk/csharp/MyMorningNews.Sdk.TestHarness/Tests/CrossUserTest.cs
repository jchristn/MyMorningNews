namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;

    /// <summary>
    /// Isolation: a topic the administrator owns is invisible to another member of the tenant (404 on read, absent from lists).
    /// </summary>
    public class CrossUserTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Cross-user isolation (demo cannot see admin topic)";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public CrossUserTest()
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
            Topic topic = await StepAsync(context, result, "POST topic as admin", () => Fixtures.CreateTopicAsync(context, c, "private", token)).ConfigureAwait(false);
            using (MyMorningNewsClient demo = context.NewClient())
            {
                try
                {
                    await demo.LoginAsync(context.Environment.DemoEmail, context.Environment.DemoPassword, context.Environment.Tenant, token).ConfigureAwait(false);
                }
                catch (MyMorningNewsException ex) when (ex.StatusCode == 401 || ex.StatusCode == 404)
                {
                    throw new SkipTestException("The demo user cannot sign in (" + ex.StatusCode + ").");
                }

                UserResponse me = await StepAsync(context, result, "GET me as demo", () => demo.GetMeAsync(token)).ConfigureAwait(false);
                Helpers.True(!me.IsAdmin && !me.IsTenantAdmin, "demo is an ordinary member");
                await StepAsync(context, result, "GET admin topic as demo (404)", () => Helpers.ExpectErrorAsync(() => demo.Topics.GetAsync(topic.Id, token), 404)).ConfigureAwait(false);
                await StepAsync(context, result, "PUT admin topic as demo (404)", () => Helpers.ExpectErrorAsync(() => demo.Topics.PauseAsync(topic.Id, token), 404)).ConfigureAwait(false);
                EnumerationResult<Topic> list = await StepAsync(context, result, "GET topics as demo (userId filter ignored)",
                    () => demo.Topics.ListAsync(new TopicListOptions { UserId = context.AdminUserId, PageSize = 1000 }, token)).ConfigureAwait(false);
                Helpers.DoesNotContain(list.Items, t => t.Id == topic.Id, "the admin topic in demo's list");
                await demo.LogoutAsync(token).ConfigureAwait(false);
            }

            await StepAsync(context, result, "DELETE topic", () => Fixtures.DeleteTopicAsync(context, c, topic.Id, token)).ConfigureAwait(false);
        }
    }
}
