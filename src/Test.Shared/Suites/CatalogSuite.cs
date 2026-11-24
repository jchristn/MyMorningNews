namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Audio;
    using MyMorningNews.Core.Catalog;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Storage;
    using MyMorningNews.Server;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>Catalog isolation, concurrency, and publication lifecycle integration tests.</summary>
    public static class CatalogSuite
    {
        private const string S = "Catalog";

        /// <summary>Build the suite.</summary>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Shared system briefing catalog", new[]
            {
                Cases.Case(S, "Isolation", "Only published subscribed content crosses tenants; retirement and retraction are enforced", IsolationAsync),
                Cases.Case(S, "Research", "System research binds schedules to the protected canonical owner and rejects tenant administrators", ResearchAsync),
                Cases.Case(S, "Feeds", "Private feeds include subscribed shared audio and recheck downgrade, unsubscribe, retirement and retraction on every enclosure", FeedsAsync),
                Cases.Case(S, "Delivery", "Shared issue fanout is private and idempotent, resumes interrupted email rendering, and rechecks unsubscribe before sending", DeliveryAsync),
                Cases.Case(S, "Concurrency", "Concurrent subscriptions cannot exceed five slots and retries are idempotent", ConcurrencyAsync)
            });
        }

        private static async Task<CatalogPublication> PublishAsync(TestApiClient admin, bool free, CancellationToken token)
        {
            return (await admin.PostAsync("/v1.0/api/admin/catalog", new { title = "Public research " + Guid.NewGuid().ToString("N"), description = "Public scope", freeEligible = free, status = "Published" }, token).ConfigureAwait(false)).Expect(201, "publish").Json<CatalogPublication>();
        }

        private static async Task IsolationAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            Tenant tenant = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser member = await TestFixtures.CreateUserAsync(admin, tenant.Id, true, token).ConfigureAwait(false);
            TestUser other = await TestFixtures.CreateUserAsync(admin, tenant.Id, false, token).ConfigureAwait(false);
            (await member.Client.PostAsync("/v1.0/api/admin/catalog", new { title = "Unauthorized" }, token).ConfigureAwait(false)).Expect(403, "tenant admin cannot publish");
            CatalogPublication publication = await PublishAsync(admin, true, token).ConfigureAwait(false);
            User owner = await server.Context.Database.CatalogPublications.EnsureOwnerAsync(token).ConfigureAwait(false);
            Assert.True(publication.OwnerTenantId == owner.TenantId && owner.IsProtected && !owner.IsAdmin, "protected non-admin canonical owner");
            Topic topic = Assert.NotNull(await server.Context.Database.Topics.ReadAsync(owner.TenantId, publication.TopicId, token).ConfigureAwait(false), "canonical topic");
            Briefing issue = await server.Context.Database.Briefings.CreateAsync(new Briefing { TenantId = owner.TenantId, UserId = owner.Id, TopicId = topic.Id, Title = "Published issue", Markdown = "Public evidence", Status = BriefingStatusEnum.Ready }, token).ConfigureAwait(false);
            Briefing dry = await server.Context.Database.Briefings.CreateAsync(new Briefing { TenantId = owner.TenantId, UserId = owner.Id, TopicId = topic.Id, Title = "Private preview", Markdown = "Secret preview", Status = BriefingStatusEnum.Ready, IsDryRun = true }, token).ConfigureAwait(false);
            string path = "/v1.0/api/catalog/" + publication.Id;
            string issuePath = path + "/issues/" + issue.Id;
            (await member.Client.GetAsync(issuePath, token).ConfigureAwait(false)).Expect(404, "subscription required");
            (await member.Client.PutAsync(path + "/subscription", new { }, token).ConfigureAwait(false)).Expect(200, "subscribe");
            TestResponse read = (await member.Client.GetAsync(issuePath, token).ConfigureAwait(false)).Expect(200, "read published issue");
            Assert.Equal("Public evidence", read.Json<CatalogIssueResponse>().Markdown, "public content");
            Assert.True(!read.Body.Contains(owner.TenantId, StringComparison.Ordinal) && !read.Body.Contains(owner.Id, StringComparison.Ordinal) && !read.Body.Contains("topicId", StringComparison.Ordinal), "projection excludes canonical identifiers");
            (await other.Client.GetAsync(issuePath, token).ConfigureAwait(false)).Expect(404, "another member cannot use subscription");
            (await member.Client.PutAsync(issuePath + "/progress", new { positionSeconds = 42, completed = false }, token).ConfigureAwait(false)).Expect(200, "save private progress");
            (await other.Client.PutAsync(path + "/subscription", new { }, token).ConfigureAwait(false)).Expect(200, "second member subscribes");
            BriefingProgress otherProgress = (await other.Client.GetAsync(issuePath + "/progress", token).ConfigureAwait(false)).Expect(200, "second progress").Json<BriefingProgress>();
            Assert.Equal(0d, otherProgress.PositionSeconds, "listening progress is not shared");
            BriefingProgress ownProgress = (await member.Client.GetAsync(issuePath + "/progress", token).ConfigureAwait(false)).Expect(200, "own progress").Json<BriefingProgress>();
            Assert.True(ownProgress.PositionSeconds == 42 && ownProgress.TenantId == tenant.Id, "progress stays in subscriber tenant");
            (await member.Client.GetAsync("/v1.0/api/briefings/" + issue.Id, token).ConfigureAwait(false)).Expect(404, "ordinary tenant route remains isolated");
            (await member.Client.GetAsync(path + "/issues/" + dry.Id, token).ConfigureAwait(false)).Expect(404, "dry run excluded");
            EnumerationResult<CatalogEntryResponse> catalog = (await member.Client.GetAsync("/v1.0/api/catalog", token).ConfigureAwait(false)).Expect(200, "catalog").Json<EnumerationResult<CatalogEntryResponse>>();
            Assert.True(catalog.Objects.Any(x => x.Id == publication.Id && x.Subscribed), "subscription reflected");
            publication = (await admin.PutAsync("/v1.0/api/admin/catalog/" + publication.Id, new { title = publication.Title, status = "Retired", freeEligible = true, revision = publication.Revision }, token).ConfigureAwait(false)).Expect(200, "retire").Json<CatalogPublication>();
            (await member.Client.GetAsync(issuePath, token).ConfigureAwait(false)).Expect(200, "retired issue retained");
            (await admin.PutAsync("/v1.0/api/admin/catalog/" + publication.Id, new { title = publication.Title, status = "Published", revision = 1 }, token).ConfigureAwait(false)).Expect(409, "stale edit blocked");
            (await admin.PutAsync("/v1.0/api/admin/catalog/" + publication.Id, new { title = publication.Title, status = "Retracted", revision = publication.Revision }, token).ConfigureAwait(false)).Expect(200, "retract");
            (await member.Client.GetAsync(issuePath, token).ConfigureAwait(false)).Expect(404, "retracted content hidden");
        }

        private static async Task ResearchAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            Tenant tenant = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser member = await TestFixtures.CreateUserAsync(admin, tenant.Id, true, token).ConfigureAwait(false);
            CatalogPublication publication = await PublishAsync(admin, true, token).ConfigureAwait(false);
            string path = "/v1.0/api/admin/catalog/" + publication.Id + "/research";
            (await member.Client.GetAsync(path, token).ConfigureAwait(false)).Expect(403, "tenant administrator cannot inspect canonical settings");
            (await admin.PutAsync(path, new { topic = new { brief = "", status = "Active" } }, token).ConfigureAwait(false)).Expect(400, "active research requires scope");
            CatalogResearchResponse research = (await admin.PutAsync(path, new
            {
                topic = new { brief = "Follow publicly documented energy research.", status = "Active", historyMode = "PreviousIssue", audioEnabled = true },
                schedule = new { ownerId = "attempted-owner-substitution", ownerType = "Edition", intervalType = "EveryNDays", intervalValue = 1, timesOfDay = new[] { "08:00" }, timeZone = "UTC", enabled = true }
            }, token).ConfigureAwait(false)).Expect(200, "save canonical research").Json<CatalogResearchResponse>();
            Assert.True(research.Topic.Id == publication.TopicId && research.Topic.TenantId == publication.OwnerTenantId && research.Topic.HistoryMode == HistoryModeEnum.PreviousIssue, "canonical settings saved");
            Assert.True(research.Schedule?.OwnerId == publication.TopicId && research.Schedule.OwnerType == ScheduleOwnerTypeEnum.Topic && research.Schedule.TenantId == publication.OwnerTenantId, "schedule cannot redirect ownership");
            (await admin.PutAsync("/v1.0/api/admin/catalog/" + publication.Id, new { title = publication.Title, status = "Retired", revision = publication.Revision }, token).ConfigureAwait(false)).Expect(200, "retire canonical research");
            Topic stored = Assert.NotNull(await server.Context.Database.Topics.ReadAsync(publication.OwnerTenantId, publication.TopicId, token).ConfigureAwait(false), "canonical topic");
            Assert.Equal(TopicStatusEnum.Paused, stored.Status, "retirement pauses future research");
        }

        private static async Task FeedsAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            Tenant tenant = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser member = await TestFixtures.CreateUserAsync(admin, tenant.Id, false, token).ConfigureAwait(false);
            CatalogPublication publication = await PublishAsync(admin, true, token).ConfigureAwait(false);
            User owner = await server.Context.Database.CatalogPublications.EnsureOwnerAsync(token).ConfigureAwait(false);
            Briefing issue = await server.Context.Database.Briefings.CreateAsync(new Briefing { TenantId = owner.TenantId, UserId = owner.Id, TopicId = publication.TopicId, Title = "Shared podcast", Markdown = "Public evidence", Status = BriefingStatusEnum.Ready }, token).ConfigureAwait(false);
            Artifact artifact = await server.Context.Artifacts.SaveAsync(new ArtifactWrite { TenantId = owner.TenantId, UserId = owner.Id, BriefingId = issue.Id,
                Kind = ArtifactKindEnum.Audio, FileName = "fixture.mp3", Extension = "mp3", ContentType = "audio/mpeg", Content = new byte[] { 65, 66, 67, 68 } }, token).ConfigureAwait(false);
            MediaAsset audio = await server.Context.Database.MediaAssets.CreateAsync(new MediaAsset { TenantId = owner.TenantId, UserId = owner.Id, BriefingId = issue.Id,
                Title = issue.Title, ArtifactId = artifact.Id, Status = MediaStatusEnum.Ready, ContentType = "audio/mpeg", Bytes = 4, DurationSeconds = 10 }, token).ConfigureAwait(false);
            FeedTokenCreated feed = await server.Context.Audio.CreateFeedTokenAsync(tenant.Id, member.User.Id, null, "Catalog feed", token).ConfigureAwait(false);
            string basePath = "/v1.0/public/feeds/" + feed.Secret;
            string episode = basePath + "/episodes/" + audio.Id + ".mp3";
            TestApiClient anonymous = new TestApiClient(member.Client.BaseUrl);
            (await anonymous.GetAsync(episode, token).ConfigureAwait(false)).Expect(404, "feed token alone grants no canonical access");
            string subscription = "/v1.0/api/catalog/" + publication.Id + "/subscription";
            (await member.Client.PutAsync(subscription, new { }, token).ConfigureAwait(false)).Expect(200, "subscribe");
            string xml = (await anonymous.GetAsync(basePath + "/podcast.xml", token).ConfigureAwait(false)).Expect(200, "feed").Body;
            Assert.True(xml.Contains(audio.Id, StringComparison.Ordinal) && !xml.Contains(owner.Id, StringComparison.Ordinal), "feed includes only authorized public audio projection");
            TestResponse range = (await anonymous.SendAsync(HttpMethod.Get, episode, null, new Dictionary<string, string> { { "Range", "bytes=1-2" } }, token).ConfigureAwait(false)).Expect(206, "shared audio byte range");
            Assert.Equal("BC", range.Body, "range bytes");
            string limits = "/v1.0/tenants/" + tenant.Id + "/users/" + member.User.Id + "/entitlements";
            (await admin.PutAsync(limits, new { featureKey = "systemSubscriptions.max", value = "0", reason = "Downgrade fixture" }, token).ConfigureAwait(false)).Expect(200, "downgrade");
            (await anonymous.GetAsync(episode, token).ConfigureAwait(false)).Expect(404, "downgraded feed cannot play cached enclosure URL");
            (await admin.DeleteAsync(limits + "/systemSubscriptions.max", token).ConfigureAwait(false)).Expect(204, "restore inherited slots");
            (await anonymous.GetAsync(episode, token).ConfigureAwait(false)).Expect(200, "upgrade restores retained subscription");
            (await admin.PutAsync(limits, new { featureKey = "catalog.audio.enabled", value = "false", reason = "Audio restriction fixture" }, token).ConfigureAwait(false)).Expect(200, "disable catalog audio");
            (await anonymous.GetAsync(episode, token).ConfigureAwait(false)).Expect(404, "audio override enforced by feeds");
            (await member.Client.GetAsync("/v1.0/api/catalog/" + publication.Id + "/issues/" + issue.Id + "/audio", token).ConfigureAwait(false)).Expect(403, "audio override enforced by authenticated API");
            (await admin.DeleteAsync(limits + "/catalog.audio.enabled", token).ConfigureAwait(false)).Expect(204, "restore audio");
            publication = (await admin.PutAsync("/v1.0/api/admin/catalog/" + publication.Id, new { title = publication.Title, status = "Retired", freeEligible = true, revision = publication.Revision }, token).ConfigureAwait(false)).Json<CatalogPublication>();
            (await anonymous.GetAsync(episode, token).ConfigureAwait(false)).Expect(200, "retired audio remains readable");
            (await member.Client.DeleteAsync(subscription, token).ConfigureAwait(false)).Expect(204, "unsubscribe");
            (await anonymous.GetAsync(episode, token).ConfigureAwait(false)).Expect(404, "unsubscribed cached enclosure denied");
            publication = (await admin.PutAsync("/v1.0/api/admin/catalog/" + publication.Id, new { title = publication.Title, status = "Published", freeEligible = true, revision = publication.Revision }, token).ConfigureAwait(false)).Json<CatalogPublication>();
            (await member.Client.PutAsync(subscription, new { }, token).ConfigureAwait(false)).Expect(200, "resubscribe");
            (await admin.PutAsync("/v1.0/api/admin/catalog/" + publication.Id, new { title = publication.Title, status = "Retracted", revision = publication.Revision }, token).ConfigureAwait(false)).Expect(200, "retract");
            (await anonymous.GetAsync(episode, token).ConfigureAwait(false)).Expect(404, "retraction revokes feed enclosures");
        }

        private static async Task DeliveryAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            Tenant tenant = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser first = await TestFixtures.CreateUserAsync(admin, tenant.Id, false, token).ConfigureAwait(false);
            TestUser second = await TestFixtures.CreateUserAsync(admin, tenant.Id, false, token).ConfigureAwait(false);
            CatalogPublication publication = await PublishAsync(admin, true, token).ConfigureAwait(false);
            User owner = await server.Context.Database.CatalogPublications.EnsureOwnerAsync(token).ConfigureAwait(false);
            Briefing history = await server.Context.Database.Briefings.CreateAsync(new Briefing { TenantId = owner.TenantId, UserId = owner.Id, TopicId = publication.TopicId, Title = "Historical issue", Markdown = "Earlier evidence", Status = BriefingStatusEnum.Ready }, token).ConfigureAwait(false);
            string subscription = "/v1.0/api/catalog/" + publication.Id + "/subscription";
            foreach (TestUser user in new[] { first, second })
            {
                (await user.Client.PutAsync(subscription, new { emailEnabled = true, pushEnabled = false }, token).ConfigureAwait(false)).Expect(200, "subscribe to delivery");
                await server.Context.Email.Preferences.SetAsync(Assert.NotNull(await server.Context.Database.Users.ReadAsync(tenant.Id, user.User.Id, token).ConfigureAwait(false), "subscriber"), NotificationKindEnum.BriefingReady, NotificationChannelEnum.InApp, true, token).ConfigureAwait(false);
            }
            Briefing issue = await server.Context.Database.Briefings.CreateAsync(new Briefing { TenantId = owner.TenantId, UserId = owner.Id, TopicId = publication.TopicId, Title = "New shared issue", Markdown = "Current evidence", Status = BriefingStatusEnum.Ready }, token).ConfigureAwait(false);
            List<CatalogDelivery> candidates = await server.Context.Database.CatalogDeliveries.CandidatesAsync(token).ConfigureAwait(false);
            Assert.True(!candidates.Any(c => c.BriefingId == history.Id), "subscribing does not email the historical archive");
            CatalogDelivery interrupted = candidates.Single(c => c.BriefingId == issue.Id && c.UserId == first.User.Id && c.Channel == "Email");
            await server.Context.Database.CatalogDeliveries.EnqueueAsync(interrupted, token).ConfigureAwait(false);
            await server.Context.Database.EmailMessages.CreateAsync(new EmailMessage { TenantId = tenant.Id, UserId = first.User.Id, BriefingId = issue.Id,
                Template = EmailTemplateEnum.BriefingPublished, Recipient = MyMorningNews.Core.Email.EmailService.Normalize(first.User.Email), Subject = "(rendering)", Status = EmailStatusEnum.Sending,
                IdempotencyKey = "catalog:" + interrupted.Id + ":" + MyMorningNews.Core.Email.EmailService.Normalize(first.User.Email) }, token).ConfigureAwait(false);
            CatalogDeliveryService delivery = new CatalogDeliveryService(server.Context.Database, server.Context.Settings, server.Context.Email, server.Context.Push);
            await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => delivery.ProcessAsync(token))).ConfigureAwait(false);
            await delivery.ProcessAsync(token).ConfigureAwait(false);
            List<EmailMessage> emails = (await server.Context.Database.EmailMessages.EnumerateAsync(new EmailMessageFilter { TenantId = tenant.Id, BriefingId = issue.Id }, token).ConfigureAwait(false)).Objects;
            Assert.Equal(2, emails.Count, "one email per subscriber after concurrent workers and replay");
            Assert.True(emails.All(e => e.Status == EmailStatusEnum.Queued && e.RenderedHtml!.Contains("/dashboard/catalog/" + publication.Id + "/issues/" + issue.Id, StringComparison.Ordinal)), "interrupted rendering resumes with subscriber reader links");
            Assert.True(emails.All(e => !e.RenderedHtml!.Contains(owner.Id, StringComparison.Ordinal)), "canonical identity never leaks into email");
            foreach (TestUser user in new[] { first, second })
            {
                List<Notification> notices = (await server.Context.Database.Notifications.EnumerateAsync(new NotificationFilter { TenantId = tenant.Id, UserId = user.User.Id }, token).ConfigureAwait(false)).Objects;
                Assert.Equal(1, notices.Count, "one private in-app notification");
                Assert.True(notices[0].CatalogPublicationId == publication.Id && notices[0].BriefingId == issue.Id, "subscriber-safe deep link identity");
            }
            (await second.Client.DeleteAsync(subscription, token).ConfigureAwait(false)).Expect(204, "unsubscribe before email worker sends");
            await server.Context.Email.DeliverDueAsync(DateTime.UtcNow, token).ConfigureAwait(false);
            emails = (await server.Context.Database.EmailMessages.EnumerateAsync(new EmailMessageFilter { TenantId = tenant.Id, BriefingId = issue.Id }, token).ConfigureAwait(false)).Objects;
            Assert.Equal(EmailStatusEnum.Sent, emails.Single(e => e.UserId == first.User.Id).Status, "subscribed recipient delivered to file provider");
            Assert.Equal(EmailStatusEnum.Suppressed, emails.Single(e => e.UserId == second.User.Id).Status, "queued message suppressed after unsubscribe");
        }

        private static async Task ConcurrencyAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant tenant = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser member = await TestFixtures.CreateUserAsync(admin, tenant.Id, false, token).ConfigureAwait(false);
            string limits = "/v1.0/tenants/" + tenant.Id + "/users/" + member.User.Id + "/entitlements";
            (await admin.PutAsync(limits, new { featureKey = "systemSubscriptions.max", value = "5", reason = "Five-slot concurrency fixture" }, token).ConfigureAwait(false)).Expect(200, "five slots");
            (await admin.PutAsync(limits, new { featureKey = "catalog.freeOnly", value = "true", reason = "Free catalog fixture" }, token).ConfigureAwait(false)).Expect(200, "free eligibility");
            List<CatalogPublication> publications = new List<CatalogPublication>();
            for (int i = 0; i < 10; i++) publications.Add(await PublishAsync(admin, true, token).ConfigureAwait(false));
            TestResponse[] replies = await Task.WhenAll(publications.Select(p => member.Client.PutAsync("/v1.0/api/catalog/" + p.Id + "/subscription", new { }, token))).ConfigureAwait(false);
            Assert.Equal(5, replies.Count(r => r.StatusCode == 200), "exactly five accepted");
            Assert.Equal(5, replies.Count(r => r.StatusCode == 402), "other five capped");
            CatalogSubscription subscription = replies.First(r => r.StatusCode == 200).Json<CatalogSubscription>();
            string path = "/v1.0/api/catalog/" + subscription.PublicationId + "/subscription";
            (await member.Client.PutAsync(path, new { emailEnabled = false }, token).ConfigureAwait(false)).Expect(200, "retry succeeds at limit");
            CatalogPublication paid = await PublishAsync(admin, false, token).ConfigureAwait(false);
            (await member.Client.PutAsync("/v1.0/api/catalog/" + paid.Id + "/subscription", new { }, token).ConfigureAwait(false)).Expect(402, "free eligibility enforced");
            (await member.Client.DeleteAsync(path, token).ConfigureAwait(false)).Expect(204, "unsubscribe");
            (await member.Client.PutAsync(path, new { }, token).ConfigureAwait(false)).Expect(200, "restore freed slot");
        }
    }
}
