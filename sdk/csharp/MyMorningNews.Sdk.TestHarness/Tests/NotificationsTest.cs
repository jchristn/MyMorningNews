namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Notification preferences (defaults, round trip, agreement with the older email preferences) and the in-app
    /// notification list for a new member (empty list, zero unread, read-all, 404 on a missing ID).
    /// </summary>
    public class NotificationsTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Notifications and notification preferences";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public NotificationsTest()
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
            MyMorningNewsClient admin = context.RequireAdmin();
            string password = "Harness-" + Helpers.Unique();
            UserResponse user = await StepAsync(context, result, "POST user", () => Fixtures.CreateUserAsync(context, admin, password, token)).ConfigureAwait(false);
            using (MyMorningNewsClient c = context.NewClient())
            {
                await c.LoginAsync(user.Email, password, context.Environment.Tenant, token).ConfigureAwait(false);

                NotificationPreferencesResponse prefs = await StepAsync(context, result, "GET notifications/preferences (defaults)", () => c.Notifications.GetPreferencesAsync(token)).ConfigureAwait(false);
                Helpers.Equal(21, prefs.Preferences.Count, "seven kinds on three channels");
                Helpers.True(On(prefs, NotificationKindEnum.RunResults, NotificationChannelEnum.Email) && On(prefs, NotificationKindEnum.BriefingReady, NotificationChannelEnum.Email)
                    && On(prefs, NotificationKindEnum.RunFailed, NotificationChannelEnum.Email) && On(prefs, NotificationKindEnum.CapReached, NotificationChannelEnum.Email)
                    && On(prefs, NotificationKindEnum.TopicStale, NotificationChannelEnum.Email), "email is on by default for results, briefings, failures, limits, and stale topics");
                Helpers.True(!On(prefs, NotificationKindEnum.AudioReady, NotificationChannelEnum.Email) && !On(prefs, NotificationKindEnum.PredictionDue, NotificationChannelEnum.Email), "email is off by default for audio and predictions");
                Helpers.True(!prefs.Preferences.Any(x => x.Channel != NotificationChannelEnum.Email && x.Enabled), "push and in-app are off by default");
                NotificationChannelStatus push = prefs.Channels.Single(x => x.Channel == NotificationChannelEnum.Push);
                Helpers.True(!push.Available && (push.Reason == NotificationUnavailableReasonEnum.NoDevice || push.Reason == NotificationUnavailableReasonEnum.PushNotConfigured), "push needs a configured service and device");
                Helpers.True(prefs.Channels.Single(x => x.Channel == NotificationChannelEnum.InApp).Available, "in-app is always available");
                Helpers.NotEmpty(prefs.Address, "address");

                NotificationPreferencesResponse saved = await StepAsync(context, result, "PUT notifications/preferences", () => c.Notifications.UpdatePreferencesAsync(new NotificationPreferencesRequest
                {
                    Preferences = new List<NotificationPreferenceCell>
                    {
                        new NotificationPreferenceCell { Kind = NotificationKindEnum.AudioReady, Channel = NotificationChannelEnum.InApp, Enabled = true },
                        new NotificationPreferenceCell { Kind = NotificationKindEnum.AudioReady, Channel = NotificationChannelEnum.Push, Enabled = true },
                        new NotificationPreferenceCell { Kind = NotificationKindEnum.RunResults, Channel = NotificationChannelEnum.Email, Enabled = false }
                    }
                }, token)).ConfigureAwait(false);
                Helpers.True(On(saved, NotificationKindEnum.AudioReady, NotificationChannelEnum.InApp) && On(saved, NotificationKindEnum.AudioReady, NotificationChannelEnum.Push)
                    && !On(saved, NotificationKindEnum.RunResults, NotificationChannelEnum.Email) && On(saved, NotificationKindEnum.BriefingReady, NotificationChannelEnum.Email), "sent cells saved, the rest unchanged");
                NotificationPreferencesResponse read = await StepAsync(context, result, "GET notifications/preferences (saved)", () => c.Notifications.GetPreferencesAsync(token)).ConfigureAwait(false);
                Helpers.True(On(read, NotificationKindEnum.AudioReady, NotificationChannelEnum.InApp) && !On(read, NotificationKindEnum.RunResults, NotificationChannelEnum.Email), "saved cells persist");

                EmailPreferencesResponse old = await StepAsync(context, result, "GET email-preferences (agrees with the matrix)", () => c.Email.GetPreferencesAsync(token)).ConfigureAwait(false);
                Helpers.True(!old.RunEmails && old.BriefingEmails && old.FailureEmails, "the older route shows the same email cells");
                await StepAsync(context, result, "PUT email-preferences (briefing emails off)", () => c.Email.UpdatePreferencesAsync(new EmailPreferencesRequest { BriefingEmails = false, RunEmails = true }, token)).ConfigureAwait(false);
                NotificationPreferencesResponse mirrored = await StepAsync(context, result, "GET notifications/preferences (after the older route)", () => c.Notifications.GetPreferencesAsync(token)).ConfigureAwait(false);
                Helpers.True(!On(mirrored, NotificationKindEnum.BriefingReady, NotificationChannelEnum.Email) && On(mirrored, NotificationKindEnum.RunResults, NotificationChannelEnum.Email), "the matrix shows what the older route saved");

                EnumerationResult<Notification> list = await StepAsync(context, result, "GET notifications", () => c.Notifications.ListAsync(new NotificationListOptions { Unread = true, Kind = NotificationKindEnum.AudioReady, PageSize = 10 }, token)).ConfigureAwait(false);
                Helpers.True(list.Objects.Count == 0 && list.TotalRecords == 0, "a new member has no notifications");
                NotificationCountResponse unread = await StepAsync(context, result, "GET notifications/unread-count", () => c.Notifications.UnreadCountAsync(token)).ConfigureAwait(false);
                Helpers.Equal(0L, unread.Count, "none unread");
                NotificationCountResponse marked = await StepAsync(context, result, "POST notifications/read-all", () => c.Notifications.MarkAllReadAsync(token)).ConfigureAwait(false);
                Helpers.Equal(0L, marked.Count, "nothing to mark");
                await StepAsync(context, result, "POST unknown notification read (404)", () => Helpers.ExpectErrorAsync(() => c.Notifications.MarkReadAsync("ntf_missing", token), 404)).ConfigureAwait(false);
                await StepAsync(context, result, "DELETE unknown notification (404)", () => Helpers.ExpectErrorAsync(() => c.Notifications.DeleteAsync("ntf_missing", token), 404)).ConfigureAwait(false);
                await c.LogoutAsync(token).ConfigureAwait(false);
            }

            await StepAsync(context, result, "DELETE user", () => Fixtures.DeleteUserAsync(context, admin, user.Id, token)).ConfigureAwait(false);
        }

        private static bool On(NotificationPreferencesResponse prefs, NotificationKindEnum kind, NotificationChannelEnum channel)
        {
            return prefs.Preferences.Single(x => x.Kind == kind && x.Channel == channel).Enabled;
        }
    }
}
