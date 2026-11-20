namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Entitlements;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Server;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Plan limits (plan section 33.2): topic counts, the monthly model cost cap, daily runs, the shortest schedule,
    /// custom model endpoints, and the usage meter. Refusals are 402 CapReached with the key, limit, and reset time.
    /// </summary>
    public static class EntitlementSuite
    {
        private const string S = "Entitlements";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Plan limits and the usage meter", new[]
            {
                Cases.Case(S, "TopicLimit", "The Free plan allows one topic; the second is refused with 402 CapReached", TopicLimitAsync),
                Cases.Case(S, "CostCap", "Past the monthly cost cap, runs and model calls are refused until the reset", CostCapAsync),
                Cases.Case(S, "ScheduledCap", "A scheduled run past the cap is skipped with the reason, and the owner hears about it once a month", ScheduledCapAsync),
                Cases.Case(S, "DailyRuns", "The daily run limit counts today's runs", DailyRunsAsync),
                Cases.Case(S, "ScheduleAndEndpoints", "The plan's shortest schedule and its endpoint rule apply", ScheduleAndEndpointsAsync),
                Cases.Case(S, "Meter", "The usage meter reports the plan, spend, counts, and limits", MeterAsync)
            });
        }

        private static async Task<TestUser> FreeUserAsync(TestApiClient admin, CancellationToken token, bool tenantAdmin = false)
        {
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser u = await TestFixtures.CreateUserAsync(admin, t.Id, tenantAdmin, token).ConfigureAwait(false);
            List<PlanResponse> plans = (await admin.GetAsync("/v1.0/api/admin/plans", token).ConfigureAwait(false)).Expect(200, "plans").Json<List<PlanResponse>>();
            string free = plans.Single(p => p.Plan.Key == "free").Plan.Id;
            (await admin.PostAsync("/v1.0/api/admin/plan-assignments", new { tenantId = t.Id, userId = u.User.Id, planId = free, durationDays = 30 }, token).ConfigureAwait(false)).Expect(201, "grant free");
            return u;
        }

        private static void ExpectCap(TestResponse r, string key, string what)
        {
            r.Expect(402, what);
            JsonElement e = r.Json<JsonElement>();
            Assert.Equal("CapReached", e.GetProperty("error").GetString() ?? String.Empty, what + " error");
            Assert.Equal(key, e.GetProperty("details").GetProperty("key").GetString() ?? String.Empty, what + " key");
        }

        private static async Task<Topic> TopicAsync(TestUser u, CancellationToken token)
        {
            return (await u.Client.PostAsync("/v1.0/api/topics", new { name = "Plan topic " + Guid.NewGuid().ToString("N").Substring(0, 6), brief = "Limits", status = "Active" }, token).ConfigureAwait(false)).Expect(201, "topic").Json<Topic>();
        }

        private static async Task TopicLimitAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            TestUser u = await FreeUserAsync(admin, token).ConfigureAwait(false);
            Topic first = await TopicAsync(u, token).ConfigureAwait(false);
            ExpectCap(await u.Client.PostAsync("/v1.0/api/topics", new { name = "Second topic" }, token).ConfigureAwait(false), "topics.max", "second topic");
            ExpectCap(await u.Client.PostAsync("/v1.0/api/topics/" + first.Id + "/duplicate", null, token).ConfigureAwait(false), "topics.max", "duplicate");
            ExpectCap(await u.Client.PostAsync("/v1.0/api/refinement-sessions", new { seedText = "Another topic about grid batteries" }, token).ConfigureAwait(false), "topics.max", "wizard");
            TestUser unlimited = await TestFixtures.CreateUserAsync(admin, u.User.TenantId, false, token).ConfigureAwait(false);
            await TopicAsync(unlimited, token).ConfigureAwait(false);
            await TopicAsync(unlimited, token).ConfigureAwait(false);
        }

        private static async Task CostCapAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            TestUser u = await FreeUserAsync(admin, token).ConfigureAwait(false);
            Topic topic = await TopicAsync(u, token).ConfigureAwait(false);
            await server.Context.Database.LlmCalls.CreateAsync(new LlmCall
            {
                TenantId = u.User.TenantId, UserId = u.User.Id, TopicId = topic.Id, Task = ModelTaskEnum.Synthesis, Provider = "Anthropic", Model = "claude-opus-5-5",
                EstimatedCostUsd = 3.50m, InputCostUsd = 2.00m, OutputCostUsd = 1.50m, CreatedUtc = DateTime.UtcNow
            }, token).ConfigureAwait(false);
            UsageMeter meter = (await u.Client.GetAsync("/v1.0/api/account/usage", token).ConfigureAwait(false)).Expect(200, "meter").Json<UsageMeter>();
            Assert.True(meter.MonthToDateCostUsd >= 3.50m && meter.MonthlyCostCapUsd == 3.00m && meter.CostUsedFraction >= 1, "meter shows the cap reached: " + meter.MonthToDateCostUsd);

            TestResponse run = await u.Client.PostAsync("/v1.0/api/topics/" + topic.Id + "/run", null, token).ConfigureAwait(false);
            ExpectCap(run, "usage.monthlyCostUsd.max", "run now");
            JsonElement details = run.Json<JsonElement>().GetProperty("details");
            Assert.Equal("3.00", details.GetProperty("limit").GetString() ?? String.Empty, "limit");
            Assert.True(details.GetProperty("resetUtc").GetDateTime() == EntitlementService.MonthlyReset(DateTime.UtcNow), "resets at the start of next month");

            bool refused = false;
            try
            {
                await server.Context.Models.ChatAsync(new ModelCallRequest { TenantId = u.User.TenantId, UserId = u.User.Id, Task = ModelTaskEnum.Synthesis, UserPrompt = "hello" }, token).ConfigureAwait(false);
            }
            catch (CapReachedException cap)
            {
                refused = cap.Key == "usage.monthlyCostUsd.max";
            }

            Assert.True(refused, "the model gateway refuses calls past the cap");
        }

        private static async Task ScheduledCapAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            TestUser u = await FreeUserAsync(admin, token).ConfigureAwait(false);
            Topic topic = await TopicAsync(u, token).ConfigureAwait(false);
            Schedule s = (await u.Client.PostAsync("/v1.0/api/schedules", new { ownerId = topic.Id, intervalType = "DaysOfWeek", daysOfWeekMask = 127, timesOfDay = new[] { "06:30" }, timeZone = "UTC" }, token).ConfigureAwait(false))
                .Expect(201, "daily schedule").Json<Schedule>();
            await server.Context.Database.LlmCalls.CreateAsync(new LlmCall
            {
                TenantId = u.User.TenantId, UserId = u.User.Id, TopicId = topic.Id, Task = ModelTaskEnum.Synthesis, Provider = "Anthropic", Model = "claude-opus-5-5", EstimatedCostUsd = 4m, CreatedUtc = DateTime.UtcNow
            }, token).ConfigureAwait(false);
            DateTime now = DateTime.UtcNow;
            for (int i = 0; i < 2; i++)
            {
                Schedule due = (await server.Context.Database.Schedules.ReadAsync(s.TenantId, s.Id, token).ConfigureAwait(false))!;
                due.NextRunUtc = now.AddMinutes(-1);
                await server.Context.Database.Schedules.UpdateAsync(due, token).ConfigureAwait(false);
                await server.Context.Scheduling.TickAsync(now, token).ConfigureAwait(false);
            }

            List<Run> runs = (await server.Context.Database.Runs.EnumerateAsync(new MyMorningNews.Core.Requests.RunFilter { TenantId = topic.TenantId, TopicId = topic.Id, PageSize = 10 }, token).ConfigureAwait(false)).Items;
            Assert.True(runs.Count == 2 && runs.All(r => r.Status == RunStatusEnum.Skipped && r.SkippedReason!.StartsWith("CostCapReached", StringComparison.Ordinal)), "skipped with the reason: " + String.Join(" | ", runs.Select(r => r.Status + " " + r.SkippedReason)));
            List<EmailMessage> mail = (await server.Context.Database.EmailMessages.EnumerateAsync(new MyMorningNews.Core.Requests.EmailMessageFilter { TenantId = topic.TenantId, PageSize = 50 }, token).ConfigureAwait(false)).Items;
            Assert.Equal(1, mail.Count(m => m.Template == EmailTemplateEnum.CapReached), "one notice this month");
        }

        private static async Task DailyRunsAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            TestUser u = await FreeUserAsync(admin, token).ConfigureAwait(false);
            Topic topic = await TopicAsync(u, token).ConfigureAwait(false);
            for (int i = 0; i < 2; i++)
            {
                await server.Context.Database.Runs.CreateAsync(new Run
                {
                    TenantId = u.User.TenantId, TopicId = topic.Id, UserId = u.User.Id, Trigger = RunTriggerEnum.Manual, Status = RunStatusEnum.Completed, QueuedUtc = DateTime.UtcNow, CreatedUtc = DateTime.UtcNow
                }, token).ConfigureAwait(false);
            }

            ExpectCap(await u.Client.PostAsync("/v1.0/api/topics/" + topic.Id + "/run", null, token).ConfigureAwait(false), "runs.daily.max", "third run today");
        }

        private static async Task ScheduleAndEndpointsAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            TestUser u = await FreeUserAsync(admin, token, true).ConfigureAwait(false);
            Topic topic = await TopicAsync(u, token).ConfigureAwait(false);
            TestResponse hourly = (await u.Client.PostAsync("/v1.0/api/schedules", new { ownerId = topic.Id, intervalType = "EveryNMinutes", intervalValue = 60 }, token).ConfigureAwait(false)).Expect(400, "hourly refused on Free");
            Assert.True(hourly.Body.Contains("1440", StringComparison.Ordinal), "names the plan minimum: " + hourly.Body);
            (await u.Client.PostAsync("/v1.0/api/schedules", new { ownerId = topic.Id, intervalType = "DaysOfWeek", daysOfWeekMask = 127, timesOfDay = new[] { "06:30", "18:30" }, timeZone = "UTC" }, token).ConfigureAwait(false)).Expect(400, "twice a day refused on Free");
            (await u.Client.PostAsync("/v1.0/api/schedules", new { ownerId = topic.Id, intervalType = "DaysOfWeek", daysOfWeekMask = 127, timesOfDay = new[] { "06:30" }, timeZone = "UTC" }, token).ConfigureAwait(false)).Expect(201, "daily allowed");
            ExpectCap(await u.Client.PostAsync("/v1.0/api/model-endpoints", new { name = "Mine", provider = "Ollama", model = "llama3", endpoint = "http://127.0.0.1:12120" }, token).ConfigureAwait(false), "endpoints.custom", "custom endpoint");
        }

        private static async Task MeterAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            TestUser u = await FreeUserAsync(admin, token).ConfigureAwait(false);
            await TopicAsync(u, token).ConfigureAwait(false);
            UsageMeter meter = (await u.Client.GetAsync("/v1.0/api/account/usage", token).ConfigureAwait(false)).Expect(200, "meter").Json<UsageMeter>();
            Assert.True(meter.PlanKey == "free" && meter.Topics == 1 && meter.TopicsMax == 1 && meter.RunsDailyMax == 2 && meter.ScheduleMinIntervalMinutes == 1440 && meter.AudioEnabled && meter.AudioMinutesMax == 10 && !meter.CustomEndpoints && meter.MonthToDateCostUsd == 0,
                "free plan meter");
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser plain = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            UsageMeter open = (await plain.Client.GetAsync("/v1.0/api/account/usage", token).ConfigureAwait(false)).Expect(200, "meter").Json<UsageMeter>();
            Assert.True(open.PlanKey == "unlimited" && open.TopicsMax == null && open.MonthlyCostCapUsd == null && open.CustomEndpoints, "self-hosted default has no caps");
        }
    }
}
