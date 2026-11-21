namespace MyMorningNews.Sdk.TestHarness
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.TestHarness.Tests;

    /// <summary>
    /// My Morning News C# SDK test harness. Runs every case against a live server, prints one line per case, cleans up what it
    /// created, and exits non-zero on any failure.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="args">--base-url, --email, --password, --tenant, --with-run, --verbose.</param>
        /// <returns>0 when every case passed or was skipped; 1 on any failure; 2 on bad arguments.</returns>
        public static async Task<int> Main(string[] args)
        {
            TestEnvironment env;
            try
            {
                env = TestEnvironment.Parse(args);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 2;
            }

            Console.WriteLine("My Morning News C# SDK harness");
            Console.WriteLine("Server: " + env.BaseUrl + "  user: " + env.Email + "  tenant: " + env.Tenant + "  with-run: " + (env.WithRun ? "yes" : "no"));
            Console.WriteLine();

            TestContext context = new TestContext(env);
            List<TestResult> results = new List<TestResult>();
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                foreach (TestBase test in Cases())
                {
                    TestResult r = await RunOneAsync(test, context, cts.Token).ConfigureAwait(false);
                    results.Add(r);
                    Print(r);
                    if (cts.IsCancellationRequested) break;
                }

                List<string> leftovers = await context.Cleanup.RunAsync(CancellationToken.None).ConfigureAwait(false);
                if (context.Admin != null)
                {
                    try
                    {
                        await context.Admin.LogoutAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (MyMorningNewsException)
                    {
                    }

                    context.Admin.Dispose();
                }

                Console.WriteLine();
                foreach (string l in leftovers) Console.WriteLine("CLEANUP FAILED " + l);
                int pass = results.Count(x => x.Outcome == TestOutcomeEnum.Pass);
                int fail = results.Count(x => x.Outcome == TestOutcomeEnum.Fail);
                int skip = results.Count(x => x.Outcome == TestOutcomeEnum.Skip);
                long ms = results.Sum(x => x.ElapsedMs);
                Console.WriteLine("Summary: " + results.Count + " cases, " + pass + " passed, " + fail + " failed, " + skip + " skipped (" + ms + " ms)");
                if (fail > 0)
                {
                    Console.WriteLine("Failed:");
                    foreach (TestResult f in results.Where(x => x.Outcome == TestOutcomeEnum.Fail)) Console.WriteLine("  " + f.Name + ": " + f.Message);
                }

                return fail > 0 || leftovers.Count > 0 ? 1 : 0;
            }
        }

        private static List<TestBase> Cases()
        {
            return new List<TestBase>
            {
                new SignerVectorTest(),
                new TolerantEnumTest(),
                new SetupWireTest(),
                new ListOptionsTest(),
                new HealthTest(),
                new LoginTest(),
                new AuthErrorTest(),
                new TopicCrudTest(),
                new TopicChildrenTest(),
                new CrossUserTest(),
                new RefinementTest(),
                new ScheduleTest(),
                new RunsReadTest(),
                new ResearchReadTest(),
                new FindingsEditTest(),
                new PreferencesFeedbackTest(),
                new EditionTest(),
                new BriefingsTest(),
                new TemplatesTest(),
                new PromptsTest(),
                new MediaArtifactsTest(),
                new FeedTokensTest(),
                new EmailTest(),
                new AnalyticsUsageTest(),
                new AnalyticsNewReportsTest(),
                new ModelEndpointsTest(),
                new SetupTest(),
                new SearchVoicesTest(),
                new VaultDevicesTest(),
                new AccountTest(),
                new AccountLimitsTest(),
                new SignupTest(),
                new McpServersTest(),
                new AdminUsersRolesTest(),
                new AdminCredentialsTest(),
                new SignedRequestTest(),
                new AdminPromoPlansTest(),
                new AdminAuditHistoryTest(),
                new RunNowWaitTest(),
                new RefinementGenerateTest(),
                new ResearchRequestTest()
            };
        }

        private static async Task<TestResult> RunOneAsync(TestBase test, TestContext context, CancellationToken token)
        {
            TestResult r = new TestResult { Name = test.Name, StartUtc = DateTime.UtcNow };
            try
            {
                if (test.RequiresRun && !context.Environment.WithRun)
                {
                    r.Outcome = TestOutcomeEnum.Skip;
                    r.Message = "needs --with-run (calls real models or starts research runs)";
                }
                else
                {
                    await test.RunAsync(context, r, token).ConfigureAwait(false);
                    r.Outcome = TestOutcomeEnum.Pass;
                }
            }
            catch (SkipTestException ex)
            {
                r.Outcome = TestOutcomeEnum.Skip;
                r.Message = ex.Message;
            }
            catch (HarnessAssertionException ex)
            {
                r.Outcome = TestOutcomeEnum.Fail;
                r.Message = ex.Message;
            }
            catch (MyMorningNewsException ex)
            {
                r.Outcome = TestOutcomeEnum.Fail;
                r.Message = "HTTP " + ex.StatusCode + " " + ex.ErrorCode + ": " + ex.Message;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException && token.IsCancellationRequested))
            {
                r.Outcome = TestOutcomeEnum.Fail;
                r.Message = ex.GetType().Name + ": " + ex.Message;
            }
            catch (OperationCanceledException)
            {
                r.Outcome = TestOutcomeEnum.Fail;
                r.Message = "Canceled.";
            }
            finally
            {
                r.EndUtc = DateTime.UtcNow;
            }

            return r;
        }

        private static void Print(TestResult r)
        {
            string label = r.Outcome == TestOutcomeEnum.Pass ? "PASS" : r.Outcome == TestOutcomeEnum.Fail ? "FAIL" : "SKIP";
            string line = label + "  " + r.Name + "  (" + r.ElapsedMs + " ms)";
            if (r.Outcome != TestOutcomeEnum.Pass && !String.IsNullOrEmpty(r.Message)) line += "  " + r.Message;
            Console.WriteLine(line);
        }
    }
}
