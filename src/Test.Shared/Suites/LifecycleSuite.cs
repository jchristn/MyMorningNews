namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Analytics;
    using MyMorningNews.Core.Entitlements;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Server;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Stale topic reviews, the administrator daily summary, system briefings made from a topic, replacing a user's
    /// plan, and the welcome email for administrator-created accounts.
    /// </summary>
    public static class LifecycleSuite
    {
        private const string S = "Lifecycle";
        private const string R = "/v1.0/api/topic-reviews";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Stale topic reviews, administrator summary, system briefings from topics, plan replacement, and welcome emails", new[]
            {
                Cases.Case(S, "StalePrompt", "A recurring topic with runs but no new findings for a week is reviewed once, by email and push, and listed only for its owner", StalePromptAsync),
                Cases.Case(S, "StaleKeep", "Keep closes the review and holds off a new prompt", StaleKeepAsync),
                Cases.Case(S, "StaleRecover", "New findings before the deadline close the review without deleting anything", StaleRecoverAsync),
                Cases.Case(S, "StaleAutoDelete", "Without Keep a reminder goes out before the deadline and the topic is deleted after it, with a notice", StaleAutoDeleteAsync),
                Cases.Case(S, "StaleDelete", "Delete from the prompt removes the topic now; fresh and one-time topics are never reviewed", StaleDeleteAsync),
                Cases.Case(S, "AdminSummary", "Activity is recorded per user per day, statistics are global-admin only, and the summary is sent once per day and address", AdminSummaryAsync),
                Cases.Case(S, "PromoteTopic", "A global administrator makes a topic system-wide: the setup is copied to the catalog and the topic is untouched", PromoteTopicAsync),
                Cases.Case(S, "ReplacePlan", "Granting with replaceCurrent revokes the earlier administrator plan so the new one applies", ReplacePlanAsync),
                Cases.Case(S, "AdminWelcome", "An account created by an administrator gets one welcome email with a password link unless the admin opts out", AdminWelcomeAsync)
            });
        }

        private static async Task<Topic> RecurringTopicAsync(MyMorningNewsServer server, TestUser user, string name, CancellationToken token)
        {
            Topic topic = (await user.Client.PostAsync("/v1.0/api/topics", new { name = name }, token).ConfigureAwait(false)).Expect(201, "topic").Json<Topic>();
            topic.Brief = "Follow " + name + ".";
            topic.Status = TopicStatusEnum.Active;
            return await server.Context.Database.Topics.UpdateAsync(topic, token).ConfigureAwait(false);
        }

        private static async Task RunAtAsync(MyMorningNewsServer server, Topic topic, DateTime completedUtc, int newFindings, CancellationToken token)
        {
            await server.Context.Database.Runs.CreateAsync(new Run
            {
                TenantId = topic.TenantId, TopicId = topic.Id, UserId = topic.UserId, Status = RunStatusEnum.Completed, Trigger = RunTriggerEnum.Schedule,
                QueuedUtc = completedUtc.AddMinutes(-5), StartedUtc = completedUtc.AddMinutes(-4), CompletedUtc = completedUtc, NewFindingCount = newFindings
            }, token).ConfigureAwait(false);
        }

        private static async Task<TopicReview?> PendingAsync(MyMorningNewsServer server, Topic topic, CancellationToken token)
        {
            return (await server.Context.Database.TopicReviews.EnumerateAsync(new TopicReviewFilter { TenantId = topic.TenantId, TopicId = topic.Id, Status = TopicReviewStatusEnum.Pending }, token).ConfigureAwait(false)).Objects.FirstOrDefault();
        }

        private static async Task<TopicReview> OnlyReviewAsync(MyMorningNewsServer server, Topic topic, CancellationToken token)
        {
            List<TopicReview> all = (await server.Context.Database.TopicReviews.EnumerateAsync(new TopicReviewFilter { TenantId = topic.TenantId, TopicId = topic.Id }, token).ConfigureAwait(false)).Objects;
            Assert.Equal(1, all.Count, "one review for the topic");
            return all[0];
        }

        private static async Task<TestUser> UserAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            return await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
        }

        private static async Task StalePromptAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            server.Context.SettingsService.Current.Push.ExpoPushUrl = StubModelServer.Shared.BaseUrl + "/push/send";
            TestUser owner = await UserAsync(token).ConfigureAwait(false);
            TestUser other = await UserAsync(token).ConfigureAwait(false);
            string pushToken = "ExponentPushToken[stale-" + Guid.NewGuid().ToString("N").Substring(0, 12) + "]";
            (await owner.Client.PostAsync("/v1.0/api/devices", new DeviceRegisterRequest { ExpoPushToken = pushToken, Platform = "ios" }, token).ConfigureAwait(false)).Expect(201, "device");

            DateTime now = DateTime.UtcNow;
            Topic topic = await RecurringTopicAsync(server, owner, "Quiet topic " + Guid.NewGuid().ToString("N").Substring(0, 6), token).ConfigureAwait(false);
            await RunAtAsync(server, topic, now.AddDays(2), 0, token).ConfigureAwait(false);
            int pushesBefore = StubModelServer.Shared.PushRequests.Count;

            TopicLifecycleResult first = await server.Context.TopicLifecycle.RunOnceAsync(now.AddDays(8), token).ConfigureAwait(false);
            Assert.True(first.Prompted >= 1, "the quiet topic is prompted");
            TopicReview review = Assert.NotNull(await PendingAsync(server, topic, token).ConfigureAwait(false), "pending review");
            Assert.True(Math.Abs((review.DeleteAfterUtc - now.AddDays(8 + server.Context.Settings.TopicLifecycle.GraceDays)).TotalMinutes) < 1, "deadline is the grace period after the prompt");

            EmailMessage email = Assert.NotNull(await server.Context.Database.EmailMessages.ReadByKeyAsync(topic.TenantId, "topicreview:" + review.Id + ":prompt:" + owner.User.Email.ToLowerInvariant(), token).ConfigureAwait(false), "prompt email");
            Assert.Equal(EmailTemplateEnum.TopicStale, email.Template, "prompt template");
            Assert.True(StubModelServer.Shared.PushRequests.Skip(pushesBefore).Any(p => p.Contains(pushToken, StringComparison.Ordinal) && p.Contains("topic.stale", StringComparison.Ordinal)), "prompt pushed to the first device by default");

            await server.Context.TopicLifecycle.RunOnceAsync(now.AddDays(8).AddHours(1), token).ConfigureAwait(false);
            await OnlyReviewAsync(server, topic, token).ConfigureAwait(false);

            EnumerationResult<TopicReviewResponse> mine = (await owner.Client.GetAsync(R, token).ConfigureAwait(false)).Expect(200, "list").Json<EnumerationResult<TopicReviewResponse>>();
            TopicReviewResponse listed = Assert.NotNull(mine.Objects.FirstOrDefault(r => r.Id == review.Id), "listed for the owner");
            Assert.Equal(topic.Name, listed.TopicName, "topic name in the list");
            Assert.True(!(await other.Client.GetAsync(R, token).ConfigureAwait(false)).Expect(200, "other list").Json<EnumerationResult<TopicReviewResponse>>().Objects.Any(r => r.Id == review.Id), "not listed for another user");
            (await other.Client.PostAsync(R + "/" + review.Id + "/keep", null, token).ConfigureAwait(false)).Expect(404, "another user cannot answer");
        }

        private static async Task StaleKeepAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TestUser owner = await UserAsync(token).ConfigureAwait(false);
            DateTime now = DateTime.UtcNow;
            Topic topic = await RecurringTopicAsync(server, owner, "Kept topic", token).ConfigureAwait(false);
            await RunAtAsync(server, topic, now.AddDays(2), 0, token).ConfigureAwait(false);
            await server.Context.TopicLifecycle.RunOnceAsync(now.AddDays(8), token).ConfigureAwait(false);
            TopicReview review = Assert.NotNull(await PendingAsync(server, topic, token).ConfigureAwait(false), "pending review");

            TopicReviewResponse kept = (await owner.Client.PostAsync(R + "/" + review.Id + "/keep", null, token).ConfigureAwait(false)).Expect(200, "keep").Json<TopicReviewResponse>();
            Assert.Equal(TopicReviewStatusEnum.Kept, kept.Status, "kept");
            (await owner.Client.PostAsync(R + "/" + review.Id + "/keep", null, token).ConfigureAwait(false)).Expect(200, "keep is idempotent");
            (await owner.Client.PostAsync(R + "/" + review.Id + "/delete", null, token).ConfigureAwait(false)).Expect(409, "a kept review cannot delete");

            await server.Context.TopicLifecycle.RunOnceAsync(now.AddDays(1), token).ConfigureAwait(false);
            Assert.True(await PendingAsync(server, topic, token).ConfigureAwait(false) == null, "no new prompt inside the keep window");
            Assert.NotNull(await server.Context.Database.Topics.ReadAsync(topic.TenantId, topic.Id, token).ConfigureAwait(false), "topic kept");
        }

        private static async Task StaleRecoverAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TestUser owner = await UserAsync(token).ConfigureAwait(false);
            DateTime now = DateTime.UtcNow;
            Topic topic = await RecurringTopicAsync(server, owner, "Recovering topic", token).ConfigureAwait(false);
            await RunAtAsync(server, topic, now.AddDays(2), 0, token).ConfigureAwait(false);
            await server.Context.TopicLifecycle.RunOnceAsync(now.AddDays(8), token).ConfigureAwait(false);
            Assert.NotNull(await PendingAsync(server, topic, token).ConfigureAwait(false), "pending review");

            await RunAtAsync(server, topic, now.AddDays(9), 3, token).ConfigureAwait(false);
            TopicLifecycleResult pass = await server.Context.TopicLifecycle.RunOnceAsync(now.AddDays(10), token).ConfigureAwait(false);
            Assert.True(pass.Recovered >= 1, "recovered");
            Assert.Equal(TopicReviewStatusEnum.Recovered, (await OnlyReviewAsync(server, topic, token).ConfigureAwait(false)).Status, "review closed as recovered");
            await server.Context.TopicLifecycle.RunOnceAsync(now.AddDays(40), token).ConfigureAwait(false);
            Assert.NotNull(await server.Context.Database.Topics.ReadAsync(topic.TenantId, topic.Id, token).ConfigureAwait(false), "never deleted");
        }

        private static async Task StaleAutoDeleteAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TestUser owner = await UserAsync(token).ConfigureAwait(false);
            DateTime now = DateTime.UtcNow;
            int grace = server.Context.Settings.TopicLifecycle.GraceDays;
            int remind = server.Context.Settings.TopicLifecycle.ReminderDays;
            Topic topic = await RecurringTopicAsync(server, owner, "Forgotten topic", token).ConfigureAwait(false);
            await RunAtAsync(server, topic, now.AddDays(2), 0, token).ConfigureAwait(false);
            await server.Context.TopicLifecycle.RunOnceAsync(now.AddDays(8), token).ConfigureAwait(false);
            TopicReview review = Assert.NotNull(await PendingAsync(server, topic, token).ConfigureAwait(false), "pending review");
            string address = owner.User.Email.ToLowerInvariant();

            await server.Context.TopicLifecycle.RunOnceAsync(now.AddDays(8 + grace - remind - 1), token).ConfigureAwait(false);
            Assert.True(await server.Context.Database.EmailMessages.ReadByKeyAsync(topic.TenantId, "topicreview:" + review.Id + ":reminder:" + address, token).ConfigureAwait(false) == null, "no reminder yet");
            TopicLifecycleResult reminded = await server.Context.TopicLifecycle.RunOnceAsync(now.AddDays(8 + grace - remind).AddHours(1), token).ConfigureAwait(false);
            Assert.True(reminded.Reminded >= 1, "reminder sent");
            Assert.NotNull(await server.Context.Database.EmailMessages.ReadByKeyAsync(topic.TenantId, "topicreview:" + review.Id + ":reminder:" + address, token).ConfigureAwait(false), "reminder email");
            Assert.NotNull(await server.Context.Database.Topics.ReadAsync(topic.TenantId, topic.Id, token).ConfigureAwait(false), "still there before the deadline");

            TopicLifecycleResult deleted = await server.Context.TopicLifecycle.RunOnceAsync(now.AddDays(8 + grace).AddHours(1), token).ConfigureAwait(false);
            Assert.True(deleted.Deleted >= 1, "deleted");
            Assert.True(await server.Context.Database.Topics.ReadAsync(topic.TenantId, topic.Id, token).ConfigureAwait(false) == null, "topic gone");
            Assert.Equal(TopicReviewStatusEnum.AutoDeleted, (await OnlyReviewAsync(server, topic, token).ConfigureAwait(false)).Status, "review records the automatic deletion");
            EmailMessage notice = Assert.NotNull(await server.Context.Database.EmailMessages.ReadByKeyAsync(topic.TenantId, "topicreview:" + review.Id + ":deleted:" + address, token).ConfigureAwait(false), "deletion notice");
            Assert.Equal(EmailTemplateEnum.TopicDeleted, notice.Template, "deletion template");
        }

        private static async Task StaleDeleteAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TestUser owner = await UserAsync(token).ConfigureAwait(false);
            DateTime now = DateTime.UtcNow;
            Topic quiet = await RecurringTopicAsync(server, owner, "Deleted from the prompt", token).ConfigureAwait(false);
            await RunAtAsync(server, quiet, now.AddDays(2), 0, token).ConfigureAwait(false);
            Topic busy = await RecurringTopicAsync(server, owner, "Busy topic", token).ConfigureAwait(false);
            await RunAtAsync(server, busy, now.AddDays(2), 0, token).ConfigureAwait(false);
            await RunAtAsync(server, busy, now.AddDays(6), 2, token).ConfigureAwait(false);
            Topic once = await RecurringTopicAsync(server, owner, "One-time report", token).ConfigureAwait(false);
            once.IsOneTime = true;
            await server.Context.Database.Topics.UpdateAsync(once, token).ConfigureAwait(false);
            await RunAtAsync(server, once, now.AddDays(2), 0, token).ConfigureAwait(false);

            await server.Context.TopicLifecycle.RunOnceAsync(now.AddDays(8), token).ConfigureAwait(false);
            Assert.True(await PendingAsync(server, busy, token).ConfigureAwait(false) == null, "a topic with new findings in the window is not reviewed");
            Assert.True(await PendingAsync(server, once, token).ConfigureAwait(false) == null, "a one-time topic is not reviewed");
            TopicReview review = Assert.NotNull(await PendingAsync(server, quiet, token).ConfigureAwait(false), "pending review");

            (await owner.Client.PostAsync(R + "/" + review.Id + "/delete", null, token).ConfigureAwait(false)).Expect(204, "delete from the prompt");
            Assert.True(await server.Context.Database.Topics.ReadAsync(quiet.TenantId, quiet.Id, token).ConfigureAwait(false) == null, "topic deleted");
            Assert.Equal(TopicReviewStatusEnum.Deleted, (await OnlyReviewAsync(server, quiet, token).ConfigureAwait(false)).Status, "review closed as deleted");
            (await owner.Client.PostAsync(R + "/" + review.Id + "/keep", null, token).ConfigureAwait(false)).Expect(409, "cannot keep a deleted topic");

            server.Context.SettingsService.Current.TopicLifecycle.Enabled = false;
            try
            {
                Topic off = await RecurringTopicAsync(server, owner, "Feature off", token).ConfigureAwait(false);
                await RunAtAsync(server, off, now.AddDays(2), 0, token).ConfigureAwait(false);
                TopicLifecycleResult none = await server.Context.TopicLifecycle.RunOnceAsync(now.AddDays(8), token).ConfigureAwait(false);
                Assert.Equal(0, none.Prompted, "nothing happens when the feature is off");
            }
            finally
            {
                server.Context.SettingsService.Current.TopicLifecycle.Enabled = true;
            }
        }

        private static async Task AdminSummaryAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            TestUser member = await UserAsync(token).ConfigureAwait(false);
            (await member.Client.GetAsync("/v1.0/api/topics", token).ConfigureAwait(false)).Expect(200, "member request");

            DateTime today = DateTime.UtcNow.Date;
            PlatformDailyStats stats = await server.Context.Database.PlatformStats.ReadAsync(today, today.AddDays(1), token).ConfigureAwait(false);
            Assert.True(stats.DailyActiveUsers >= 1 && stats.MonthlyActiveUsers >= stats.DailyActiveUsers, "an authenticated request counts as activity");
            Assert.True(stats.TotalUsers >= 1 && stats.NewUsers >= 1, "user counts");

            (await member.Client.GetAsync("/v1.0/api/admin/reports/daily?day=" + today.ToString("yyyy-MM-dd"), token).ConfigureAwait(false)).Expect(403, "members cannot read platform statistics");
            PlatformDailyStats api = (await admin.GetAsync("/v1.0/api/admin/reports/daily?day=" + today.ToString("yyyy-MM-dd"), token).ConfigureAwait(false)).Expect(200, "admin statistics").Json<PlatformDailyStats>();
            Assert.True(api.DailyActiveUsers >= 1, "statistics over the API");

            Assert.True(server.Context.Settings.AdminReports.Recipients.SequenceEqual(new[] { "hello@latticetech.ai" }), "default recipient");
            string a = "ops-" + Guid.NewGuid().ToString("N").Substring(0, 8) + "@example.test";
            string b = "Founder-" + Guid.NewGuid().ToString("N").Substring(0, 8) + "@Example.test";
            DateTime day = today.AddDays(-400);
            int sent = await server.Context.AdminReports.SendAsync(day, new List<string> { a, b, " " }, token).ConfigureAwait(false);
            Assert.Equal(2, sent, "one summary per address");
            Assert.Equal(0, await server.Context.AdminReports.SendAsync(day, new List<string> { a, b }, token).ConfigureAwait(false), "never twice for the same day");
            EmailMessage m = Assert.NotNull(await server.Context.Database.EmailMessages.ReadByKeyAsync("default", "adminsummary:" + day.ToString("yyyyMMdd") + ":" + b.ToLowerInvariant(), token).ConfigureAwait(false), "summary email");
            Assert.Equal(EmailTemplateEnum.AdminDailySummary, m.Template, "summary template");
            Assert.True((m.RenderedHtml ?? String.Empty).Contains("Daily active users", StringComparison.Ordinal) && (m.RenderedText ?? String.Empty).Contains("Model cost", StringComparison.Ordinal), "summary shows users and model cost");

            Dictionary<string, object> viaApi = (await admin.PostAsync("/v1.0/api/admin/reports/daily/send", new AdminReportSendRequest { DayUtc = today.AddDays(-401), Recipients = new List<string> { a } }, token).ConfigureAwait(false))
                .Expect(200, "send now").Json<Dictionary<string, object>>();
            Assert.Equal("1", viaApi["queued"].ToString(), "sent through the API");
            Assert.Equal(0, await server.Context.AdminReports.SendDueAsync(today.AddHours(server.Context.Settings.AdminReports.HourUtc).AddMinutes(-1), token).ConfigureAwait(false), "nothing before the configured hour");
        }

        private static async Task PromoteTopicAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            TestUser member = await UserAsync(token).ConfigureAwait(false);
            Topic source = (await admin.PostAsync("/v1.0/api/topics", new { name = "Admin topic " + Guid.NewGuid().ToString("N").Substring(0, 6) }, token).ConfigureAwait(false)).Expect(201, "admin topic").Json<Topic>();
            object body = new { title = "Shared " + source.Name, description = "For everyone", category = "Technology", language = "en", freeEligible = true, status = "Published" };
            (await admin.PostAsync("/v1.0/api/admin/catalog/from-topic/" + source.Id, body, token).ConfigureAwait(false)).Expect(400, "a topic without a scope is refused");

            source.Brief = "Track new open-weight model releases.";
            source.Status = TopicStatusEnum.Active;
            source.SeedQueries = new List<string> { "open weight model release" };
            source = await server.Context.Database.Topics.UpdateAsync(source, token).ConfigureAwait(false);
            await server.Context.Database.TopicDimensions.CreateAsync(new TopicDimension { TenantId = source.TenantId, TopicId = source.Id, Name = "Releases", Weight = DimensionWeightEnum.High }, token).ConfigureAwait(false);

            (await member.Client.PostAsync("/v1.0/api/admin/catalog/from-topic/" + source.Id, body, token).ConfigureAwait(false)).Expect(403, "members cannot publish to the catalog");
            CatalogPublication publication = (await admin.PostAsync("/v1.0/api/admin/catalog/from-topic/" + source.Id, body, token).ConfigureAwait(false)).Expect(201, "promote").Json<CatalogPublication>();
            User owner = await server.Context.Database.CatalogPublications.EnsureOwnerAsync(token).ConfigureAwait(false);
            Assert.True(publication.OwnerTenantId == owner.TenantId && publication.TopicId != source.Id && publication.Status == "Published" && publication.PublishedUtc.HasValue, "a published copy owned by the catalog");
            Topic copy = Assert.NotNull(await server.Context.Database.Topics.ReadAsync(owner.TenantId, publication.TopicId, token).ConfigureAwait(false), "canonical topic");
            Assert.True(copy.Brief == source.Brief && copy.SeedQueries.SequenceEqual(source.SeedQueries) && copy.Status == TopicStatusEnum.Active, "scope and seeds copied");
            List<TopicDimension> dims = (await server.Context.Database.TopicDimensions.EnumerateAsync(new TopicDimensionFilter { TenantId = owner.TenantId, TopicId = copy.Id }, token).ConfigureAwait(false)).Objects;
            Assert.True(dims.Any(d => d.Name == "Releases"), "dimensions copied");
            Topic unchanged = Assert.NotNull(await server.Context.Database.Topics.ReadAsync(source.TenantId, source.Id, token).ConfigureAwait(false), "source topic");
            Assert.True(unchanged.UserId == source.UserId && unchanged.TenantId == source.TenantId, "source untouched");
            (await admin.PostAsync("/v1.0/api/admin/catalog/from-topic/" + "tpc_missing", body, token).ConfigureAwait(false)).Expect(404, "unknown topic");
        }

        private static async Task ReplacePlanAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            TestUser member = await UserAsync(token).ConfigureAwait(false);
            List<PlanResponse> plans = (await admin.GetAsync("/v1.0/api/admin/plans?pageSize=100", token).ConfigureAwait(false)).Expect(200, "plans").Json<EnumerationResult<PlanResponse>>().Objects;
            List<PlanResponse> active = plans.Where(p => p.Plan.Active).ToList();
            Assert.True(active.Count >= 2, "at least two plans");
            string first = active[0].Plan.Id;
            string second = active[1].Plan.Id;
            object grant1 = new { tenantId = member.User.TenantId, userId = member.User.Id, planId = first, durationDays = 0, note = "first" };
            (await admin.PostAsync("/v1.0/api/admin/plan-assignments", grant1, token).ConfigureAwait(false)).Expect(201, "first plan");
            object grant2 = new { tenantId = member.User.TenantId, userId = member.User.Id, planId = second, durationDays = 30, note = "second", replaceCurrent = true };
            PlanAssignment replacement = (await admin.PostAsync("/v1.0/api/admin/plan-assignments", grant2, token).ConfigureAwait(false)).Expect(201, "replacement").Json<PlanAssignment>();

            List<PlanAssignment> history = (await admin.GetAsync("/v1.0/api/admin/plan-assignments?tenantId=" + member.User.TenantId + "&userId=" + member.User.Id, token).ConfigureAwait(false))
                .Expect(200, "assignments").Json<EnumerationResult<PlanAssignment>>().Objects;
            Assert.True(history.Single(a => a.PlanId == first && a.Source == PlanSourceEnum.Admin).RevokedUtc.HasValue, "the earlier administrator plan is revoked");
            Assert.True(!history.Single(a => a.Id == replacement.Id).RevokedUtc.HasValue && replacement.EndsUtc.HasValue, "the new plan is live with its end date");
            EffectivePlan effective = (await admin.GetAsync("/v1.0/tenants/" + member.User.TenantId + "/users/" + member.User.Id + "/entitlements", token).ConfigureAwait(false)).Expect(200, "effective").Json<EffectivePlan>();
            Assert.Equal(second, effective.Plan.Id, "the replacement applies");
        }

        private static async Task AdminWelcomeAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            string email = "welcome-" + Guid.NewGuid().ToString("N").Substring(0, 8) + "@example.test";
            UserResponse created = (await admin.PostAsync("/v1.0/tenants/" + t.Id + "/users", new UserCreateRequest { Email = email, Password = "test-password-1" }, token).ConfigureAwait(false)).Expect(201, "create").Json<UserResponse>();
            EmailMessage welcome = Assert.NotNull(await server.Context.Database.EmailMessages.ReadByKeyAsync(t.Id, "welcome:" + created.Id, token).ConfigureAwait(false), "welcome email");
            Assert.Equal(EmailTemplateEnum.AdminWelcome, welcome.Template, "admin welcome template");
            Assert.True((welcome.RenderedHtml ?? String.Empty).Contains("/reset-password?token=", StringComparison.Ordinal), "a link to choose a password");

            string quiet = "quiet-" + Guid.NewGuid().ToString("N").Substring(0, 8) + "@example.test";
            UserResponse silent = (await admin.PostAsync("/v1.0/tenants/" + t.Id + "/users", new UserCreateRequest { Email = quiet, Password = "test-password-1", SendWelcomeEmail = false }, token).ConfigureAwait(false)).Expect(201, "create quietly").Json<UserResponse>();
            Assert.True(await server.Context.Database.EmailMessages.ReadByKeyAsync(t.Id, "welcome:" + silent.Id, token).ConfigureAwait(false) == null, "no welcome when the admin opts out");
        }
    }
}
