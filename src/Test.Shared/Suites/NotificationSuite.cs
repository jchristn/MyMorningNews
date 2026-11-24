namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Server;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Notifications: one preference matrix (kind by channel) with defaults, the old email preferences reading and
    /// writing the same cells, the in-app list and its privacy, audio ready notices, and what a user with default
    /// settings receives.
    /// </summary>
    public static class NotificationSuite
    {
        private const string S = "Notification";
        private const string P = "/v1.0/api/notifications/preferences";
        private const string N = "/v1.0/api/notifications";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Notification preferences, in-app notifications, and audio ready notices", new[]
            {
                Cases.Case(S, "Defaults", "A new user gets email for results, briefings, failures, and limits, and nothing by push or in-app", DefaultsAsync),
                Cases.Case(S, "RoundTrip", "Saved cells come back, only the sent cells change, bad values are refused, and another user is unaffected", RoundTripAsync),
                Cases.Case(S, "EmailPreferences", "The old email preferences and the matrix read and write the same cells, both ways, and earlier unsubscribes carry over", EmailPreferencesAsync),
                Cases.Case(S, "InApp", "In-app notifications are created only for kinds turned on, are private, count unread, and can be read, read all, and deleted", InAppAsync),
                Cases.Case(S, "EarlierPush", "Devices registered without a push choice keep the kinds older app builds delivered; a saved choice is left alone", EarlierPushAsync),
                Cases.Case(S, "PredictionEmail", "A due prediction emails its owner once when they turned that on, and never by default", PredictionEmailAsync),
                Cases.Case(S, "AudioReady", "Audio that becomes ready is announced once by email, push, and in-app when turned on, and not at all by default", AudioReadyAsync),
                Cases.Case(S, "DefaultUser", "With default settings a run still sends its digest, an edition its briefing email, and nothing is pushed or listed in-app", DefaultUserAsync)
            });
        }

        private static async Task<NotificationPreferencesResponse> PrefsAsync(TestUser user, CancellationToken token)
        {
            return (await user.Client.GetAsync(P, token).ConfigureAwait(false)).Expect(200, "preferences").Json<NotificationPreferencesResponse>();
        }

        private static async Task<NotificationPreferencesResponse> SetAsync(TestUser user, NotificationKindEnum kind, NotificationChannelEnum channel, bool enabled, CancellationToken token)
        {
            return (await user.Client.PutAsync(P, new { preferences = new[] { new { kind = kind.ToString(), channel = channel.ToString(), enabled = enabled } } }, token).ConfigureAwait(false))
                .Expect(200, "save " + kind + " " + channel).Json<NotificationPreferencesResponse>();
        }

        private static bool On(NotificationPreferencesResponse prefs, NotificationKindEnum kind, NotificationChannelEnum channel)
        {
            return prefs.Preferences.Single(c => c.Kind == kind && c.Channel == channel).Enabled;
        }

        private static async Task<List<Notification>> ListAsync(TestUser user, string query, CancellationToken token)
        {
            return (await user.Client.GetAsync(N + query, token).ConfigureAwait(false)).Expect(200, "list").Json<EnumerationResult<Notification>>().Objects;
        }

        private static async Task<long> UnreadAsync(TestUser user, CancellationToken token)
        {
            return (await user.Client.GetAsync(N + "/unread-count", token).ConfigureAwait(false)).Expect(200, "unread count").Json<NotificationCountResponse>().Count;
        }

        private static async Task<string> DeviceAsync(TestUser user, CancellationToken token)
        {
            string pushToken = "ExponentPushToken[live-" + Guid.NewGuid().ToString("N").Substring(0, 12) + "]";
            (await user.Client.PostAsync("/v1.0/api/devices", new DeviceRegisterRequest { ExpoPushToken = pushToken, Platform = "ios" }, token).ConfigureAwait(false)).Expect(201, "device");
            return pushToken;
        }

        private static List<string> PushesTo(string pushToken, int skip)
        {
            return StubModelServer.Shared.PushRequests.Skip(skip).Where(x => x.Contains(pushToken, StringComparison.Ordinal)).ToList();
        }

        private static async Task<List<EmailMessage>> MailAsync(MyMorningNewsServer server, string tenantId, EmailTemplateEnum template, CancellationToken token)
        {
            return (await server.Context.Database.EmailMessages.EnumerateAsync(new EmailMessageFilter { TenantId = tenantId, Template = template, PageSize = 100 }, token).ConfigureAwait(false)).Objects;
        }

        private static async Task DefaultsAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser user = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            NotificationPreferencesResponse prefs = await PrefsAsync(user, token).ConfigureAwait(false);
            Assert.Equal(18, prefs.Preferences.Count, "six kinds on three channels");
            Assert.True(prefs.Preferences[0].Kind == NotificationKindEnum.RunResults && prefs.Preferences[0].Channel == NotificationChannelEnum.Email && prefs.Preferences[17].Kind == NotificationKindEnum.PredictionDue
                && prefs.Preferences[17].Channel == NotificationChannelEnum.InApp, "kind order, then channel order");
            foreach (NotificationKindEnum kind in Enum.GetValues<NotificationKindEnum>())
            {
                bool emailDefault = kind == NotificationKindEnum.RunResults || kind == NotificationKindEnum.BriefingReady || kind == NotificationKindEnum.RunFailed || kind == NotificationKindEnum.CapReached;
                Assert.Equal(emailDefault, On(prefs, kind, NotificationChannelEnum.Email), kind + " email default");
                Assert.Equal(false, On(prefs, kind, NotificationChannelEnum.Push), kind + " push is off until chosen");
                Assert.Equal(false, On(prefs, kind, NotificationChannelEnum.InApp), kind + " in-app is off until chosen");
                Assert.Equal(emailDefault, NotificationPreferenceService.Default(kind, NotificationChannelEnum.Email), kind + " default table");
            }

            Assert.True(prefs.Address == user.User.Email.ToLowerInvariant() && prefs.UnsubscribedTopicIds.Count == 0, "address and no topic unsubscribes");
            NotificationChannelStatus email = prefs.Channels.Single(c => c.Channel == NotificationChannelEnum.Email);
            NotificationChannelStatus push = prefs.Channels.Single(c => c.Channel == NotificationChannelEnum.Push);
            NotificationChannelStatus inApp = prefs.Channels.Single(c => c.Channel == NotificationChannelEnum.InApp);
            Assert.True(email.Available && email.Reason == null && inApp.Available, "email and in-app can deliver");
            Assert.True(!push.Available && push.Reason == NotificationUnavailableReasonEnum.NoDevice, "push needs a device: " + push.Reason);
            Assert.Equal(0L, await (await SharedTestServer.GetServerAsync(token).ConfigureAwait(false)).Context.Database.NotificationPreferences.CountAsync(new NotificationPreferenceFilter { TenantId = t.Id, UserId = user.User.Id }, token).ConfigureAwait(false),
                "reading stores nothing");
            (await (await SharedTestServer.AnonymousClientAsync(token).ConfigureAwait(false)).GetAsync(P, token).ConfigureAwait(false)).Expect(401, "signed-in users only");
        }

        private static async Task RoundTripAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser user = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            TestUser other = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);

            NotificationPreferencesResponse saved = (await user.Client.PutAsync(P, new
            {
                preferences = new object[]
                {
                    new { kind = "AudioReady", channel = "Email", enabled = true },
                    new { kind = "AudioReady", channel = "InApp", enabled = true },
                    new { kind = "RunResults", channel = "Email", enabled = false },
                    new { kind = "PredictionDue", channel = "Push", enabled = true }
                }
            }, token).ConfigureAwait(false)).Expect(200, "save four cells").Json<NotificationPreferencesResponse>();
            Assert.True(On(saved, NotificationKindEnum.AudioReady, NotificationChannelEnum.Email) && On(saved, NotificationKindEnum.AudioReady, NotificationChannelEnum.InApp)
                && !On(saved, NotificationKindEnum.RunResults, NotificationChannelEnum.Email) && On(saved, NotificationKindEnum.PredictionDue, NotificationChannelEnum.Push), "the response shows the saved cells");
            NotificationPreferencesResponse read = await PrefsAsync(user, token).ConfigureAwait(false);
            Assert.True(On(read, NotificationKindEnum.AudioReady, NotificationChannelEnum.Email) && !On(read, NotificationKindEnum.RunResults, NotificationChannelEnum.Email)
                && On(read, NotificationKindEnum.BriefingReady, NotificationChannelEnum.Email) && !On(read, NotificationKindEnum.AudioReady, NotificationChannelEnum.Push), "saved cells persist; unsent cells keep their defaults");
            Assert.True(read.Channels.Single(c => c.Channel == NotificationChannelEnum.Push).Reason == NotificationUnavailableReasonEnum.NoDevice, "a push cell can be saved before a device exists");

            NotificationPreferencesResponse back = await SetAsync(user, NotificationKindEnum.RunResults, NotificationChannelEnum.Email, true, token).ConfigureAwait(false);
            Assert.True(On(back, NotificationKindEnum.RunResults, NotificationChannelEnum.Email) && On(back, NotificationKindEnum.AudioReady, NotificationChannelEnum.InApp), "one cell changes, the rest stay");
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            Assert.Equal(4L, await server.Context.Database.NotificationPreferences.CountAsync(new NotificationPreferenceFilter { TenantId = t.Id, UserId = user.User.Id }, token).ConfigureAwait(false), "one row per saved cell, updated in place");

            (await user.Client.PutAsync(P, new { preferences = new[] { new { kind = "Bogus", channel = "Email", enabled = true } } }, token).ConfigureAwait(false)).Expect(400, "unknown kind");
            (await user.Client.PutAsync(P, new { }, token).ConfigureAwait(false)).Expect(200, "nothing to change");
            NotificationPreferencesResponse theirs = await PrefsAsync(other, token).ConfigureAwait(false);
            Assert.True(!On(theirs, NotificationKindEnum.AudioReady, NotificationChannelEnum.Email) && !On(theirs, NotificationKindEnum.PredictionDue, NotificationChannelEnum.Push), "another user keeps their own settings");

            await DeviceAsync(user, token).ConfigureAwait(false);
            NotificationPreferencesResponse withDevice = await PrefsAsync(user, token).ConfigureAwait(false);
            Assert.True(withDevice.Channels.Single(c => c.Channel == NotificationChannelEnum.Push).Available, "push can deliver once a device is registered");
            Assert.True(On(withDevice, NotificationKindEnum.PredictionDue, NotificationChannelEnum.Push) && !On(withDevice, NotificationKindEnum.BriefingReady, NotificationChannelEnum.Push), "registering after a saved choice turns nothing else on");
        }

        private static async Task EmailPreferencesAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser user = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            string address = user.User.Email.ToLowerInvariant();

            EmailPreferencesResponse old = (await user.Client.PutAsync("/v1.0/api/email-preferences", new { briefingEmails = false }, token).ConfigureAwait(false)).Expect(200, "old route").Json<EmailPreferencesResponse>();
            Assert.True(old.RunEmails && !old.BriefingEmails && old.FailureEmails, "old route: briefing emails off");
            NotificationPreferencesResponse matrix = await PrefsAsync(user, token).ConfigureAwait(false);
            Assert.True(!On(matrix, NotificationKindEnum.BriefingReady, NotificationChannelEnum.Email) && On(matrix, NotificationKindEnum.RunResults, NotificationChannelEnum.Email), "the matrix shows what the old route saved");
            Assert.Equal(0L, await server.Context.Database.EmailSuppressions.CountAsync(new EmailSuppressionFilter { TenantId = t.Id, Recipient = address }, token).ConfigureAwait(false), "saved as a preference row, not a second record");

            await SetAsync(user, NotificationKindEnum.RunResults, NotificationChannelEnum.Email, false, token).ConfigureAwait(false);
            await SetAsync(user, NotificationKindEnum.BriefingReady, NotificationChannelEnum.Email, true, token).ConfigureAwait(false);
            await SetAsync(user, NotificationKindEnum.RunFailed, NotificationChannelEnum.Email, false, token).ConfigureAwait(false);
            EmailPreferencesResponse mirrored = (await user.Client.GetAsync("/v1.0/api/email-preferences", token).ConfigureAwait(false)).Expect(200, "old route read").Json<EmailPreferencesResponse>();
            Assert.True(!mirrored.RunEmails && mirrored.BriefingEmails && !mirrored.FailureEmails, "the old route shows what the matrix saved");
            Assert.True(await server.Context.Email.IsSuppressedAsync(t.Id, address, EmailCategoryEnum.RunEmails, null, token).ConfigureAwait(false)
                && !await server.Context.Email.IsSuppressedAsync(t.Id, address, EmailCategoryEnum.BriefingEmails, null, token).ConfigureAwait(false), "and sending honors the same cells");

            // An unsubscribe recorded by address before the matrix existed is read as the cell until the user saves it.
            TestUser earlier = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            string earlierAddress = earlier.User.Email.ToLowerInvariant();
            await server.Context.Database.EmailSuppressions.CreateAsync(new EmailSuppression { TenantId = t.Id, Recipient = earlierAddress, Category = EmailCategoryEnum.RunEmails, Source = "link" }, token).ConfigureAwait(false);
            NotificationPreferencesResponse carried = await PrefsAsync(earlier, token).ConfigureAwait(false);
            EmailPreferencesResponse carriedOld = (await earlier.Client.GetAsync("/v1.0/api/email-preferences", token).ConfigureAwait(false)).Json<EmailPreferencesResponse>();
            Assert.True(!On(carried, NotificationKindEnum.RunResults, NotificationChannelEnum.Email) && !carriedOld.RunEmails && carriedOld.BriefingEmails, "an earlier unsubscribe shows as off in both places");
            Assert.True(await server.Context.Email.IsSuppressedAsync(t.Id, earlierAddress, EmailCategoryEnum.RunEmails, null, token).ConfigureAwait(false), "and still stops the email");
            await SetAsync(earlier, NotificationKindEnum.RunResults, NotificationChannelEnum.Email, true, token).ConfigureAwait(false);
            Assert.Equal(0L, await server.Context.Database.EmailSuppressions.CountAsync(new EmailSuppressionFilter { TenantId = t.Id, Recipient = earlierAddress }, token).ConfigureAwait(false), "saving the cell replaces the earlier record");
            Assert.True(!await server.Context.Email.IsSuppressedAsync(t.Id, earlierAddress, EmailCategoryEnum.RunEmails, null, token).ConfigureAwait(false)
                && (await earlier.Client.GetAsync("/v1.0/api/email-preferences", token).ConfigureAwait(false)).Json<EmailPreferencesResponse>().RunEmails, "turned back on everywhere");

            // An unsubscribe link (which acts by address) saves the same cell for a user; a topic choice stays per topic.
            await server.Context.Email.SetSuppressedAsync(t.Id, earlierAddress, EmailCategoryEnum.BriefingEmails, null, true, "link", token).ConfigureAwait(false);
            Assert.True(!On(await PrefsAsync(earlier, token).ConfigureAwait(false), NotificationKindEnum.BriefingReady, NotificationChannelEnum.Email), "an unsubscribe link turns the cell off");
            Topic topic = (await earlier.Client.PostAsync("/v1.0/api/topics", new { name = "Quiet topic" }, token).ConfigureAwait(false)).Expect(201, "topic").Json<Topic>();
            NotificationPreferencesResponse topicOff = (await earlier.Client.PutAsync(P, new { unsubscribeTopicIds = new[] { topic.Id } }, token).ConfigureAwait(false)).Expect(200, "topic off").Json<NotificationPreferencesResponse>();
            Assert.True(topicOff.UnsubscribedTopicIds.Single() == topic.Id && On(topicOff, NotificationKindEnum.RunResults, NotificationChannelEnum.Email), "a topic unsubscribe is listed and leaves the cell on");
            Assert.True((await earlier.Client.GetAsync("/v1.0/api/email-preferences", token).ConfigureAwait(false)).Json<EmailPreferencesResponse>().UnsubscribedTopicIds.Single() == topic.Id, "the old route lists it too");
            Assert.True(await server.Context.Email.IsSuppressedAsync(t.Id, earlierAddress, EmailCategoryEnum.RunEmails, topic.Id, token).ConfigureAwait(false)
                && !await server.Context.Email.IsSuppressedAsync(t.Id, earlierAddress, EmailCategoryEnum.RunEmails, "top_other", token).ConfigureAwait(false), "only that topic is stopped");
            NotificationPreferencesResponse topicOn = (await earlier.Client.PutAsync(P, new { resubscribeTopicIds = new[] { topic.Id } }, token).ConfigureAwait(false)).Expect(200, "topic on").Json<NotificationPreferencesResponse>();
            Assert.Equal(0, topicOn.UnsubscribedTopicIds.Count, "resubscribed");

            // Someone who is not a user of the workspace is still remembered by address.
            await server.Context.Email.SetSuppressedAsync(t.Id, "outsider@example.test", EmailCategoryEnum.RunEmails, null, true, "link", token).ConfigureAwait(false);
            Assert.True(await server.Context.Email.IsSuppressedAsync(t.Id, "outsider@example.test", EmailCategoryEnum.RunEmails, null, token).ConfigureAwait(false), "outside recipients can unsubscribe");
        }

        private static async Task InAppAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser user = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            TestUser other = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            Topic topic = (await user.Client.PostAsync("/v1.0/api/topics", new { name = "Notified topic" }, token).ConfigureAwait(false)).Expect(201, "topic").Json<Topic>();
            Run failed = new Run { Id = "run_" + Guid.NewGuid().ToString("N").Substring(0, 20), TenantId = t.Id, TopicId = topic.Id, UserId = user.User.Id, Status = RunStatusEnum.Failed };

            await server.Context.Scheduling.OnRunFailed!(failed, token).ConfigureAwait(false);
            Assert.Equal(0, (await ListAsync(user, String.Empty, token).ConfigureAwait(false)).Count, "nothing in-app until the kind is turned on");
            Assert.Equal(0L, await UnreadAsync(user, token).ConfigureAwait(false), "and nothing unread");

            await SetAsync(user, NotificationKindEnum.RunFailed, NotificationChannelEnum.InApp, true, token).ConfigureAwait(false);
            await SetAsync(user, NotificationKindEnum.RunResults, NotificationChannelEnum.InApp, true, token).ConfigureAwait(false);
            await SetAsync(other, NotificationKindEnum.RunFailed, NotificationChannelEnum.InApp, true, token).ConfigureAwait(false);
            await server.Context.Scheduling.OnRunFailed!(failed, token).ConfigureAwait(false);
            Run found = new Run { Id = "run_" + Guid.NewGuid().ToString("N").Substring(0, 20), TenantId = t.Id, TopicId = topic.Id, UserId = user.User.Id, Status = RunStatusEnum.Completed, NewFindingCount = 3 };
            NotificationResult results = await server.Context.Notifications.NotifyAsync(t.Id, user.User.Id, NotificationKindEnum.RunResults, new NotificationContext { Run = found, EmailAllowed = false }, token).ConfigureAwait(false);
            Run empty = new Run { Id = "run_" + Guid.NewGuid().ToString("N").Substring(0, 20), TenantId = t.Id, TopicId = topic.Id, UserId = user.User.Id, Status = RunStatusEnum.Completed, NewFindingCount = 0 };
            NotificationResult nothing = await server.Context.Notifications.NotifyAsync(t.Id, user.User.Id, NotificationKindEnum.RunResults, new NotificationContext { Run = empty }, token).ConfigureAwait(false);
            Assert.True(results.InApp && !nothing.InApp, "a run with new findings is listed; a run with none is not");
            await server.Context.Notifications.NotifyAsync(t.Id, user.User.Id, NotificationKindEnum.BriefingReady,
                new NotificationContext { Briefing = new Briefing { TenantId = t.Id, UserId = user.User.Id, Title = "Not wanted", Status = BriefingStatusEnum.Ready, IsDryRun = true } }, token).ConfigureAwait(false);

            List<Notification> mine = await ListAsync(user, String.Empty, token).ConfigureAwait(false);
            Assert.True(mine.Count == 2 && mine[0].Kind == NotificationKindEnum.RunResults && mine[1].Kind == NotificationKindEnum.RunFailed, "two notifications, newest first, only for kinds turned on: " + String.Join(",", mine.Select(n => n.Kind)));
            Notification runFailed = mine[1];
            Assert.True(runFailed.Id.StartsWith("ntf_", StringComparison.Ordinal) && runFailed.Title == topic.Name && runFailed.RunId == failed.Id && runFailed.TopicId == topic.Id && runFailed.ReadUtc == null && runFailed.UserId == user.User.Id,
                "title and link target: " + runFailed.Title);
            Assert.True(mine[0].Title == topic.Name && mine[0].Body == "3 new findings." && mine[0].RunId == found.Id, "run results text: " + mine[0].Body);
            Assert.Equal(2L, await UnreadAsync(user, token).ConfigureAwait(false), "two unread");
            Assert.Equal(1, (await ListAsync(user, "?kind=RunFailed", token).ConfigureAwait(false)).Count, "filter by kind");
            EnumerationResult<Notification> paged = (await user.Client.GetAsync(N + "?pageSize=1", token).ConfigureAwait(false)).Expect(200, "paged").Json<EnumerationResult<Notification>>();
            Assert.True(paged.Objects.Count == 1 && paged.TotalRecords == 2 && paged.TotalPages == 2, "paged");

            Assert.Equal(0, (await ListAsync(other, String.Empty, token).ConfigureAwait(false)).Count, "another user sees none of them");
            Assert.Equal(0, (await ListAsync(other, "?userId=" + user.User.Id, token).ConfigureAwait(false)).Count, "even when asking for my user ID");
            Assert.Equal(0L, await UnreadAsync(other, token).ConfigureAwait(false), "and counts none");
            (await other.Client.PostAsync(N + "/" + runFailed.Id + "/read", null, token).ConfigureAwait(false)).Expect(404, "cannot mark mine read");
            (await other.Client.DeleteAsync(N + "/" + runFailed.Id, token).ConfigureAwait(false)).Expect(404, "cannot delete mine");
            Assert.Equal(0L, (await other.Client.PostAsync(N + "/read-all", null, token).ConfigureAwait(false)).Expect(200, "their read-all").Json<NotificationCountResponse>().Count, "their read-all touches nothing of mine");
            Assert.Equal(2L, await UnreadAsync(user, token).ConfigureAwait(false), "still two unread");

            Notification read = (await user.Client.PostAsync(N + "/" + runFailed.Id + "/read", null, token).ConfigureAwait(false)).Expect(200, "read").Json<Notification>();
            Assert.True(read.ReadUtc.HasValue, "marked read");
            Notification again = (await user.Client.PostAsync(N + "/" + runFailed.Id + "/read", null, token).ConfigureAwait(false)).Expect(200, "read again").Json<Notification>();
            Assert.Equal(read.ReadUtc, again.ReadUtc, "reading twice keeps the first time");
            Assert.Equal(1L, await UnreadAsync(user, token).ConfigureAwait(false), "one unread");
            Assert.True((await ListAsync(user, "?unread=true", token).ConfigureAwait(false)).Single().Id == mine[0].Id && (await ListAsync(user, "?unread=false", token).ConfigureAwait(false)).Single().Id == runFailed.Id, "filter by unread");
            Assert.Equal(1L, (await user.Client.PostAsync(N + "/read-all", null, token).ConfigureAwait(false)).Expect(200, "read all").Json<NotificationCountResponse>().Count, "read-all marks the rest");
            Assert.Equal(0L, await UnreadAsync(user, token).ConfigureAwait(false), "none unread");
            (await user.Client.DeleteAsync(N + "/" + runFailed.Id, token).ConfigureAwait(false)).Expect(204, "delete");
            (await user.Client.DeleteAsync(N + "/" + runFailed.Id, token).ConfigureAwait(false)).Expect(404, "already gone");
            (await user.Client.PostAsync(N + "/ntf_missing/read", null, token).ConfigureAwait(false)).Expect(404, "unknown ID");
            Assert.Equal(1, (await ListAsync(user, String.Empty, token).ConfigureAwait(false)).Count, "one left");

            await server.Context.Database.Notifications.PruneAsync(DateTime.UtcNow.AddDays(-90), token).ConfigureAwait(false);
            Assert.Equal(1, (await ListAsync(user, String.Empty, token).ConfigureAwait(false)).Count, "retention keeps anything newer than 90 days");

            await SetAsync(user, NotificationKindEnum.RunFailed, NotificationChannelEnum.InApp, false, token).ConfigureAwait(false);
            await server.Context.Scheduling.OnRunFailed!(failed, token).ConfigureAwait(false);
            Assert.Equal(1, (await ListAsync(user, String.Empty, token).ConfigureAwait(false)).Count, "turned off again: nothing new");
        }

        private static async Task EarlierPushAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            server.Context.SettingsService.Current.Push.ExpoPushUrl = StubModelServer.Shared.BaseUrl + "/push/send";
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);

            // An older app build registers a device and expects the pushes it always got.
            TestUser olderApp = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            await DeviceAsync(olderApp, token).ConfigureAwait(false);
            NotificationPreferencesResponse kept = await PrefsAsync(olderApp, token).ConfigureAwait(false);
            Assert.True(On(kept, NotificationKindEnum.BriefingReady, NotificationChannelEnum.Push) && On(kept, NotificationKindEnum.RunFailed, NotificationChannelEnum.Push)
                && On(kept, NotificationKindEnum.CapReached, NotificationChannelEnum.Push) && On(kept, NotificationKindEnum.PredictionDue, NotificationChannelEnum.Push), "the four kinds pushed before stay on");
            Assert.True(!On(kept, NotificationKindEnum.AudioReady, NotificationChannelEnum.Push) && !On(kept, NotificationKindEnum.RunResults, NotificationChannelEnum.Push), "new kinds stay off until chosen");
            await SetAsync(olderApp, NotificationKindEnum.RunFailed, NotificationChannelEnum.Push, false, token).ConfigureAwait(false);
            await DeviceAsync(olderApp, token).ConfigureAwait(false);
            Assert.True(!On(await PrefsAsync(olderApp, token).ConfigureAwait(false), NotificationKindEnum.RunFailed, NotificationChannelEnum.Push), "a second device never turns a kind back on");

            // A device row from before the upgrade (no preference rows at all) is picked up by the start-up step.
            TestUser upgraded = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            string upgradedToken = "ExponentPushToken[live-" + Guid.NewGuid().ToString("N").Substring(0, 12) + "]";
            await server.Context.Database.Devices.CreateAsync(new Device { TenantId = t.Id, UserId = upgraded.User.Id, ExpoPushToken = upgradedToken, Platform = "android" }, token).ConfigureAwait(false);
            Assert.True(!On(await PrefsAsync(upgraded, token).ConfigureAwait(false), NotificationKindEnum.BriefingReady, NotificationChannelEnum.Push), "off before the step runs");
            Assert.True(await server.Context.Notifications.Preferences.KeepEarlierPushForDevicesAsync(token).ConfigureAwait(false) >= 1, "the step finds the device owner");
            NotificationPreferencesResponse after = await PrefsAsync(upgraded, token).ConfigureAwait(false);
            Assert.True(On(after, NotificationKindEnum.BriefingReady, NotificationChannelEnum.Push) && On(after, NotificationKindEnum.PredictionDue, NotificationChannelEnum.Push) && !On(after, NotificationKindEnum.AudioReady, NotificationChannelEnum.Push), "kept after the upgrade");
            await SetAsync(upgraded, NotificationKindEnum.BriefingReady, NotificationChannelEnum.Push, false, token).ConfigureAwait(false);
            await server.Context.Notifications.Preferences.KeepEarlierPushForDevicesAsync(token).ConfigureAwait(false);
            Assert.True(!On(await PrefsAsync(upgraded, token).ConfigureAwait(false), NotificationKindEnum.BriefingReady, NotificationChannelEnum.Push), "running the step again never undoes a choice");

            // The run failure push still reaches the upgraded device, and stops when the user turns it off.
            Topic topic = (await upgraded.Client.PostAsync("/v1.0/api/topics", new { name = "Push topic" }, token).ConfigureAwait(false)).Expect(201, "topic").Json<Topic>();
            Run failed = new Run { Id = "run_" + Guid.NewGuid().ToString("N").Substring(0, 20), TenantId = t.Id, TopicId = topic.Id, UserId = upgraded.User.Id, Status = RunStatusEnum.Failed };
            int before = StubModelServer.Shared.PushRequests.Count;
            await server.Context.Scheduling.OnRunFailed!(failed, token).ConfigureAwait(false);
            Assert.True(PushesTo(upgradedToken, before).Single().Contains("\"kind\":\"run.failed\"", StringComparison.Ordinal), "run failure pushed as before");
            await SetAsync(upgraded, NotificationKindEnum.RunFailed, NotificationChannelEnum.Push, false, token).ConfigureAwait(false);
            int beforeOff = StubModelServer.Shared.PushRequests.Count;
            await server.Context.Scheduling.OnRunFailed!(failed, token).ConfigureAwait(false);
            Assert.Equal(0, PushesTo(upgradedToken, beforeOff).Count, "and not after it is turned off");
        }

        private static async Task PredictionEmailAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser user = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            Topic topic = (await user.Client.PostAsync("/v1.0/api/topics", new { name = "Prediction topic" }, token).ConfigureAwait(false)).Expect(201, "topic").Json<Topic>();
            Run past = await server.Context.Database.Runs.CreateAsync(new Run { TenantId = t.Id, TopicId = topic.Id, UserId = user.User.Id, Trigger = RunTriggerEnum.Manual, Status = RunStatusEnum.Completed }, token).ConfigureAwait(false);
            Prediction quiet = await server.Context.Database.Predictions.CreateAsync(new Prediction
            {
                TenantId = t.Id, RunId = past.Id, TopicId = topic.Id, UserId = user.User.Id, Statement = "Rates hold through winter.", Confidence = 0.5, HorizonUtc = DateTime.UtcNow.AddDays(-1), Status = PredictionStatusEnum.Open
            }, token).ConfigureAwait(false);
            await server.Context.PredictionNotices.NotifyDueAsync(DateTime.UtcNow, token).ConfigureAwait(false);
            Assert.Equal(0, (await MailAsync(server, t.Id, EmailTemplateEnum.PredictionDue, token).ConfigureAwait(false)).Count, "no prediction email by default");
            Assert.True((await server.Context.Database.Predictions.ReadAsync(t.Id, quiet.Id, token).ConfigureAwait(false))!.DueNotifiedUtc.HasValue, "still marked noticed");

            await SetAsync(user, NotificationKindEnum.PredictionDue, NotificationChannelEnum.Email, true, token).ConfigureAwait(false);
            await SetAsync(user, NotificationKindEnum.PredictionDue, NotificationChannelEnum.InApp, true, token).ConfigureAwait(false);
            Prediction due = await server.Context.Database.Predictions.CreateAsync(new Prediction
            {
                TenantId = t.Id, RunId = past.Id, TopicId = topic.Id, UserId = user.User.Id, Statement = "Prices fall by spring.", Confidence = 0.6, HorizonUtc = DateTime.UtcNow.AddDays(-1), Status = PredictionStatusEnum.Open
            }, token).ConfigureAwait(false);
            await server.Context.PredictionNotices.NotifyDueAsync(DateTime.UtcNow, token).ConfigureAwait(false);
            await server.Context.PredictionNotices.NotifyDueAsync(DateTime.UtcNow, token).ConfigureAwait(false);
            EmailMessage mail = (await MailAsync(server, t.Id, EmailTemplateEnum.PredictionDue, token).ConfigureAwait(false)).Single();
            Assert.True(mail.Status == EmailStatusEnum.Queued && mail.Subject == "[My Morning News] A prediction is due" && mail.RenderedText!.Contains("Prices fall by spring.", StringComparison.Ordinal)
                && mail.RenderedText.Contains("/dashboard/predictions?id=" + due.Id, StringComparison.Ordinal) && mail.RenderedText.Contains("Unsubscribe: http", StringComparison.Ordinal)
                && mail.RenderedText.Contains("Notification preferences: ", StringComparison.Ordinal), "one email with the statement, a link, and the footer: " + mail.Subject);
            Notification listed = (await ListAsync(user, String.Empty, token).ConfigureAwait(false)).Single();
            Assert.True(listed.Kind == NotificationKindEnum.PredictionDue && listed.PredictionId == due.Id, "and one in-app notification pointing at it");
        }

        private static async Task<Briefing> EditionBriefingAsync(PipelineFixture f, string name, bool email, CancellationToken token)
        {
            EditionResponse e = (await f.User.Client.PostAsync("/v1.0/api/editions", new { name = name, freshResearchBeforeCompose = false, audioEnabled = true, emailEnabled = email, lookbackHours = 48 }, token).ConfigureAwait(false))
                .Expect(201, "edition").Json<EditionResponse>();
            (await f.User.Client.PutAsync("/v1.0/api/editions/" + e.Edition.Id + "/topics", new { topicIds = new[] { f.Topic.Id } }, token).ConfigureAwait(false)).Expect(200, "topics");
            Briefing queued = (await f.User.Client.PostAsync("/v1.0/api/editions/" + e.Edition.Id + "/compose", null, token).ConfigureAwait(false)).Expect(202, "compose").Json<Briefing>();
            Briefing b = await BriefingWaiter.FinishAsync(f.Server, queued.TenantId, queued.Id, token).ConfigureAwait(false);
            Assert.True(b.Status == BriefingStatusEnum.Ready, "briefing ready: " + b.ErrorMessage);
            return b;
        }

        private static async Task<MediaAsset> EpisodeForAsync(PipelineFixture f, Briefing b, CancellationToken token)
        {
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.Elapsed < TimeSpan.FromSeconds(60))
            {
                int done = await f.Server.Context.Audio.ProcessQueuedAsync(DateTime.UtcNow, new[] { b.TenantId }, token).ConfigureAwait(false);
                EnumerationResult<MediaAsset> list = (await f.User.Client.GetAsync("/v1.0/api/media?briefingId=" + b.Id, token).ConfigureAwait(false)).Json<EnumerationResult<MediaAsset>>();
                MediaAsset? m = list.Objects.OrderByDescending(x => x.CreatedUtc).FirstOrDefault();
                if (m != null && (m.Status == MediaStatusEnum.Ready || m.Status == MediaStatusEnum.Failed)) return m;
                if (done == 0) await Task.Delay(50, token).ConfigureAwait(false);
            }

            throw new InvalidOperationException("No finished episode for briefing " + b.Id);
        }

        private static async Task AudioReadyAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            f.Server.Context.SettingsService.Current.Push.ExpoPushUrl = StubModelServer.Shared.BaseUrl + "/push/send";
            string tenantId = f.Topic.TenantId;
            await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);

            // Default settings, with a device registered the way older app builds do it: audio is not announced anywhere.
            string device = await DeviceAsync(f.User, token).ConfigureAwait(false);
            int beforeQuiet = StubModelServer.Shared.PushRequests.Count;
            Briefing quiet = await EditionBriefingAsync(f, "Quiet audio", false, token).ConfigureAwait(false);
            MediaAsset quietAudio = await EpisodeForAsync(f, quiet, token).ConfigureAwait(false);
            Assert.True(quietAudio.Status == MediaStatusEnum.Ready, "audio ready: " + quietAudio.ErrorMessage);
            Assert.Equal(0, (await MailAsync(f.Server, tenantId, EmailTemplateEnum.AudioReady, token).ConfigureAwait(false)).Count, "no audio email by default");
            Assert.True(!PushesTo(device, beforeQuiet).Any(p => p.Contains("audio.ready", StringComparison.Ordinal)), "no audio push by default");
            Assert.Equal(0, (await ListAsync(f.User, "?kind=AudioReady", token).ConfigureAwait(false)).Count, "no in-app audio notification by default");

            foreach (NotificationChannelEnum channel in Enum.GetValues<NotificationChannelEnum>())
                await SetAsync(f.User, NotificationKindEnum.AudioReady, channel, true, token).ConfigureAwait(false);
            int before = StubModelServer.Shared.PushRequests.Count;
            Briefing b = await EditionBriefingAsync(f, "Loud audio", false, token).ConfigureAwait(false);
            MediaAsset m = await EpisodeForAsync(f, b, token).ConfigureAwait(false);
            Assert.True(m.Status == MediaStatusEnum.Ready, "audio ready: " + m.ErrorMessage);
            await f.Server.Context.Audio.ProcessQueuedAsync(DateTime.UtcNow, new[] { tenantId }, token).ConfigureAwait(false);

            // The edition's email switch is off, so the audio email is held back while push and in-app go out.
            Assert.Equal(0, (await MailAsync(f.Server, tenantId, EmailTemplateEnum.AudioReady, token).ConfigureAwait(false)).Count, "the edition's email switch still filters email");
            string push = PushesTo(device, before).Single(p => p.Contains("audio.ready", StringComparison.Ordinal));
            Assert.True(push.Contains("\"briefingId\":\"" + b.Id + "\"", StringComparison.Ordinal) && push.Contains("\"mediaAssetId\":\"" + m.Id + "\"", StringComparison.Ordinal) && push.Contains("\"channelId\":\"briefings\"", StringComparison.Ordinal),
                "one push with the briefing and audio IDs: " + push);
            Notification listed = (await ListAsync(f.User, "?kind=AudioReady", token).ConfigureAwait(false)).Single();
            Assert.True(listed.BriefingId == b.Id && listed.MediaAssetId == m.Id && listed.Title == b.Title && listed.ReadUtc == null, "one in-app notification pointing at the audio");

            int beforeMail = StubModelServer.Shared.PushRequests.Count;
            Briefing mailed = await EditionBriefingAsync(f, "Mailed audio", true, token).ConfigureAwait(false);
            MediaAsset mailedAudio = await EpisodeForAsync(f, mailed, token).ConfigureAwait(false);
            await f.Server.Context.Audio.ProcessQueuedAsync(DateTime.UtcNow, new[] { tenantId }, token).ConfigureAwait(false);
            EmailMessage mail = (await MailAsync(f.Server, tenantId, EmailTemplateEnum.AudioReady, token).ConfigureAwait(false)).Single();
            Assert.True(mail.Status == EmailStatusEnum.Queued && mail.BriefingId == mailed.Id && mail.Subject == "[My Morning News] " + mailed.Title && mail.IdempotencyKey == "audio:" + mailedAudio.Id + ":" + mail.Recipient,
                "one audio email for the audio: " + mail.Subject);
            Assert.True(mail.RenderedHtml!.Contains("/dashboard/briefings/" + mailed.Id + "?listen=1", StringComparison.Ordinal) && mail.RenderedHtml.Contains(">Listen<", StringComparison.Ordinal)
                && mail.RenderedHtml.Contains("background:#2563eb", StringComparison.Ordinal) && mail.RenderedText!.Contains("Audio · ", StringComparison.Ordinal), "Listen button in the blue palette with the duration");
            Assert.True(mail.RenderedText!.Contains("Listen: http", StringComparison.Ordinal) && mail.RenderedText.Contains("Unsubscribe: http", StringComparison.Ordinal)
                && mail.RenderedText.Contains("Notification preferences: " + f.Server.Context.Settings.Rest.AppBaseUrl + "/dashboard/notifications", StringComparison.Ordinal)
                && (mail.RenderedHtml.Contains("© 2026 Lattice AI LLC", StringComparison.Ordinal) || mail.RenderedHtml.Contains("&#169; 2026 Lattice AI LLC", StringComparison.Ordinal)), "footer with preferences, unsubscribe, and the notice");
            Assert.Equal(1, PushesTo(device, beforeMail).Count(p => p.Contains("audio.ready", StringComparison.Ordinal)), "pushed once");
            Assert.Equal(2, (await ListAsync(f.User, "?kind=AudioReady", token).ConfigureAwait(false)).Count, "listed once per audio");
            Assert.Equal(1, (await MailAsync(f.Server, tenantId, EmailTemplateEnum.BriefingPublished, token).ConfigureAwait(false)).Count(x => x.BriefingId == mailed.Id), "the briefing email still goes out on its own");
        }

        private static async Task DefaultUserAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            string tenantId = f.Topic.TenantId;
            string address = f.User.User.Email.ToLowerInvariant();
            Run run = await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            EmailMessage digest = (await f.Server.Context.Database.EmailMessages.EnumerateAsync(new EmailMessageFilter { TenantId = tenantId, RunId = run.Id, PageSize = 10 }, token).ConfigureAwait(false)).Objects.Single();
            Assert.True(digest.Template == EmailTemplateEnum.RunDigest && digest.Status == EmailStatusEnum.Queued && digest.Recipient == address && digest.IdempotencyKey == "run:" + run.Id + ":" + address
                && digest.Subject.StartsWith("[My Morning News] " + f.Topic.Name + ": 2 new (", StringComparison.Ordinal), "the run digest is unchanged: " + digest.Subject);
            Assert.True(digest.RenderedText!.Contains("Unsubscribe from this topic: http", StringComparison.Ordinal) && digest.RenderedText.Contains("Email preferences: " + f.Server.Context.Settings.Rest.AppBaseUrl + "/dashboard/notifications", StringComparison.Ordinal),
                "its footer links to the one preferences page");

            Briefing b = await EditionBriefingAsync(f, "Default edition", true, token).ConfigureAwait(false);
            EmailMessage briefing = (await f.Server.Context.Database.EmailMessages.EnumerateAsync(new EmailMessageFilter { TenantId = tenantId, BriefingId = b.Id, Template = EmailTemplateEnum.BriefingPublished, PageSize = 10 }, token).ConfigureAwait(false)).Objects.Single();
            Assert.True(briefing.Status == EmailStatusEnum.Queued && briefing.Subject == "[My Morning News] " + b.Title && briefing.IdempotencyKey == "briefing:" + b.Id + ":" + address, "the briefing email is unchanged");

            Run failed = new Run { Id = "run_" + Guid.NewGuid().ToString("N").Substring(0, 20), TenantId = tenantId, TopicId = f.Topic.Id, UserId = f.User.User.Id, Status = RunStatusEnum.Failed };
            await f.Server.Context.Scheduling.OnRunFailed!(failed, token).ConfigureAwait(false);
            await f.Server.Context.Scheduling.OnRunFailed!(failed, token).ConfigureAwait(false);
            EmailMessage failure = (await f.Server.Context.Database.EmailMessages.EnumerateAsync(new EmailMessageFilter { TenantId = tenantId, RunId = failed.Id, PageSize = 10 }, token).ConfigureAwait(false)).Objects.Single();
            Assert.True(failure.Template == EmailTemplateEnum.RunFailed && failure.Status == EmailStatusEnum.Queued && failure.Recipient == address && failure.RenderedText!.Contains("/dashboard/runs?id=" + failed.Id, StringComparison.Ordinal)
                && failure.RenderedText.Contains(f.Topic.Name, StringComparison.Ordinal), "a failed run emails its owner once: " + failure.Subject);

            Assert.Equal(0, (await ListAsync(f.User, String.Empty, token).ConfigureAwait(false)).Count, "nothing is listed in-app by default");
            Assert.Equal(0L, await f.Server.Context.Database.NotificationPreferences.CountAsync(new NotificationPreferenceFilter { TenantId = tenantId, UserId = f.User.User.Id }, token).ConfigureAwait(false), "and no preference was stored for them");

            // Turning the kinds off stops exactly those emails; the old email route and the matrix agree.
            await SetAsync(f.User, NotificationKindEnum.RunResults, NotificationChannelEnum.Email, false, token).ConfigureAwait(false);
            (await f.User.Client.PutAsync("/v1.0/api/email-preferences", new { failureEmails = false }, token).ConfigureAwait(false)).Expect(200, "failure emails off through the old route");
            Run second = await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            List<EmailMessage> held = (await f.Server.Context.Database.EmailMessages.EnumerateAsync(new EmailMessageFilter { TenantId = tenantId, RunId = second.Id, PageSize = 10 }, token).ConfigureAwait(false)).Objects;
            Assert.True(held.Count == 0 || held.All(x => x.Status == EmailStatusEnum.Suppressed), "no digest once run results email is off: " + String.Join(",", held.Select(x => x.Status)));
            Run failedAgain = new Run { Id = "run_" + Guid.NewGuid().ToString("N").Substring(0, 20), TenantId = tenantId, TopicId = f.Topic.Id, UserId = f.User.User.Id, Status = RunStatusEnum.Failed };
            await f.Server.Context.Scheduling.OnRunFailed!(failedAgain, token).ConfigureAwait(false);
            List<EmailMessage> heldFailure = (await f.Server.Context.Database.EmailMessages.EnumerateAsync(new EmailMessageFilter { TenantId = tenantId, RunId = failedAgain.Id, PageSize = 10 }, token).ConfigureAwait(false)).Objects;
            Assert.Equal(0, heldFailure.Count, "no failure email once it is off");
        }
    }
}
