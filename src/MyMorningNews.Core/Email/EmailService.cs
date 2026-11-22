namespace MyMorningNews.Core.Email
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Briefings;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Helpers;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Prompts;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Core.Telemetry;
    using SyslogLogging;

    /// <summary>
    /// Email after every run and briefing (plan section 23): builds per-recipient messages with detail links and
    /// per-item feedback tokens, honors suppressions, is idempotent per run or briefing and recipient, queues to the
    /// outbox, and delivers with retries and backoff. Queueing never throws to its caller. Thread safety: safe for
    /// concurrent use.
    /// </summary>
    public class EmailService
    {
        #region Public-Members

        /// <summary>
        /// Tokens.
        /// </summary>
        public EmailTokenService Tokens { get; }

        /// <summary>
        /// Notification preferences, read before any email in a category is queued.
        /// </summary>
        public NotificationPreferenceService Preferences { get; }

        /// <summary>
        /// Dashboard path of the notification preferences page, used by footer links. Default
        /// /dashboard/notifications. Never null or empty.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string PreferencesPath
        {
            get { return _PreferencesPath; }
            set { _PreferencesPath = String.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(PreferencesPath)) : value; }
        }

        #endregion

        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly ModelGateway _Models;
        private readonly PromptService _Prompts;
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private const string _Header = "[EmailService] ";
        private string _PreferencesPath = "/dashboard/notifications";
        private static readonly Regex _CountOnlySummary = new Regex(@"^\d+ new findings?\.$", RegexOptions.Compiled);

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="models">Model gateway (email lead paragraph).</param>
        /// <param name="prompts">Prompts.</param>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public EmailService(DatabaseDriverBase database, ModelGateway models, PromptService prompts, ServerSettings settings, LoggingModule logging)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Models = models ?? throw new ArgumentNullException(nameof(models));
            _Prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            Tokens = new EmailTokenService(database, settings);
            Preferences = new NotificationPreferenceService(database);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Queue the run digest to every recipient of the run's topic. Respects EmailOnRun, EmailWhenEmpty, dry runs, and
        /// suppressions. Never throws; failures are logged and return 0.
        /// </summary>
        /// <param name="run">Finished run.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Messages queued.</returns>
        public async Task<int> QueueRunDigestAsync(Run run, CancellationToken token = default)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(run);
                if (run.IsDryRun) return 0;
                Topic? topic = await _Database.Topics.ReadAsync(run.TenantId, run.TopicId, token).ConfigureAwait(false);
                if (topic == null || !topic.EmailOnRun) return 0;
                DigestContent content = await DigestAsync(run, topic, true, token).ConfigureAwait(false);
                if (content.Findings.Count == 0 && !topic.EmailWhenEmpty) return 0;
                int queued = 0;
                foreach (string recipient in await RecipientsAsync(topic, token).ConfigureAwait(false))
                    if (await QueueDigestToAsync(run, topic, content, recipient, token).ConfigureAwait(false)) queued++;
                return queued;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _Logging.Warn(_Header + "could not queue the digest for run " + run?.Id + ": " + ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Queue a briefing email (editions and one-time reports). Never throws.
        /// </summary>
        /// <param name="briefing">Ready briefing.</param>
        /// <param name="recipients">Recipients, or null for the owner.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Messages queued.</returns>
        public async Task<int> QueueBriefingAsync(Briefing briefing, IReadOnlyList<string>? recipients, CancellationToken token = default)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(briefing);
                if (briefing.IsDryRun || briefing.Status != BriefingStatusEnum.Ready) return 0;
                User? owner = await _Database.Users.ReadAsync(briefing.TenantId, briefing.UserId, token).ConfigureAwait(false);
                List<string> to = (recipients != null && recipients.Count > 0 ? recipients : new List<string> { owner?.Email ?? String.Empty })
                    .Select(Normalize).Where(r => r.Length > 0).Distinct(StringComparer.Ordinal).ToList();
                int queued = 0;
                foreach (string recipient in to)
                {
                    EmailMessage? m = await CreateAsync(briefing.TenantId, recipient, EmailTemplateEnum.BriefingPublished, null, briefing.Id, briefing.TopicId, "briefing:" + briefing.Id + ":" + recipient,
                        EmailCategoryEnum.BriefingEmails, briefing.TopicId, token).ConfigureAwait(false);
                    if (m == null || m.Status != EmailStatusEnum.Queued) continue;
                    // Feedback from any recipient is attributed to the owner, who owns the items being rated.
                    EmailPreview rendered = await RenderBriefingAsync(briefing, m, briefing.UserId, true, token).ConfigureAwait(false);
                    await FinishAsync(m, rendered, token).ConfigureAwait(false);
                    queued++;
                }

                return queued;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _Logging.Warn(_Header + "could not queue briefing " + briefing?.Id + ": " + ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Queue a test email to a user.
        /// </summary>
        /// <param name="user">User.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Queued message.</returns>
        /// <exception cref="ConflictException">The user has no email address.</exception>
        public async Task<EmailMessage> SendTestAsync(User user, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(user);
            string recipient = Normalize(user.Email);
            if (recipient.Length == 0) throw new ConflictException("Your account has no email address.");
            EmailMessage m = (await CreateAsync(user.TenantId, recipient, EmailTemplateEnum.Test, null, null, null, "test:" + Guid.NewGuid().ToString("N"), null, null, token).ConfigureAwait(false))!;
            EmailBuilder b = new EmailBuilder().Preheader("Email from My Morning News is working.")
                .Header(Logo(), "Test email", DateTime.UtcNow.ToString("MMMM d, yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture), new EmailLink("Open My Morning News", _Settings.Rest.AppBaseUrl + "/dashboard/today"))
                .Paragraph("If you can read this, email delivery works through the " + EmailProviderFactory.Create(_Settings.Email).Provider + " provider.")
                .Footer(new List<EmailLink> { new EmailLink("Email preferences", PreferencesUrl()) }, null);
            string subject = Subject("Test email");
            await FinishAsync(m, new EmailPreview { Recipient = recipient, Subject = subject, Html = b.Html(subject), Text = b.Text() }, token).ConfigureAwait(false);
            return m;
        }

        /// <summary>
        /// Queue a transactional account email (verification, welcome, password reset, password changed, deletion). These
        /// are never suppressed by email preferences.
        /// </summary>
        /// <param name="user">Recipient.</param>
        /// <param name="template">Template kind.</param>
        /// <param name="title">Heading and subject.</param>
        /// <param name="paragraphs">Body paragraphs.</param>
        /// <param name="button">Main action, or null.</param>
        /// <param name="key">Idempotency key, unique per message.</param>
        /// <param name="token">Cancellation token.</param>
        /// <param name="preference">Kind whose email preference decides whether this is sent, or null to always send. When the
        /// user turned that kind off, the message is recorded as Suppressed (so the key still counts as used) and not sent.</param>
        /// <returns>The message (Queued, or Suppressed by the preference), or null when one with the same key exists.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public async Task<EmailMessage?> SendAccountEmailAsync(User user, EmailTemplateEnum template, string title, IReadOnlyList<string> paragraphs, EmailLink? button, string key, CancellationToken token = default, NotificationKindEnum? preference = null)
        {
            ArgumentNullException.ThrowIfNull(user);
            ArgumentNullException.ThrowIfNull(title);
            ArgumentNullException.ThrowIfNull(paragraphs);
            ArgumentNullException.ThrowIfNull(key);
            string recipient = Normalize(user.Email);
            if (recipient.Length == 0) return null;
            EmailMessage? m = await CreateAsync(user.TenantId, recipient, template, null, null, null, key, null, null, token, preference).ConfigureAwait(false);
            if (m == null) return null;
            if (m.Status == EmailStatusEnum.Suppressed) return m;
            EmailBuilder b = new EmailBuilder().Preheader(paragraphs.FirstOrDefault()).Header(Logo(), title, null, button);
            foreach (string p in paragraphs) b.Paragraph(p);
            b.Footer(new List<EmailLink> { new EmailLink("Support", "https://mymorningnews.ai/support.html") }, "You get this email because of activity on your My Morning News account.");
            string subject = Subject(title);
            await FinishAsync(m, new EmailPreview { Recipient = recipient, Subject = subject, Html = b.Html(subject), Text = b.Text() }, token).ConfigureAwait(false);
            return m;
        }

        /// <summary>
        /// Queue a short notice email to a user (audio ready, run failed, prediction due). Honors the user's
        /// notification preference for the notice's category and unsubscribes, and is idempotent per key and recipient.
        /// Never throws; failures are logged and return false.
        /// </summary>
        /// <param name="user">Recipient.</param>
        /// <param name="notice">Notice.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when a message was queued.</returns>
        public async Task<bool> QueueNoticeAsync(User user, EmailNotice notice, CancellationToken token = default)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(user);
                ArgumentNullException.ThrowIfNull(notice);
                string recipient = Normalize(user.Email);
                if (recipient.Length == 0) return false;
                EmailMessage? m = await CreateAsync(user.TenantId, recipient, notice.Template, notice.RunId, notice.BriefingId, notice.TopicId, notice.Key + ":" + recipient, notice.Category, null, token).ConfigureAwait(false);
                if (m == null || m.Status != EmailStatusEnum.Queued) return false;
                EmailBuilder b = new EmailBuilder().Preheader(notice.Paragraphs.FirstOrDefault() ?? notice.Title).Header(Logo(), notice.Title, notice.Subtitle, notice.Button);
                foreach (string p in notice.Paragraphs) b.Paragraph(p);
                List<EmailLink> footer = new List<EmailLink>(notice.Links) { new EmailLink("Notification preferences", PreferencesUrl()) };
                EmailLink? unsubscribe = await UnsubscribeAsync(m, user.Id, notice.Category, null, true, token).ConfigureAwait(false);
                if (unsubscribe != null) footer.Add(unsubscribe);
                b.Footer(footer, notice.Note);
                string subject = Subject(notice.Title);
                await FinishAsync(m, new EmailPreview { Recipient = recipient, Subject = subject, Html = b.Html(subject), Text = b.Text() }, token).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _Logging.Warn(_Header + "could not queue the " + notice?.Template + " notice " + notice?.Key + ": " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Render the digest for a run as it would be sent to its owner, with sample links. Nothing is saved. The lead
        /// paragraph comes from the stored run summary unless <paramref name="summarize"/> is true, in which case it is
        /// written by the model as in a real send (one model call).
        /// </summary>
        /// <param name="run">Run.</param>
        /// <param name="summarize">Write the lead with the model. Default false (no model call).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Preview.</returns>
        /// <exception cref="KeyNotFoundException">The topic is gone.</exception>
        public async Task<EmailPreview> PreviewRunDigestAsync(Run run, bool summarize = false, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(run);
            Topic topic = await _Database.Topics.ReadAsync(run.TenantId, run.TopicId, token).ConfigureAwait(false) ?? throw new KeyNotFoundException("The run's topic no longer exists.");
            DigestContent content = await DigestAsync(run, topic, summarize, token).ConfigureAwait(false);
            User? owner = await _Database.Users.ReadAsync(run.TenantId, topic.UserId, token).ConfigureAwait(false);
            EmailMessage sample = new EmailMessage { TenantId = run.TenantId, Recipient = Normalize(owner?.Email ?? "preview@example.test"), Template = EmailTemplateEnum.RunDigest, Subject = "preview", IdempotencyKey = "preview" };
            return await RenderDigestAsync(run, topic, content, sample, topic.UserId, false, token).ConfigureAwait(false);
        }

        /// <summary>
        /// One delivery pass: recover abandoned claims, send due messages, and schedule retries with exponential backoff.
        /// </summary>
        /// <param name="nowUtc">Now.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Messages attempted.</returns>
        public async Task<int> DeliverDueAsync(DateTime nowUtc, CancellationToken token = default)
        {
            await _Database.EmailMessages.RecoverAbandonedAsync(nowUtc, token).ConfigureAwait(false);
            List<EmailMessage> due = await _Database.EmailMessages.ClaimDueAsync(nowUtc, nowUtc.AddMinutes(5), 50, token).ConfigureAwait(false);
            IEmailProvider provider = EmailProviderFactory.Create(_Settings.Email);
            foreach (EmailMessage m in due)
            {
                token.ThrowIfCancellationRequested();
                m.Provider = provider.Provider;
                m.LockedUntilUtc = null;
                if (provider.Provider == EmailProviderEnum.Disabled)
                {
                    m.Status = EmailStatusEnum.Skipped;
                    m.LastError = "Email is disabled.";
                }
                else
                {
                    EmailSendResult r = await provider.SendAsync(m, token).ConfigureAwait(false);
                    if (r.Success)
                    {
                        m.Status = EmailStatusEnum.Sent;
                        m.ProviderMessageId = r.ProviderMessageId;
                        m.SentUtc = DateTime.UtcNow;
                        m.LastError = null;
                    }
                    else if (r.Permanent || m.Attempts >= _Settings.Email.DeliveryMaxAttempts)
                    {
                        m.Status = EmailStatusEnum.Failed;
                        m.FailedUtc = DateTime.UtcNow;
                        m.LastError = r.Error;
                    }
                    else
                    {
                        m.Status = EmailStatusEnum.Queued;
                        m.NextAttemptUtc = nowUtc.AddMinutes(Math.Pow(2, Math.Max(0, m.Attempts - 1)));
                        m.LastError = r.Error;
                    }
                }

                await _Database.EmailMessages.UpdateAsync(m, CancellationToken.None).ConfigureAwait(false);
                MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.EmailMessages, "template", m.Template.ToString(), "provider", m.Provider.ToString(), "outcome", m.Status.ToString().ToLowerInvariant());
            }

            return due.Count;
        }

        /// <summary>
        /// Whether a recipient opted out of a category (for a topic, or for every topic, or of all email). For a
        /// recipient who is a user of the tenant, the account-wide answer is that user's notification preference for the
        /// category's kind on the email channel; other recipients are remembered by address.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="recipient">Address.</param>
        /// <param name="category">Category.</param>
        /// <param name="topicId">Topic, or null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when suppressed.</returns>
        public async Task<bool> IsSuppressedAsync(string tenantId, string recipient, EmailCategoryEnum category, string? topicId, CancellationToken token = default)
        {
            string address = Normalize(recipient);
            List<EmailSuppression> rows = (await _Database.EmailSuppressions.EnumerateAsync(new EmailSuppressionFilter { TenantId = tenantId, Recipient = address, PageSize = 500 }, token).ConfigureAwait(false)).Items;
            if (rows.Any(r => r.Category == EmailCategoryEnum.All || (r.Category == category && r.TopicId != null && r.TopicId == topicId))) return true;
            NotificationKindEnum? kind = NotificationPreferenceService.KindFor(category);
            User? user = kind.HasValue ? await UserByAddressAsync(tenantId, address, token).ConfigureAwait(false) : null;
            if (user != null && kind.HasValue) return !await Preferences.IsEnabledAsync(user, kind.Value, NotificationChannelEnum.Email, token).ConfigureAwait(false);
            return rows.Any(r => r.Category == category && r.TopicId == null);
        }

        /// <summary>
        /// Opt a recipient out of (or back into) a category, optionally for one topic. An account-wide choice by a user
        /// of the tenant is saved as that user's notification preference (the same cell the preferences matrix shows);
        /// topic choices, choices by other recipients, and <see cref="EmailCategoryEnum.All"/> are remembered by address.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="recipient">Address.</param>
        /// <param name="category">Category.</param>
        /// <param name="topicId">Topic, or null for every topic.</param>
        /// <param name="suppressed">Opt out (true) or back in (false).</param>
        /// <param name="source">How it was set.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task SetSuppressedAsync(string tenantId, string recipient, EmailCategoryEnum category, string? topicId, bool suppressed, string source, CancellationToken token = default)
        {
            string r = Normalize(recipient);
            NotificationKindEnum? kind = topicId == null ? NotificationPreferenceService.KindFor(category) : null;
            User? user = kind.HasValue ? await UserByAddressAsync(tenantId, r, token).ConfigureAwait(false) : null;
            if (user != null && kind.HasValue)
            {
                await Preferences.SetAsync(user, kind.Value, NotificationChannelEnum.Email, !suppressed, token).ConfigureAwait(false);
                return;
            }

            List<EmailSuppression> rows = (await _Database.EmailSuppressions.EnumerateAsync(new EmailSuppressionFilter { TenantId = tenantId, Recipient = r, PageSize = 500 }, token).ConfigureAwait(false)).Items
                .Where(x => x.Category == category && x.TopicId == topicId).ToList();
            if (suppressed && rows.Count == 0)
                await _Database.EmailSuppressions.CreateAsync(new EmailSuppression { TenantId = tenantId, Recipient = r, Category = category, TopicId = topicId, Source = source }, token).ConfigureAwait(false);
            if (!suppressed)
                foreach (EmailSuppression x in rows) await _Database.EmailSuppressions.DeleteAsync(tenantId, x.Id, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Lowercase, trimmed address.
        /// </summary>
        /// <param name="address">Address.</param>
        /// <returns>Normalized address, or empty.</returns>
        public static string Normalize(string? address)
        {
            string a = (address ?? String.Empty).Trim().ToLowerInvariant();
            return a.Contains('@') && !a.Contains(' ') && a.Length <= 320 ? a : String.Empty;
        }

        #endregion

        #region Private-Methods

        private async Task<bool> QueueDigestToAsync(Run run, Topic topic, DigestContent content, string recipient, CancellationToken token)
        {
            EmailMessage? m = await CreateAsync(run.TenantId, recipient, EmailTemplateEnum.RunDigest, run.Id, null, topic.Id, "run:" + run.Id + ":" + recipient, EmailCategoryEnum.RunEmails, topic.Id, token).ConfigureAwait(false);
            if (m == null || m.Status != EmailStatusEnum.Queued) return false;
            // Feedback from any recipient is attributed to the topic owner, who owns the items being rated.
            EmailPreview rendered = await RenderDigestAsync(run, topic, content, m, topic.UserId, true, token).ConfigureAwait(false);
            await FinishAsync(m, rendered, token).ConfigureAwait(false);
            return true;
        }

        private async Task<EmailMessage?> CreateAsync(string tenantId, string recipient, EmailTemplateEnum template, string? runId, string? briefingId, string? topicId, string key, EmailCategoryEnum? category, string? suppressionTopicId, CancellationToken token, NotificationKindEnum? preference = null)
        {
            EmailMessage? existing = (await _Database.EmailMessages.EnumerateAsync(new EmailMessageFilter { TenantId = tenantId, Recipient = recipient, RunId = runId, BriefingId = briefingId, Template = template, PageSize = 50 }, token).ConfigureAwait(false)).Items
                .FirstOrDefault(x => x.IdempotencyKey == key);
            if (existing != null) return null;
            User? user = (await _Database.Users.EnumerateAsync(new UserFilter { TenantId = tenantId, Email = recipient, PageSize = 1 }, token).ConfigureAwait(false)).Items.FirstOrDefault();
            bool suppressed = category.HasValue && await IsSuppressedAsync(tenantId, recipient, category.Value, suppressionTopicId, token).ConfigureAwait(false);
            if (!suppressed && preference.HasValue && user != null) suppressed = !await Preferences.IsEnabledAsync(user, preference.Value, NotificationChannelEnum.Email, token).ConfigureAwait(false);
            EmailMessage m = new EmailMessage
            {
                TenantId = tenantId,
                UserId = user?.Id,
                Template = template,
                RunId = runId,
                BriefingId = briefingId,
                TopicId = topicId,
                Recipient = recipient,
                Subject = "(rendering)",
                Status = suppressed ? EmailStatusEnum.Suppressed : (_Settings.Email.Enabled ? EmailStatusEnum.Sending : EmailStatusEnum.Skipped),
                Provider = _Settings.Email.Provider,
                IdempotencyKey = key
            };
            try
            {
                m = await _Database.EmailMessages.CreateAsync(m, token).ConfigureAwait(false);
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
            {
                return null;
            }

            if (m.Status != EmailStatusEnum.Sending)
            {
                MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.EmailMessages, "template", template.ToString(), "provider", m.Provider.ToString(), "outcome", m.Status.ToString().ToLowerInvariant());
                return m;
            }

            m.Status = EmailStatusEnum.Queued;
            return m;
        }

        private async Task FinishAsync(EmailMessage m, EmailPreview rendered, CancellationToken token)
        {
            m.Subject = rendered.Subject.Length > 512 ? rendered.Subject.Substring(0, 512) : rendered.Subject;
            m.RenderedHtml = rendered.Html;
            m.RenderedText = rendered.Text;
            m.Status = EmailStatusEnum.Queued;
            m.NextAttemptUtc = DateTime.UtcNow;
            await _Database.EmailMessages.UpdateAsync(m, token).ConfigureAwait(false);
        }

        private async Task<DigestContent> DigestAsync(Run run, Topic topic, bool writeLead, CancellationToken token)
        {
            DigestContent c = new DigestContent();
            c.Findings = (await _Database.Findings.EnumerateAsync(new FindingFilter { TenantId = run.TenantId, RunId = run.Id, IsDuplicate = false, Hidden = false, ExcludeTier = FindingTierEnum.BelowThreshold, PageSize = 200 }, token).ConfigureAwait(false)).Items
                .OrderBy(f => f.Rank <= 0 ? int.MaxValue : f.Rank).ThenByDescending(f => f.RelevanceScore).Take(topic.MaxEmailFindings).ToList();
            List<Prediction> preds = (await _Database.Predictions.EnumerateAsync(new PredictionFilter { TenantId = run.TenantId, TopicId = topic.Id, IsDryRun = false, PageSize = 500 }, token).ConfigureAwait(false)).Items;
            c.NewPredictions = preds.Where(p => p.RunId == run.Id && p.Status == PredictionStatusEnum.Open).OrderByDescending(p => p.Confidence).Take(5).ToList();
            c.ToGrade = preds.Where(p => p.Status == PredictionStatusEnum.ProposedResolved || (p.Status == PredictionStatusEnum.Open && p.HorizonUtc <= DateTime.UtcNow)).OrderBy(p => p.HorizonUtc).Take(5).ToList();
            foreach (Prediction p in c.ToGrade.Where(p => p.Status == PredictionStatusEnum.ProposedResolved))
            {
                PredictionReview? review = (await _Database.PredictionReviews.EnumerateAsync(new PredictionReviewFilter { TenantId = run.TenantId, PredictionId = p.Id, PageSize = 10 }, token).ConfigureAwait(false)).Items
                    .OrderByDescending(r => r.CreatedUtc).FirstOrDefault();
                if (review != null) c.Reviews[p.Id] = review;
            }

            c.Suggestions = (await _Database.Suggestions.EnumerateAsync(new SuggestionFilter { TenantId = run.TenantId, TopicId = topic.Id, Status = SuggestionStatusEnum.New, IsDryRun = false, PageSize = 50 }, token).ConfigureAwait(false)).Items
                .Where(s => s.RunId == run.Id).OrderBy(s => (int)s.Priority).Take(5).ToList();
            foreach (Finding f in c.Findings.Where(f => f.SourceItemId != null))
            {
                SourceItem? s = await _Database.SourceItems.ReadAsync(run.TenantId, f.SourceItemId!, token).ConfigureAwait(false);
                if (s != null) c.Sources[f.Id] = s;
            }

            c.Proposals = (await _Database.LearnedPreferences.EnumerateAsync(new LearnedPreferenceFilter { TenantId = run.TenantId, UserId = topic.UserId, Status = PreferenceStatusEnum.Proposed, PageSize = 20 }, token).ConfigureAwait(false)).Items
                .Where(p => p.TopicId == null || p.TopicId == topic.Id).OrderByDescending(p => p.Confidence).Take(3).ToList();
            c.Lead = writeLead ? await LeadAsync(run, topic, c, token).ConfigureAwait(false) : FallbackLead(run, c);
            return c;
        }

        private static string FallbackLead(Run run, DigestContent c)
        {
            string counted = c.Findings.Count == 0 ? "Nothing new this run." : c.Findings.Count + (c.Findings.Count == 1 ? " new finding." : " new findings.");
            string? first = FirstParagraph(run.Summary);
            // The pipeline's stand-in summary is only a count of every new finding; say what this email shows instead.
            if (first == null || _CountOnlySummary.IsMatch(first)) return counted;
            return first;
        }

        private async Task<TimeZoneInfo> ReaderZoneAsync(string tenantId, string recipient, Topic topic, CancellationToken token)
        {
            // The reader's own profile zone when the recipient is a user of the tenant, then the topic owner's, then the
            // zone the topic is scheduled in, then the tenant's; UTC when none is set.
            string address = Normalize(recipient);
            if (address.Length > 0)
            {
                User? reader = (await _Database.Users.ReadAllByEmailAsync(address, token).ConfigureAwait(false)).FirstOrDefault(u => u.TenantId == tenantId);
                TimeZoneInfo? own = TimeZoneHelper.TryFind(reader?.TimeZone);
                if (own != null) return own;
            }

            User? owner = await _Database.Users.ReadAsync(tenantId, topic.UserId, token).ConfigureAwait(false);
            TimeZoneInfo? ownerZone = TimeZoneHelper.TryFind(owner?.TimeZone);
            if (ownerZone != null) return ownerZone;
            Schedule? schedule = (await _Database.Schedules.EnumerateAsync(new ScheduleFilter { TenantId = tenantId, OwnerType = ScheduleOwnerTypeEnum.Topic, OwnerId = topic.Id, PageSize = 10 }, token).ConfigureAwait(false)).Items
                .FirstOrDefault(x => !String.IsNullOrWhiteSpace(x.TimeZone));
            TimeZoneInfo? scheduleZone = TimeZoneHelper.TryFind(schedule?.TimeZone);
            if (scheduleZone != null) return scheduleZone;
            Tenant? tenant = await _Database.Tenants.ReadAsync(tenantId, token).ConfigureAwait(false);
            return TimeZoneHelper.TryFind(tenant?.TimeZone) ?? TimeZoneInfo.Utc;
        }

        private async Task<string> LeadAsync(Run run, Topic topic, DigestContent c, CancellationToken token)
        {
            string fallback = FallbackLead(run, c);
            if (c.Findings.Count == 0) return fallback;
            try
            {
                ResolvedPrompt prompt = await _Prompts.ResolveAsync(run.TenantId, "email.run-summary", topic.Id, token).ConfigureAwait(false);
                RenderedPrompt rendered = PromptService.Render(prompt.Definition, new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    { "topic.name", topic.Name },
                    { "run.summary", run.Summary },
                    { "findings.top", c.Findings.Take(5).Select(f => f.Title + ": " + (f.Summary ?? String.Empty)).ToList() }
                });
                ModelCallResult r = await _Models.ChatAsync(new ModelCallRequest
                {
                    TenantId = run.TenantId, UserId = topic.UserId, TopicId = topic.Id, RunId = run.Id, Task = prompt.Definition.Role, PromptKey = "email.run-summary",
                    PromptTemplateVersionId = prompt.Version.Id, SystemPrompt = rendered.System, UserPrompt = rendered.User
                }, token).ConfigureAwait(false);
                string text = SafeMarkdown.ToText(r.Text).Trim();
                return text.Length >= 20 ? text : fallback;
            }
            catch (Exception ex) when (ex is ModelCallException || ex is StructuredOutputException)
            {
                return fallback;
            }
        }

        private async Task<EmailPreview> RenderDigestAsync(Run run, Topic topic, DigestContent c, EmailMessage m, string userId, bool issueTokens, CancellationToken token)
        {
            string app = _Settings.Rest.AppBaseUrl;
            DateTime finishedUtc = DateTime.SpecifyKind(run.CompletedUtc ?? DateTime.UtcNow, DateTimeKind.Utc);
            TimeZoneInfo zone = await ReaderZoneAsync(run.TenantId, m.Recipient, topic, token).ConfigureAwait(false);
            string when = TimeZoneInfo.ConvertTimeFromUtc(finishedUtc, zone).ToString("dddd, MMMM d, h:mm tt", CultureInfo.InvariantCulture) + " " + TimeZoneHelper.Abbreviation(zone, finishedUtc);
            // The subject and the lead count the findings this email shows (owner decision for UT-030).
            int mustRead = c.Findings.Count(f => f.Tier == FindingTierEnum.MustRead);
            string subject = c.Findings.Count == 0 ? Subject(topic.Name + ": nothing significant this run") : Subject(topic.Name + ": " + c.Findings.Count + " new (" + mustRead + " must-read)");
            EmailBuilder b = new EmailBuilder().Preheader(c.Findings.FirstOrDefault()?.Title ?? "Nothing significant this run.")
                .Header(Logo(), topic.Name, when, new EmailLink("View this run", app + "/dashboard/runs?id=" + run.Id))
                .Paragraph(c.Lead);
            if (run.NewFindingCount > c.Findings.Count && c.Findings.Count > 0)
                b.Paragraph("This email shows the top " + c.Findings.Count + " of " + run.NewFindingCount + " new findings. The rest are in the full run.");

            if (c.Findings.Count > 0) b.Section("Top findings");
            int rank = 1;
            foreach (Finding f in c.Findings)
            {
                List<EmailLink> chips = new List<EmailLink>();
                if (c.Sources.TryGetValue(f.Id, out SourceItem? s))
                    chips.Add(new EmailLink((s.Domain ?? "source") + (s.PublishedUtc.HasValue ? ", " + s.PublishedUtc.Value.ToString("MMM d", CultureInfo.InvariantCulture) : String.Empty), s.CanonicalUrl));
                List<EmailLink> actions = await ActionsAsync(m, userId, FeedbackTargetTypeEnum.Finding, f.Id, topic.Id, EmailActions.Finding, issueTokens, token).ConfigureAwait(false);
                b.Item((rank++) + ". " + TierLabel(f.Tier) + " · " + f.RelevanceScore.ToString("0", CultureInfo.InvariantCulture), f.Title, app + "/dashboard/findings/" + f.Id,
                    new[] { f.Summary ?? String.Empty, String.IsNullOrWhiteSpace(f.WhyItMatters) ? String.Empty : "Why it matters: " + f.WhyItMatters }, chips, actions);
            }

            if (c.NewPredictions.Count > 0) b.Section("Where it might go");
            foreach (Prediction p in c.NewPredictions)
            {
                List<EmailLink> actions = await ActionsAsync(m, userId, FeedbackTargetTypeEnum.Prediction, p.Id, topic.Id, EmailActions.PredictionNew, issueTokens, token).ConfigureAwait(false);
                b.Item(Pct(p.Confidence) + " by " + p.HorizonUtc.ToString("MMM d, yyyy", CultureInfo.InvariantCulture), p.Statement, app + "/dashboard/predictions?id=" + p.Id, new[] { p.Rationale ?? String.Empty }, Array.Empty<EmailLink>(), actions);
            }

            if (c.ToGrade.Count > 0) b.Section("Predictions to grade");
            foreach (Prediction p in c.ToGrade)
            {
                List<EmailLink> actions = await ActionsAsync(m, userId, FeedbackTargetTypeEnum.Prediction, p.Id, topic.Id, EmailActions.PredictionGrade, issueTokens, token).ConfigureAwait(false);
                c.Reviews.TryGetValue(p.Id, out PredictionReview? review);
                string badge = review != null ? "Proposed: " + review.ProposedStatus : "Due " + p.HorizonUtc.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
                b.Item(badge, p.Statement, app + "/dashboard/predictions?id=" + p.Id, new[] { review?.Rationale ?? String.Empty }, Array.Empty<EmailLink>(), actions);
            }

            if (c.Suggestions.Count > 0) b.Section("Dig into next");
            foreach (Suggestion s in c.Suggestions)
            {
                List<EmailLink> actions = await ActionsAsync(m, userId, FeedbackTargetTypeEnum.Suggestion, s.Id, topic.Id, EmailActions.Suggestion, issueTokens, token).ConfigureAwait(false);
                b.Item(s.Priority + " priority · " + s.Effort, s.Title, app + "/dashboard/suggestions?id=" + s.Id, new[] { s.Description ?? String.Empty }, Array.Empty<EmailLink>(), actions);
            }

            if (c.Proposals.Count > 0) b.Section("What we learned from your feedback");
            foreach (LearnedPreference p in c.Proposals)
            {
                List<EmailLink> actions = await ActionsAsync(m, userId, FeedbackTargetTypeEnum.LearnedPreference, p.Id, topic.Id, EmailActions.Preference, issueTokens, token).ConfigureAwait(false);
                b.Item(MyMorningNews.Core.Feedback.LearningService.Label(p.Kind) + " · from " + p.SupportCount + (p.SupportCount == 1 ? " rating" : " ratings"), p.Statement, app + "/dashboard/insights/preferences?id=" + p.Id,
                    new[] { "Apply it and future runs will follow it. Dismiss it and we will not suggest it again." }, Array.Empty<EmailLink>(), actions);
            }

            List<EmailLink> footer = new List<EmailLink>
            {
                new EmailLink("Full run", app + "/dashboard/runs?id=" + run.Id),
                new EmailLink("Today's briefing", app + "/dashboard/today"),
                new EmailLink("Email preferences", PreferencesUrl())
            };
            EmailLink? unsubscribe = await UnsubscribeAsync(m, userId, EmailCategoryEnum.RunEmails, topic.Id, issueTokens, token).ConfigureAwait(false);
            if (unsubscribe != null) footer.Add(unsubscribe);
            b.Footer(footer, "Rating links open a short page where you confirm your rating. You get this email because you follow " + topic.Name + " in My Morning News.");
            return new EmailPreview { Recipient = m.Recipient, Subject = subject, Html = b.Html(subject), Text = b.Text() };
        }

        private async Task<EmailPreview> RenderBriefingAsync(Briefing br, EmailMessage m, string userId, bool issueTokens, CancellationToken token)
        {
            string app = _Settings.Rest.AppBaseUrl;
            List<BriefingSection> sections = (await _Database.BriefingSections.EnumerateAsync(new BriefingSectionFilter { TenantId = br.TenantId, BriefingId = br.Id, PageSize = 100 }, token).ConfigureAwait(false)).Items
                .OrderBy(s => s.SortOrder).ToList();
            List<BriefingItem> items = (await _Database.BriefingItems.EnumerateAsync(new BriefingItemFilter { TenantId = br.TenantId, BriefingId = br.Id, PageSize = 1000 }, token).ConfigureAwait(false)).Items;
            string subject = Subject(br.Title);
            string opening = sections.FirstOrDefault(s => s.Source == "opening")?.Markdown ?? String.Empty;
            EmailBuilder b = new EmailBuilder().Preheader(FirstParagraph(SafeMarkdown.ToText(opening)) ?? br.Title)
                .Header(Logo(), br.Title, (br.ComposedUtc ?? DateTime.UtcNow).ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture) + " · " + br.ReadMinutes + " min read",
                    new EmailLink("Read the full briefing", app + "/dashboard/briefings/" + br.Id))
                .Markdown(opening);
            if (br.MediaAssetId != null) b.Button(new EmailLink("Listen", app + "/dashboard/briefings/" + br.Id + "?listen=1"));
            foreach (BriefingSection sec in sections.Where(s => s.Source != "opening" && s.Source != "citations"))
            {
                b.Section(sec.Heading);
                List<BriefingItem> top = items.Where(i => i.SectionId == sec.Id && i.ItemType == BriefingItemTypeEnum.Finding).OrderBy(i => i.SortOrder).Take(3).ToList();
                if (top.Count == 0)
                {
                    b.Markdown(Truncate(sec.Markdown ?? String.Empty, 1500));
                    continue;
                }

                foreach (BriefingItem i in top)
                {
                    Finding? f = await _Database.Findings.ReadAsync(br.TenantId, i.ItemId, token).ConfigureAwait(false);
                    if (f == null) continue;
                    List<EmailLink> actions = await ActionsAsync(m, userId, FeedbackTargetTypeEnum.Finding, f.Id, f.TopicId, EmailActions.Finding, issueTokens, token).ConfigureAwait(false);
                    b.Item(TierLabel(f.Tier), f.Title, app + "/dashboard/findings/" + f.Id, new[] { f.Summary ?? String.Empty }, Array.Empty<EmailLink>(), actions);
                }
            }

            List<EmailLink> footer = new List<EmailLink> { new EmailLink("Open in My Morning News", app + "/dashboard/briefings/" + br.Id), new EmailLink("Email preferences", PreferencesUrl()) };
            EmailLink? unsubscribe = await UnsubscribeAsync(m, userId, EmailCategoryEnum.BriefingEmails, null, issueTokens, token).ConfigureAwait(false);
            if (unsubscribe != null) footer.Add(unsubscribe);
            b.Footer(footer, "Rating links open a short page where you confirm your rating. You get this email because briefing email is on for this edition.");
            return new EmailPreview { Recipient = m.Recipient, Subject = subject, Html = b.Html(subject), Text = b.Text() };
        }

        private async Task<List<EmailLink>> ActionsAsync(EmailMessage m, string userId, FeedbackTargetTypeEnum type, string targetId, string? topicId, IReadOnlyList<string> actions, bool issue, CancellationToken token)
        {
            bool withWhy = !ReferenceEquals(actions, EmailActions.PredictionNew) && !ReferenceEquals(actions, EmailActions.Preference);
            string secret = issue ? (await Tokens.IssueAsync(m, userId, ActionTokenKindEnum.Feedback, type, targetId, topicId, actions, token).ConfigureAwait(false)).Secret : "preview";
            string page = _Settings.Rest.PublicBaseUrl + "/v1.0/public/feedback/" + secret;
            List<EmailLink> links = actions.Select(a => new EmailLink(EmailActions.Label(a), page + "?a=" + a)).ToList();
            if (withWhy) links.Add(new EmailLink("Tell us why", page));
            return links;
        }

        private async Task<EmailLink?> UnsubscribeAsync(EmailMessage m, string userId, EmailCategoryEnum category, string? topicId, bool issue, CancellationToken token)
        {
            string scope = category + (topicId == null ? String.Empty : ":" + topicId);
            string secret = issue ? (await Tokens.IssueAsync(m, userId, ActionTokenKindEnum.Unsubscribe, FeedbackTargetTypeEnum.Briefing, scope, topicId, new[] { EmailActions.Unsubscribe }, token).ConfigureAwait(false)).Secret : "preview";
            return new EmailLink(topicId == null ? "Unsubscribe" : "Unsubscribe from this topic", _Settings.Rest.PublicBaseUrl + "/v1.0/public/unsubscribe/" + secret);
        }

        private async Task<List<string>> RecipientsAsync(Topic topic, CancellationToken token)
        {
            List<string> list = (await _Database.TopicRecipients.EnumerateAsync(new TopicRecipientFilter { TenantId = topic.TenantId, TopicId = topic.Id, PageSize = 200 }, token).ConfigureAwait(false)).Items
                .Where(r => r.Active).Select(r => Normalize(r.Email)).Where(r => r.Length > 0).Distinct(StringComparer.Ordinal).ToList();
            if (list.Count > 0) return list;
            User? owner = await _Database.Users.ReadAsync(topic.TenantId, topic.UserId, token).ConfigureAwait(false);
            string o = Normalize(owner?.Email);
            return o.Length == 0 ? new List<string>() : new List<string> { o };
        }

        private async Task<User?> UserByAddressAsync(string tenantId, string address, CancellationToken token)
        {
            if (String.IsNullOrEmpty(address)) return null;
            return (await _Database.Users.ReadAllByEmailAsync(address, token).ConfigureAwait(false)).FirstOrDefault(u => u.TenantId == tenantId);
        }

        private string PreferencesUrl()
        {
            return _Settings.Rest.AppBaseUrl + _PreferencesPath;
        }

        private string Subject(string body)
        {
            string prefix = (_Settings.Email.SubjectPrefix ?? String.Empty).Trim();
            return (prefix.Length > 0 ? prefix + " " : String.Empty) + body.Trim();
        }

        private string Logo()
        {
            return _Settings.Rest.PublicBaseUrl + "/v1.0/public/assets/logo.png";
        }

        private static string TierLabel(FindingTierEnum tier)
        {
            return tier == FindingTierEnum.MustRead ? "Must read" : tier == FindingTierEnum.WorthALook ? "Worth a look" : tier == FindingTierEnum.Fyi ? "FYI" : "Below threshold";
        }

        private static string Pct(double c)
        {
            return Math.Round(Math.Clamp(c, 0, 1) * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
        }

        private static string? FirstParagraph(string? text)
        {
            if (String.IsNullOrWhiteSpace(text)) return null;
            string first = text.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            return first.Length == 0 ? null : first;
        }

        private static string Truncate(string s, int max)
        {
            return s.Length <= max ? s : s.Substring(0, max).TrimEnd() + "...";
        }

        #endregion
    }
}
