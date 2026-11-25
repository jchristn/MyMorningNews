namespace MyMorningNews.Core.Services
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
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Settings;
    using SyslogLogging;

    /// <summary>
    /// The one way the pipeline, briefings, audio, plan limits, and predictions tell a user that something happened.
    /// <see cref="NotifyAsync"/> fans one event out to email, push, and the in-app list according to the user's
    /// preferences (<see cref="NotificationPreferenceService"/>) and any further filter in the context. A failure on one
    /// channel is logged and never blocks the others or the caller. Thread safety: safe for concurrent use.
    /// </summary>
    public class NotificationService
    {
        #region Public-Members

        /// <summary>
        /// Notification preferences.
        /// </summary>
        public NotificationPreferenceService Preferences { get; }

        #endregion

        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly EmailService _Email;
        private readonly PushService _Push;
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private const string _Header = "[NotificationService] ";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="email">Email service.</param>
        /// <param name="push">Push service.</param>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public NotificationService(DatabaseDriverBase database, EmailService email, PushService push, ServerSettings settings, LoggingModule logging)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Email = email ?? throw new ArgumentNullException(nameof(email));
            _Push = push ?? throw new ArgumentNullException(nameof(push));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            Preferences = email.Preferences;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Tell a user that something happened, on every channel they turned on for that kind. Never throws except for
        /// cancellation and null arguments; channel failures are logged.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User the push and in-app notification are for. Email goes to the recipients the kind
        /// defines (a run digest goes to the topic's recipients, a briefing to its recipients or owner, the rest to this user).</param>
        /// <param name="kind">Kind.</param>
        /// <param name="context">What it is about.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>What was sent on each channel.</returns>
        /// <exception cref="ArgumentNullException">tenantId or userId is null or empty, or context is null.</exception>
        public async Task<NotificationResult> NotifyAsync(string tenantId, string userId, NotificationKindEnum kind, NotificationContext context, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));
            ArgumentNullException.ThrowIfNull(context);
            NotificationResult result = new NotificationResult();
            User? user = null;
            bool proceed = true;
            try
            {
                user = await _Database.Users.ReadAsync(tenantId, userId, token).ConfigureAwait(false);
                if (context.TopicName == null && context.Run != null) context.TopicName = (await _Database.Topics.ReadAsync(tenantId, context.Run.TopicId, token).ConfigureAwait(false))?.Name;
                proceed = await EmailAsync(user, kind, context, result, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _Logging.Warn(_Header + kind + " email for user " + userId + " failed: " + ex.Message);
            }

            if (user == null || !proceed) return result;
            PushMessage? message = Describe(kind, context);
            if (message == null) return result;

            try
            {
                if (context.PushAllowed && await Preferences.IsEnabledAsync(user, kind, NotificationChannelEnum.Push, token).ConfigureAwait(false))
                    result.PushAccepted = (await _Push.SendToUserAsync(tenantId, userId, message, token).ConfigureAwait(false)).Accepted;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _Logging.Warn(_Header + kind + " push for user " + userId + " failed: " + ex.Message);
            }

            try
            {
                if (await Preferences.IsEnabledAsync(user, kind, NotificationChannelEnum.InApp, token).ConfigureAwait(false))
                {
                    await _Database.Notifications.CreateAsync(InApp(tenantId, userId, kind, context, message), token).ConfigureAwait(false);
                    result.InApp = true;
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _Logging.Warn(_Header + kind + " in-app notification for user " + userId + " failed: " + ex.Message);
            }

            return result;
        }

        /// <summary>
        /// A user's preference matrix with whether each channel can deliver right now.
        /// </summary>
        /// <param name="user">User.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Preferences.</returns>
        /// <exception cref="ArgumentNullException">user is null.</exception>
        public async Task<NotificationPreferencesResponse> DescribeAsync(User user, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(user);
            string address = EmailService.Normalize(user.Email);
            List<EmailSuppression> rows = address.Length == 0 ? new List<EmailSuppression>()
                : (await _Database.EmailSuppressions.EnumerateAsync(new EmailSuppressionFilter { TenantId = user.TenantId, Recipient = address, PageSize = 500 }, token).ConfigureAwait(false)).Objects;
            long devices = await _Database.Devices.CountAsync(new DeviceFilter { TenantId = user.TenantId, UserId = user.Id, Active = true }, token).ConfigureAwait(false);

            NotificationUnavailableReasonEnum? email = null;
            if (address.Length == 0) email = NotificationUnavailableReasonEnum.NoEmailAddress;
            else if (!_Settings.Email.Enabled || _Settings.Email.Provider == EmailProviderEnum.Disabled) email = NotificationUnavailableReasonEnum.EmailNotConfigured;
            else if (user.SelfSignup && !user.EmailVerifiedUtc.HasValue && _Settings.Hosting.RequireEmailVerification) email = NotificationUnavailableReasonEnum.EmailNotVerified;
            else if (rows.Any(r => r.Category == EmailCategoryEnum.All)) email = NotificationUnavailableReasonEnum.EmailUnsubscribed;

            NotificationUnavailableReasonEnum? push = null;
            if (!_Settings.Push.Enabled) push = NotificationUnavailableReasonEnum.PushNotConfigured;
            else if (devices < 1) push = NotificationUnavailableReasonEnum.NoDevice;

            return new NotificationPreferencesResponse
            {
                Preferences = await Preferences.ReadAllAsync(user, token).ConfigureAwait(false),
                Channels = new List<NotificationChannelStatus>
                {
                    new NotificationChannelStatus { Channel = NotificationChannelEnum.Email, Available = !email.HasValue, Reason = email },
                    new NotificationChannelStatus { Channel = NotificationChannelEnum.Push, Available = !push.HasValue, Reason = push },
                    new NotificationChannelStatus { Channel = NotificationChannelEnum.InApp, Available = true }
                },
                Address = address,
                UnsubscribedTopicIds = rows.Where(r => r.Category == EmailCategoryEnum.RunEmails && r.TopicId != null).Select(r => r.TopicId!).Distinct().ToList()
            };
        }

        /// <summary>
        /// Save changed cells and topic unsubscribes for a user.
        /// </summary>
        /// <param name="user">User.</param>
        /// <param name="request">Changes.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The preferences after the change.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public async Task<NotificationPreferencesResponse> UpdateAsync(User user, NotificationPreferencesRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(user);
            ArgumentNullException.ThrowIfNull(request);
            foreach (NotificationPreferenceCell cell in request.Preferences ?? new List<NotificationPreferenceCell>())
            {
                if (cell == null) continue;
                await Preferences.SetAsync(user, cell.Kind, cell.Channel, cell.Enabled, token).ConfigureAwait(false);
            }

            string address = EmailService.Normalize(user.Email);
            if (address.Length > 0)
            {
                foreach (string topicId in (request.ResubscribeTopicIds ?? new List<string>()).Where(t => !String.IsNullOrWhiteSpace(t)).Distinct())
                    await _Email.SetSuppressedAsync(user.TenantId, address, EmailCategoryEnum.RunEmails, topicId.Trim(), false, "preferences", token).ConfigureAwait(false);
                foreach (string topicId in (request.UnsubscribeTopicIds ?? new List<string>()).Where(t => !String.IsNullOrWhiteSpace(t)).Distinct())
                {
                    Topic? topic = await _Database.Topics.ReadAsync(user.TenantId, topicId.Trim(), token).ConfigureAwait(false);
                    if (topic != null) await _Email.SetSuppressedAsync(user.TenantId, address, EmailCategoryEnum.RunEmails, topic.Id, true, "preferences", token).ConfigureAwait(false);
                }
            }

            return await DescribeAsync(user, token).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        // Queues the email for a kind. Returns false when the event is a repeat that must not notify again on any
        // channel (a plan limit notice already sent this month).
        private async Task<bool> EmailAsync(User? user, NotificationKindEnum kind, NotificationContext c, NotificationResult result, CancellationToken token)
        {
            string app = _Settings.Rest.AppBaseUrl;
            switch (kind)
            {
                case NotificationKindEnum.RunResults:
                    if (c.Run != null) result.EmailsQueued = await _Email.QueueRunDigestAsync(c.Run, token).ConfigureAwait(false);
                    return true;

                case NotificationKindEnum.BriefingReady:
                    if (c.EmailAllowed && c.Briefing != null) result.EmailsQueued = await _Email.QueueBriefingAsync(c.Briefing, c.EmailRecipients, token).ConfigureAwait(false);
                    return true;

                case NotificationKindEnum.CapReached:
                    if (user == null || c.Cap == null) return false;
                    string period = DateTime.UtcNow.ToString("yyyyMM", CultureInfo.InvariantCulture);
                    EmailMessage? sent = await _Email.SendAccountEmailAsync(user, EmailTemplateEnum.CapReached, "A scheduled run was skipped",
                        new List<string> { c.Cap.Message, "Your topic \"" + (c.TopicName ?? "your topic") + "\" will run again when the limit resets. You can see your usage in Account settings." },
                        new EmailLink("See my usage", app + "/dashboard/profile"),
                        "cap:" + user.Id + ":" + c.Cap.Key + ":" + period, token, NotificationKindEnum.CapReached).ConfigureAwait(false);
                    if (sent != null && sent.Status == EmailStatusEnum.Queued) result.EmailsQueued = 1;
                    return sent != null;

                case NotificationKindEnum.AudioReady:
                    if (user == null || !c.EmailAllowed || c.Media == null || c.Briefing == null || !await WantsEmailAsync(user, kind, token).ConfigureAwait(false)) return true;
                    string briefingUrl = app + "/dashboard/briefings/" + c.Briefing.Id;
                    if (await _Email.QueueNoticeAsync(user, new EmailNotice
                    {
                        Template = EmailTemplateEnum.AudioReady,
                        Category = EmailCategoryEnum.AudioEmails,
                        Key = "audio:" + c.Media.Id,
                        BriefingId = c.Briefing.Id,
                        TopicId = c.Briefing.TopicId,
                        Title = c.Briefing.Title,
                        Subtitle = "Audio" + Duration(c.Media.DurationSeconds),
                        Paragraphs = new List<string> { "The audio version of this briefing is ready to play." },
                        Button = new EmailLink("Listen", briefingUrl + "?listen=1"),
                        Links = new List<EmailLink> { new EmailLink("Read the briefing", briefingUrl) },
                        Note = "You get this email because audio ready email is on in your notification preferences."
                    }, token).ConfigureAwait(false)) result.EmailsQueued = 1;
                    return true;

                case NotificationKindEnum.RunFailed:
                    if (user == null || !c.EmailAllowed || c.Run == null || !await WantsEmailAsync(user, kind, token).ConfigureAwait(false)) return true;
                    if (await _Email.QueueNoticeAsync(user, new EmailNotice
                    {
                        Template = EmailTemplateEnum.RunFailed,
                        Category = EmailCategoryEnum.FailureEmails,
                        Key = "runfailed:" + c.Run.Id,
                        RunId = c.Run.Id,
                        TopicId = c.Run.TopicId,
                        Title = "A research run failed",
                        Subtitle = c.TopicName,
                        Paragraphs = new List<string> { (c.TopicName != null ? "The latest research run for \"" + c.TopicName + "\"" : "One of your research runs") + " could not finish. Open the run to see what went wrong; the next scheduled run will try again." },
                        Button = new EmailLink("View this run", app + "/dashboard/runs?id=" + c.Run.Id),
                        Note = "You get this email because run failure email is on in your notification preferences."
                    }, token).ConfigureAwait(false)) result.EmailsQueued = 1;
                    return true;

                case NotificationKindEnum.PredictionDue:
                    if (user == null || !c.EmailAllowed || !await WantsEmailAsync(user, kind, token).ConfigureAwait(false)) return true;
                    bool one = c.Prediction != null && c.PredictionCount <= 1;
                    if (!one && c.PredictionCount < 1) return true;
                    if (await _Email.QueueNoticeAsync(user, new EmailNotice
                    {
                        Template = EmailTemplateEnum.PredictionDue,
                        Category = EmailCategoryEnum.PredictionEmails,
                        Key = one ? "prediction:" + c.Prediction!.Id : "predictions:" + user.Id + ":" + (c.Prediction?.Id ?? DateTime.UtcNow.ToString("yyyyMMddHH", CultureInfo.InvariantCulture)),
                        TopicId = one ? c.Prediction!.TopicId : null,
                        Title = one ? "A prediction is due" : c.PredictionCount + " predictions are due",
                        Paragraphs = one
                            ? new List<string> { c.Prediction!.Statement, "It reached its date. Check whether it came true." }
                            : new List<string> { "They reached their dates. Check whether they came true." },
                        Button = new EmailLink(one ? "Review this prediction" : "Review predictions", app + "/dashboard/predictions" + (one ? "?id=" + c.Prediction!.Id : String.Empty)),
                        Note = "You get this email because prediction email is on in your notification preferences."
                    }, token).ConfigureAwait(false)) result.EmailsQueued = 1;
                    return true;

                case NotificationKindEnum.TopicStale:
                    if (user == null || c.Review == null || !c.EmailAllowed || !await WantsEmailAsync(user, kind, token).ConfigureAwait(false)) return true;
                    if (await _Email.QueueNoticeAsync(user, StaleNotice(c, app), token).ConfigureAwait(false)) result.EmailsQueued = 1;
                    return true;

                default:
                    return true;
            }
        }

        // The keep-or-delete email: the prompt, the reminder before the deadline, or the notice after deletion.
        private static EmailNotice StaleNotice(NotificationContext c, string app)
        {
            TopicReview r = c.Review!;
            string name = c.TopicName ?? "your topic";
            string deadline = r.DeleteAfterUtc.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);
            string since = r.LastNewFindingUtc.HasValue
                ? "has not found anything new since " + r.LastNewFindingUtc.Value.ToString("MMMM d", CultureInfo.InvariantCulture)
                : "has not found anything new in " + c.StaleDays + " days";
            string topicUrl = app + "/dashboard/topics/" + r.TopicId + "?review=" + r.Id;
            EmailNotice n = new EmailNotice
            {
                Template = c.ReviewNotice == TopicReviewNoticeEnum.Deleted ? EmailTemplateEnum.TopicDeleted : EmailTemplateEnum.TopicStale,
                Category = EmailCategoryEnum.TopicReviewEmails,
                Key = "topicreview:" + r.Id + ":" + c.ReviewNotice.ToString().ToLowerInvariant(),
                TopicId = c.ReviewNotice == TopicReviewNoticeEnum.Deleted ? null : r.TopicId,
                Note = "You get this email because topic review email is on in your notification preferences. The keep-or-delete choice is also shown on the topic in the app."
            };
            switch (c.ReviewNotice)
            {
                case TopicReviewNoticeEnum.Reminder:
                    n.Title = "Last chance to keep \"" + name + "\"";
                    n.Subtitle = "Deleted on " + deadline + " unless you keep it";
                    n.Paragraphs = new List<string>
                    {
                        "Your topic \"" + name + "\" " + since + ". We asked whether to keep it and have not heard back.",
                        "If you do not choose Keep by " + deadline + ", the topic is deleted with its briefings, findings, and audio. Deleting cannot be undone."
                    };
                    n.Button = new EmailLink("Keep or delete", topicUrl);
                    break;

                case TopicReviewNoticeEnum.Deleted:
                    n.Title = "We deleted \"" + name + "\"";
                    n.Paragraphs = new List<string>
                    {
                        "Your topic \"" + name + "\" " + since + ", and nobody chose Keep by " + deadline + ", so it was deleted with its briefings, findings, and audio.",
                        "You can create it again at any time; the wizard takes a minute."
                    };
                    n.Button = new EmailLink("Create a topic", app + "/dashboard/topics/new");
                    break;

                default:
                    n.Title = "Keep \"" + name + "\"?";
                    n.Subtitle = "No new findings in " + c.StaleDays + " days";
                    n.Paragraphs = new List<string>
                    {
                        "Your topic \"" + name + "\" " + since + ". Research still runs on its schedule and counts toward your plan.",
                        "Keep it, or delete it. If you do not choose Keep by " + deadline + ", it is deleted automatically with its briefings, findings, and audio.",
                        "Tip: a broader brief or different sources often brings results back. You can refine the topic from the same page."
                    };
                    n.Button = new EmailLink("Keep or delete", topicUrl);
                    break;
            }

            return n;
        }

        // The push for a kind (also the text of the in-app notification), or null when there is nothing to say, for
        // example a run without new findings. Payloads carry identifiers and names, never content.
        private static PushMessage? Describe(NotificationKindEnum kind, NotificationContext c)
        {
            switch (kind)
            {
                case NotificationKindEnum.RunResults:
                    if (c.Run == null || c.Run.IsDryRun || c.Run.NewFindingCount < 1) return null;
                    return new PushMessage
                    {
                        Title = c.TopicName ?? "New findings",
                        Body = c.Run.NewFindingCount + (c.Run.NewFindingCount == 1 ? " new finding." : " new findings."),
                        ChannelId = "briefings",
                        Data = new Dictionary<string, string> { { "kind", "run.results" }, { "topicId", c.Run.TopicId }, { "runId", c.Run.Id } }
                    };

                case NotificationKindEnum.BriefingReady:
                    if (c.Briefing == null) return null;
                    Dictionary<string, string> ready = new Dictionary<string, string> { { "kind", "briefing.ready" }, { "briefingId", c.Briefing.Id } };
                    if (c.Edition != null) ready["editionName"] = c.Edition.Name;
                    return new PushMessage { Title = c.Edition?.Name ?? c.Briefing.Title, Body = "Your briefing is ready.", ChannelId = "briefings", Data = ready };

                case NotificationKindEnum.AudioReady:
                    if (c.Briefing == null || c.Media == null) return null;
                    return new PushMessage
                    {
                        Title = c.Briefing.Title,
                        Body = "The audio version is ready.",
                        ChannelId = "briefings",
                        Data = new Dictionary<string, string> { { "kind", "audio.ready" }, { "briefingId", c.Briefing.Id }, { "mediaAssetId", c.Media.Id } }
                    };

                case NotificationKindEnum.RunFailed:
                    if (c.Run == null) return null;
                    return new PushMessage
                    {
                        Title = "A research run failed",
                        Body = c.TopicName ?? "One of your topics could not finish its research.",
                        ChannelId = "account",
                        Data = new Dictionary<string, string> { { "kind", "run.failed" }, { "topicId", c.Run.TopicId }, { "runId", c.Run.Id } }
                    };

                case NotificationKindEnum.CapReached:
                    if (c.Cap == null) return null;
                    return new PushMessage
                    {
                        Title = "Scheduled run skipped",
                        Body = c.Cap.Message,
                        ChannelId = "account",
                        Data = new Dictionary<string, string> { { "kind", "cap.reached" }, { "key", c.Cap.Key } }
                    };

                case NotificationKindEnum.PredictionDue:
                    if (c.PredictionCount > 1)
                    {
                        return new PushMessage
                        {
                            Title = c.PredictionCount + " predictions are due",
                            Body = "Check whether they came true.",
                            ChannelId = "predictions",
                            Data = new Dictionary<string, string> { { "kind", "prediction.due" }, { "count", c.PredictionCount.ToString(CultureInfo.InvariantCulture) } }
                        };
                    }

                    if (c.Prediction == null) return null;
                    return new PushMessage
                    {
                        Title = "A prediction is due",
                        Body = "Check whether it came true.",
                        ChannelId = "predictions",
                        Data = new Dictionary<string, string> { { "kind", "prediction.due" }, { "predictionId", c.Prediction.Id } }
                    };

                case NotificationKindEnum.TopicStale:
                    if (c.Review == null) return null;
                    string topic = c.TopicName ?? "Your topic";
                    string title = c.ReviewNotice == TopicReviewNoticeEnum.Deleted ? "Topic deleted" : c.ReviewNotice == TopicReviewNoticeEnum.Reminder ? "Last chance to keep a topic" : "Keep this topic?";
                    string body = c.ReviewNotice == TopicReviewNoticeEnum.Deleted
                        ? topic + " had no new findings and was deleted."
                        : topic + " has had no new findings for " + c.StaleDays + " days. Keep it or it will be deleted on " + c.Review.DeleteAfterUtc.ToString("MMM d", CultureInfo.InvariantCulture) + ".";
                    Dictionary<string, string> data = new Dictionary<string, string> { { "kind", "topic.stale" }, { "reviewId", c.Review.Id }, { "notice", c.ReviewNotice.ToString() } };
                    if (c.ReviewNotice != TopicReviewNoticeEnum.Deleted) data["topicId"] = c.Review.TopicId;
                    return new PushMessage { Title = title, Body = body, ChannelId = "account", Data = data };

                default:
                    return null;
            }
        }

        // Notices are only written to the outbox for users who want them, so a kind that is off leaves no trace there.
        private async Task<bool> WantsEmailAsync(User user, NotificationKindEnum kind, CancellationToken token)
        {
            return await Preferences.IsEnabledAsync(user, kind, NotificationChannelEnum.Email, token).ConfigureAwait(false);
        }

        private static Notification InApp(string tenantId, string userId, NotificationKindEnum kind, NotificationContext c, PushMessage message)
        {
            // The in-app title is the thing the notification is about; the kind itself is shown by the client.
            string title = kind == NotificationKindEnum.RunFailed || kind == NotificationKindEnum.CapReached || kind == NotificationKindEnum.TopicStale ? (c.TopicName ?? message.Title) : message.Title;
            string body = kind == NotificationKindEnum.RunFailed ? "The research run could not finish." : message.Body;
            bool one = kind == NotificationKindEnum.PredictionDue && c.PredictionCount <= 1;
            return new Notification
            {
                TenantId = tenantId,
                UserId = userId,
                Kind = kind,
                Title = title.Length > 256 ? title.Substring(0, 256) : title,
                Body = body,
                TopicId = c.Run?.TopicId ?? c.Briefing?.TopicId ?? (one ? c.Prediction?.TopicId : null) ?? (c.Review != null && c.ReviewNotice != TopicReviewNoticeEnum.Deleted ? c.Review.TopicId : null),
                RunId = c.Run?.Id,
                BriefingId = c.Briefing?.Id,
                MediaAssetId = c.Media?.Id,
                PredictionId = one ? c.Prediction?.Id : null
            };
        }

        private static string Duration(double seconds)
        {
            if (seconds <= 0) return String.Empty;
            int minutes = (int)Math.Round(seconds / 60.0, MidpointRounding.AwayFromZero);
            return " · " + (minutes < 1 ? "under 1 min" : minutes + " min");
        }

        #endregion
    }
}
