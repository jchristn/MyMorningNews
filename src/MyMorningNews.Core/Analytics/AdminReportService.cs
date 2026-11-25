namespace MyMorningNews.Core.Analytics
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Email;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Settings;
    using SyslogLogging;

    /// <summary>
    /// The daily platform summary emailed to <see cref="AdminReportSettings.Recipients"/>: users, activity, research,
    /// model use and cost, audio, email, push, billing, and stale topic reviews for the previous UTC day, with the
    /// day before for comparison. Sending is idempotent per day and recipient, so calling it every hour is safe.
    /// Thread safety: stateless apart from injected services.
    /// </summary>
    public class AdminReportService
    {
        private readonly DatabaseDriverBase _Database;
        private readonly EmailService _Email;
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private const string _Header = "[AdminReportService] ";

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="email">Email.</param>
        /// <param name="settings">Settings.</param>
        /// <param name="logging">Logging.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public AdminReportService(DatabaseDriverBase database, EmailService email, ServerSettings settings, LoggingModule logging)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Email = email ?? throw new ArgumentNullException(nameof(email));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        /// <summary>
        /// Send yesterday's summary when it is due: the feature is on, there is at least one recipient, and the UTC hour
        /// has reached <see cref="AdminReportSettings.HourUtc"/>. A recipient who already got that day's summary is skipped.
        /// </summary>
        /// <param name="nowUtc">Current time, UTC.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Messages queued.</returns>
        public async Task<int> SendDueAsync(DateTime nowUtc, CancellationToken token = default)
        {
            AdminReportSettings s = _Settings.AdminReports;
            if (!s.Enabled || s.Recipients.Count == 0 || nowUtc.Hour < s.HourUtc) return 0;
            return await SendAsync(nowUtc.Date.AddDays(-1), s.Recipients, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Send the summary of one UTC day to the given addresses now, regardless of the schedule.
        /// </summary>
        /// <param name="dayUtc">The day (its date part is used).</param>
        /// <param name="recipients">Addresses.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Messages queued.</returns>
        /// <exception cref="ArgumentNullException">recipients is null.</exception>
        public async Task<int> SendAsync(DateTime dayUtc, IReadOnlyList<string> recipients, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(recipients);
            DateTime from = DateTime.SpecifyKind(dayUtc.Date, DateTimeKind.Utc);
            string key = "adminsummary:" + from.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            List<string> pending = new List<string>();
            foreach (string r in recipients.Select(EmailService.Normalize).Where(r => r.Length > 0).Distinct())
                if (await _Database.EmailMessages.ReadByKeyAsync("default", key + ":" + r, token).ConfigureAwait(false) == null) pending.Add(r);
            if (pending.Count == 0) return 0;

            PlatformDailyStats day = await _Database.PlatformStats.ReadAsync(from, from.AddDays(1), token).ConfigureAwait(false);
            PlatformDailyStats before = await _Database.PlatformStats.ReadAsync(from.AddDays(-1), from, token).ConfigureAwait(false);
            int queued = 0;
            foreach (string r in pending)
            {
                EmailMessage? m = await _Email.SendSystemEmailAsync(r, EmailTemplateEnum.AdminDailySummary, "Daily summary for " + from.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture),
                    Headline(day), b => Body(b, day, before), key, token).ConfigureAwait(false);
                if (m != null && m.Status != EmailStatusEnum.Suppressed) queued++;
            }

            if (queued > 0) _Logging.Info(_Header + "queued the summary for " + from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " to " + queued + " recipient(s)");
            return queued;
        }

        /// <summary>
        /// Render the summary body for a day (used by the email and by tests).
        /// </summary>
        /// <param name="b">Builder.</param>
        /// <param name="day">The day's statistics.</param>
        /// <param name="before">The previous day's statistics, for comparison.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static void Body(EmailBuilder b, PlatformDailyStats day, PlatformDailyStats before)
        {
            ArgumentNullException.ThrowIfNull(b);
            ArgumentNullException.ThrowIfNull(day);
            ArgumentNullException.ThrowIfNull(before);
            b.Section("Users");
            b.Metrics(new List<EmailMetric>
            {
                new EmailMetric("Daily active users", N(day.DailyActiveUsers), Change(day.DailyActiveUsers, before.DailyActiveUsers)),
                new EmailMetric("Weekly / monthly active users", N(day.WeeklyActiveUsers) + " / " + N(day.MonthlyActiveUsers)),
                new EmailMetric("Total users", N(day.TotalUsers)),
                new EmailMetric("New sign-ups", N(day.NewSignups), Change(day.NewSignups, before.NewSignups)),
                new EmailMetric("New accounts (all sources)", N(day.NewUsers)),
                new EmailMetric("Emails verified", N(day.EmailsVerified)),
                new EmailMetric("Users who signed in", N(day.SignIns)),
                new EmailMetric("Account deletions requested", N(day.DeletionRequests))
            });
            b.Section("AI use");
            b.Metrics(new List<EmailMetric>
            {
                new EmailMetric("Model cost", Usd(day.ModelCostUsd), "prior day " + Usd(before.ModelCostUsd)),
                new EmailMetric("Model cost, last 30 days", Usd(day.ModelCostUsd30Days)),
                new EmailMetric("Model time", Minutes(day.ModelMinutes)),
                new EmailMetric("Model calls", N(day.ModelCalls), day.ModelFailures > 0 ? N(day.ModelFailures) + " failed" : null),
                new EmailMetric("Tokens in / out", N(day.InputTokens) + " / " + N(day.OutputTokens)),
                new EmailMetric("Audio generated", Minutes(day.AudioMinutes), N(day.AudioFiles) + " files")
            });
            if (day.TopModels.Any())
            {
                b.Paragraph("Top models by cost:");
                b.Metrics(day.TopModels.Select(m => new EmailMetric(m.Provider + " " + m.Model, Usd(m.CostUsd), N(m.Calls) + " calls")));
            }

            b.Section("Research");
            b.Metrics(new List<EmailMetric>
            {
                new EmailMetric("Active topics", N(day.ActiveTopics)),
                new EmailMetric("New topics", N(day.NewTopics)),
                new EmailMetric("Runs completed", N(day.RunsCompleted), Change(day.RunsCompleted, before.RunsCompleted)),
                new EmailMetric("Runs failed / skipped", N(day.RunsFailed) + " / " + N(day.RunsSkipped)),
                new EmailMetric("Average run time", day.AverageRunSeconds > 0 ? Math.Round(day.AverageRunSeconds).ToString("N0", CultureInfo.InvariantCulture) + " s" : "n/a"),
                new EmailMetric("New findings", N(day.NewFindings)),
                new EmailMetric("Briefings composed", N(day.BriefingsReady)),
                new EmailMetric("Stale topic prompts / kept / deleted", N(day.StalePrompts) + " / " + N(day.StaleKept) + " / " + N(day.StaleDeleted))
            });
            b.Section("Delivery and billing");
            b.Metrics(new List<EmailMetric>
            {
                new EmailMetric("Emails sent", N(day.EmailsSent), day.EmailsFailed > 0 ? N(day.EmailsFailed) + " failed" : null),
                new EmailMetric("Push devices", N(day.PushDevices), day.NewPushDevices > 0 ? "+" + N(day.NewPushDevices) + " new" : null),
                new EmailMetric("Paid subscriptions", N(day.PaidSubscriptions), day.NewPaidSubscriptions > 0 ? "+" + N(day.NewPaidSubscriptions) + " new" : null),
                new EmailMetric("System briefing subscriptions", N(day.CatalogSubscriptions))
            });
            b.Paragraph("Times are UTC. The day runs from " + day.FromUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " to " + day.ToUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + ". Costs are estimates from recorded model calls.");
        }

        private static string Headline(PlatformDailyStats d)
        {
            return N(d.DailyActiveUsers) + " active users, " + N(d.NewSignups) + " new sign-ups, " + Usd(d.ModelCostUsd) + " model cost";
        }

        private static string N(long n)
        {
            return n.ToString("N0", CultureInfo.InvariantCulture);
        }

        private static string Usd(decimal d)
        {
            return "$" + d.ToString("N2", CultureInfo.InvariantCulture);
        }

        private static string Minutes(double m)
        {
            return m < 1 && m > 0 ? "under 1 min" : Math.Round(m).ToString("N0", CultureInfo.InvariantCulture) + " min";
        }

        private static string? Change(long now, long before)
        {
            if (now == before) return "same as the day before";
            long diff = now - before;
            return (diff > 0 ? "+" : "") + diff.ToString("N0", CultureInfo.InvariantCulture) + " vs the day before";
        }
    }
}
