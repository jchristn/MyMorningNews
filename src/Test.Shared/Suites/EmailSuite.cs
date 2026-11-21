namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Analytics;
    using MyMorningNews.Core.Email;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Server;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Email delivery and email feedback (plan section 23): rendering, run digests, recipients and suppression,
    /// feedback landing pages, unsubscribe, delivery with retries, the outbox API, and briefing email.
    /// </summary>
    public static class EmailSuite
    {
        private const string S = "Email";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Email delivery and feedback", new[]
            {
                Cases.Case(S, "Builder", "Emails encode every value, drop unsafe links, and carry a matching text version", BuilderAsync, "fast"),
                Cases.Case(S, "RunDigest", "A run queues one digest per recipient with detail and feedback links, written by the File provider", RunDigestAsync),
                Cases.Case(S, "FeedbackLinks", "Feedback links open a form, record on submit with the reason, supersede, and act on predictions and suggestions", FeedbackLinksAsync),
                Cases.Case(S, "Unsubscribe", "Unsubscribe links stop a topic's run emails and can be undone", UnsubscribeAsync),
                Cases.Case(S, "Delivery", "Mailgun delivery records the provider ID, retries with backoff, and fails after the last attempt", DeliveryAsync),
                Cases.Case(S, "Api", "The outbox, resend, test email, preview, preferences, and feedback API work and stay private", ApiAsync),
                Cases.Case(S, "BriefingEmail", "An edition with email on sends the briefing", BriefingEmailAsync)
            });
        }

        private static Task BuilderAsync(CancellationToken token)
        {
            EmailBuilder b = new EmailBuilder().Preheader("Pre <x>")
                .Header("https://a.test/logo.png", "Title & <b>", "Sub", new EmailLink("Go", "https://a.test/go?x=1&y=2"))
                .Paragraph("Hello <script>alert(1)</script>")
                .Item("Must read · 90", "Item <i>", "javascript:alert(1)", new[] { "Line one" }, new[] { new EmailLink("a.test", "https://a.test/s") }, new[] { new EmailLink("Relevant", "https://a.test/f?a=relevant"), new EmailLink("Bad", "javascript:x") })
                .Footer(new[] { new EmailLink("Prefs", "https://a.test/p") }, "Note");
            string html = b.Html("Subject <s>");
            string text = b.Text();
            Assert.True(html.Contains("Title &amp; &lt;b&gt;", StringComparison.Ordinal) && html.Contains("&lt;script&gt;", StringComparison.Ordinal) && !html.Contains("<script>", StringComparison.Ordinal), "encoded");
            Assert.True(!html.Contains("javascript:", StringComparison.Ordinal) && html.Contains("href=\"https://a.test/go?x=1&amp;y=2\"", StringComparison.Ordinal), "unsafe links dropped, safe links encoded once");
            Assert.True(html.Contains("Pre &lt;x&gt;", StringComparison.Ordinal) && html.Contains("max-width:600px", StringComparison.Ordinal) && html.Contains("color-scheme", StringComparison.Ordinal) && (html.Contains("\u00A9 2026 Lattice AI LLC", StringComparison.Ordinal) || html.Contains("&#169; 2026 Lattice AI LLC", StringComparison.Ordinal)), "preheader, width, dark mode, copyright");
            Assert.True(text.Contains("Go: https://a.test/go?x=1&y=2", StringComparison.Ordinal) && text.Contains("Relevant: https://a.test/f?a=relevant", StringComparison.Ordinal) && !text.Contains("javascript", StringComparison.Ordinal), "text version");
            Assert.True(EmailService.Normalize(" A@B.Test ") == "a@b.test" && EmailService.Normalize("not an email") == String.Empty, "address normalization");
            return Task.CompletedTask;
        }

        private static async Task<List<EmailMessage>> RunEmailsAsync(PipelineFixture f, Run run, CancellationToken token)
        {
            return (await f.Server.Context.Database.EmailMessages.EnumerateAsync(new EmailMessageFilter { TenantId = run.TenantId, RunId = run.Id, PageSize = 50 }, token).ConfigureAwait(false)).Items;
        }

        private static string Link(string text, string pattern)
        {
            Match m = Regex.Match(text, pattern);
            if (!m.Success) throw new InvalidOperationException("No link matching " + pattern + " in:\n" + text);
            return m.Value;
        }

        private static async Task RunDigestAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            (await f.User.Client.PostAsync("/v1.0/api/topics/" + f.Topic.Id + "/recipients", new { email = "Partner@Example.Test" }, token).ConfigureAwait(false)).Expect(201, "recipient");
            (await f.User.Client.PostAsync("/v1.0/api/topics/" + f.Topic.Id + "/recipients", new { email = f.User.User.Email }, token).ConfigureAwait(false)).Expect(201, "owner as recipient");
            Run run = await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            List<RunStage> stages = (await f.User.Client.GetAsync("/v1.0/api/runs/" + run.Id + "/stages", token).ConfigureAwait(false)).Json<List<RunStage>>();
            Assert.True(stages.Last().Stage == "Notify" && stages.Last().Status == StageStatusEnum.Succeeded && stages.Last().ItemsOut == 2, "notify stage queued two");
            List<EmailMessage> emails = await RunEmailsAsync(f, run, token).ConfigureAwait(false);
            Assert.True(emails.Count == 2 && emails.All(e => e.Status == EmailStatusEnum.Queued && e.Template == EmailTemplateEnum.RunDigest) && emails.Any(e => e.Recipient == "partner@example.test"), "one per recipient, addresses normalized");
            EmailMessage m = emails.First(e => e.Recipient != "partner@example.test");
            Assert.True(m.Subject.StartsWith("[My Morning News] " + f.Topic.Name + ": 2 new (", StringComparison.Ordinal) && m.IdempotencyKey == "run:" + run.Id + ":" + m.Recipient, "subject and key: " + m.Subject);
            Assert.True(m.RenderedHtml!.Contains("Heat pump rebates expand", StringComparison.Ordinal) && m.RenderedHtml.Contains("/dashboard/findings/fnd_", StringComparison.Ordinal) && m.RenderedHtml.Contains("/v1.0/public/feedback/", StringComparison.Ordinal)
                && m.RenderedHtml.Contains("Dig into next", StringComparison.Ordinal) && m.RenderedHtml.Contains("Where it might go", StringComparison.Ordinal), "content and links");
            Assert.True(m.RenderedText!.Contains("Relevant: http", StringComparison.Ordinal) && m.RenderedText.Contains("Unsubscribe from this topic: http", StringComparison.Ordinal), "text with feedback and unsubscribe links");
            Assert.Equal(0, await f.Server.Context.Email.QueueRunDigestAsync(run, token).ConfigureAwait(false), "idempotent per run and recipient");

            await f.Server.Context.Email.DeliverDueAsync(DateTime.UtcNow, token).ConfigureAwait(false);
            EmailMessage sent = (await f.Server.Context.Database.EmailMessages.ReadAsync(m.TenantId, m.Id, token).ConfigureAwait(false))!;
            Assert.True(sent.Status == EmailStatusEnum.Sent && sent.Provider == EmailProviderEnum.File && sent.ProviderMessageId == "file:" + m.Id && sent.SentUtc.HasValue, "sent by the File provider");
            string dir = f.Server.Context.Settings.Email.FileDirectory;
            Assert.True(File.Exists(Path.Combine(dir, m.Id + ".html")) && File.Exists(Path.Combine(dir, m.Id + ".txt")) && File.ReadAllText(Path.Combine(dir, m.Id + ".json")).Contains(m.Subject.Replace("\"", "\\\""), StringComparison.Ordinal), "files written");
            EmailReport er = (await f.User.Client.GetAsync("/v1.0/api/analytics/email?all=true&topicId=" + f.Topic.Id, token).ConfigureAwait(false)).Expect(200, "email analytics").Json<EmailReport>();
            Assert.True(er.Total == 2 && er.Sent + er.Pending == 2 && er.Sent >= 1 && er.DeliveryRate == 1 && er.MeanSecondsToSend.HasValue && er.ByTemplate.Single().Key == "RunDigest"
                && er.ByDay.Single().Count == 2 && er.ByStatus.Any(x => x.Key == "Sent"), "email analytics: total " + er.Total + ", sent " + er.Sent);

            (await f.User.Client.PutAsync("/v1.0/api/topics/" + f.Topic.Id, new { emailOnRun = false }, token).ConfigureAwait(false)).Expect(200, "email off");
            Run quiet = await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            Assert.Equal(0, (await RunEmailsAsync(f, quiet, token).ConfigureAwait(false)).Count, "no email when email on run is off");
        }

        private static async Task FeedbackLinksAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            Prediction due = await f.Server.Context.Database.Predictions.CreateAsync(new Prediction
            {
                TenantId = f.Topic.TenantId, RunId = "run_seed", TopicId = f.Topic.Id, UserId = f.Topic.UserId, Statement = "A rebate portal will launch by October.", Confidence = 0.6, HorizonUtc = DateTime.UtcNow.AddDays(-2)
            }, token).ConfigureAwait(false);
            Run run = await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            EmailMessage m = (await RunEmailsAsync(f, run, token).ConfigureAwait(false)).Single();
            Assert.True(m.RenderedHtml!.Contains("Predictions to grade", StringComparison.Ordinal) && m.RenderedHtml.Contains("A rebate portal will launch", StringComparison.Ordinal), "due prediction listed to grade");
            Finding top = (await f.User.Client.GetAsync("/v1.0/api/findings?runId=" + run.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Finding>>().Items.First();
            TestApiClient anon = new TestApiClient(await SharedTestServer.GetBaseUrlAsync(token).ConfigureAwait(false));
            string Path(string url) => new Uri(url).PathAndQuery;

            string relevant = Path(Link(m.RenderedText!, @"https?://\S+/v1\.0/public/feedback/[A-Za-z0-9_\-]+\?a=relevant"));
            TestResponse page = (await anon.GetAsync(relevant, token).ConfigureAwait(false)).Expect(200, "landing page");
            Assert.True(page.Body.Contains("value=\"relevant\" checked", StringComparison.Ordinal) && page.Body.Contains("<form method=\"post\"", StringComparison.Ordinal) && page.Headers["X-Robots-Tag"].Contains("noindex", StringComparison.Ordinal)
                && page.Headers["Content-Security-Policy"].Contains("default-src 'none'", StringComparison.Ordinal), "form with the choice preselected");
            Assert.True(page.Body.Contains("Confirm your rating", StringComparison.Ordinal) && page.Body.Contains("<button type=\"submit\">Save “Relevant”</button>", StringComparison.Ordinal)
                && page.Body.Contains("not saved until you press", StringComparison.Ordinal), "the page says the rating still needs confirming, with one clear button (UT-002)");
            Assert.True(m.RenderedText!.Contains("confirm", StringComparison.OrdinalIgnoreCase) && !m.RenderedText.Contains("one-click", StringComparison.OrdinalIgnoreCase), "the email says rating links ask for a confirmation");
            Assert.Equal(0L, await CountFeedbackAsync(f, top.Id, token).ConfigureAwait(false), "a GET records nothing (mail scanners)");

            string post = relevant.Split('?')[0];
            TestResponse thanks = (await anon.SendAsync(HttpMethod.Post, post, "a=relevant&reason=Exactly+what+I+track", new Dictionary<string, string>(), token).ConfigureAwait(false)).Expect(200, "submit");
            Assert.True(thanks.Body.Contains("Thank you", StringComparison.Ordinal) && thanks.Body.Contains("help tune future runs", StringComparison.Ordinal), "thanks page");
            EnumerationResult<Feedback> fb = (await f.User.Client.GetAsync("/v1.0/api/feedback?targetType=Finding&targetId=" + top.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Feedback>>();
            Assert.True(fb.Items.Count == 1 && fb.Items[0].Relevance == RelevanceRatingEnum.Relevant && fb.Items[0].Reason == "Exactly what I track" && fb.Items[0].Channel == FeedbackChannelEnum.Email && fb.Items[0].EmailMessageId == m.Id,
                "feedback recorded with the reason, channel, and email");
            (await anon.SendAsync(HttpMethod.Post, post, "a=not-relevant", new Dictionary<string, string>(), token).ConfigureAwait(false)).Expect(200, "change answer");
            EnumerationResult<Feedback> history = (await f.User.Client.GetAsync("/v1.0/api/feedback?targetType=Finding&targetId=" + top.Id + "&includeHistory=true", token).ConfigureAwait(false)).Json<EnumerationResult<Feedback>>();
            Assert.True(history.Items.Count == 2 && history.Items.Count(x => x.IsCurrent) == 1 && history.Items.Single(x => x.IsCurrent).Relevance == RelevanceRatingEnum.NotRelevant
                && history.Items.Single(x => x.IsCurrent).SupersedesFeedbackId == history.Items.Single(x => !x.IsCurrent).Id, "newest wins, history kept");
            (await anon.SendAsync(HttpMethod.Post, post, "a=accurate", new Dictionary<string, string>(), token).ConfigureAwait(false)).Expect(400, "an action the token does not allow is refused");

            string grade = Path(Link(m.RenderedText!, @"https?://\S+/v1\.0/public/feedback/[A-Za-z0-9_\-]+\?a=accurate"));
            (await anon.SendAsync(HttpMethod.Post, grade.Split('?')[0], "a=accurate", new Dictionary<string, string>(), token).ConfigureAwait(false)).Expect(200, "grade");
            Prediction graded = (await f.Server.Context.Database.Predictions.ReadAsync(due.TenantId, due.Id, token).ConfigureAwait(false))!;
            Assert.True(graded.Status == PredictionStatusEnum.Accurate && graded.ResolvedBy == "User" && Math.Abs(graded.BrierScore!.Value - 0.16) < 0.0001, "grading from email resolves the prediction");

            string done = Path(Link(m.RenderedText!, @"https?://\S+/v1\.0/public/feedback/[A-Za-z0-9_\-]+\?a=done"));
            (await anon.SendAsync(HttpMethod.Post, done.Split('?')[0], "a=done", new Dictionary<string, string>(), token).ConfigureAwait(false)).Expect(200, "done");
            Suggestion s = (await f.User.Client.GetAsync("/v1.0/api/suggestions?topicId=" + f.Topic.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Suggestion>>().Items.First();
            Assert.Equal(SuggestionStatusEnum.Done, s.Status, "Done from email completes the suggestion");

            TestResponse bad = (await anon.GetAsync("/v1.0/public/feedback/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA?a=relevant", token).ConfigureAwait(false)).Expect(404, "unknown token");
            TestResponse junk = (await anon.GetAsync("/v1.0/public/feedback/x", token).ConfigureAwait(false)).Expect(404, "malformed token");
            Assert.Equal(bad.Body, junk.Body, "uniform page for every bad link");
        }

        private static async Task<long> CountFeedbackAsync(PipelineFixture f, string targetId, CancellationToken token)
        {
            return await f.Server.Context.Database.Feedback.CountAsync(new FeedbackFilter { TenantId = f.Topic.TenantId, TargetId = targetId }, token).ConfigureAwait(false);
        }

        private static async Task UnsubscribeAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            Run run = await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            EmailMessage m = (await RunEmailsAsync(f, run, token).ConfigureAwait(false)).Single();
            string url = new Uri(Link(m.RenderedText!, @"https?://\S+/v1\.0/public/unsubscribe/[A-Za-z0-9_\-]+")).PathAndQuery;
            TestApiClient anon = new TestApiClient(await SharedTestServer.GetBaseUrlAsync(token).ConfigureAwait(false));
            TestResponse ask = (await anon.GetAsync(url, token).ConfigureAwait(false)).Expect(200, "page");
            Assert.True(ask.Body.Contains("Unsubscribe?", StringComparison.Ordinal) && ask.Body.Contains("run emails for " + f.Topic.Name, StringComparison.Ordinal), "asks first: " + ask.Body);
            Assert.True(!await f.Server.Context.Email.IsSuppressedAsync(m.TenantId, m.Recipient, EmailCategoryEnum.RunEmails, f.Topic.Id, token).ConfigureAwait(false), "a GET changes nothing");
            TestResponse done = (await anon.SendAsync(HttpMethod.Post, url, "op=unsubscribe", new Dictionary<string, string>(), token).ConfigureAwait(false)).Expect(200, "unsubscribe");
            Assert.True(done.Body.Contains("You are unsubscribed", StringComparison.Ordinal) && done.Body.Contains("Undo", StringComparison.Ordinal), "done with undo");
            Run next = await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            List<EmailMessage> after = await RunEmailsAsync(f, next, token).ConfigureAwait(false);
            Assert.True(after.Count == 1 && after[0].Status == EmailStatusEnum.Suppressed, "next digest suppressed: " + String.Join(",", after.Select(a => a.Status)));
            EmailPreferencesResponse prefs = (await f.User.Client.GetAsync("/v1.0/api/email-preferences", token).ConfigureAwait(false)).Json<EmailPreferencesResponse>();
            Assert.True(prefs.RunEmails && prefs.UnsubscribedTopicIds.Contains(f.Topic.Id), "topic unsubscribe shows in preferences");
            EmailPreferencesResponse viaApi = (await f.User.Client.PutAsync("/v1.0/api/email-preferences", new { resubscribeTopicIds = new[] { f.Topic.Id } }, token).ConfigureAwait(false)).Expect(200, "resubscribe topic").Json<EmailPreferencesResponse>();
            Assert.True(!viaApi.UnsubscribedTopicIds.Contains(f.Topic.Id), "topic back on through preferences");
            (await f.User.Client.PutAsync("/v1.0/api/email-preferences", new { unsubscribeTopicIds = new[] { f.Topic.Id } }, token).ConfigureAwait(false)).Expect(200, "unsubscribe topic");
            (await anon.SendAsync(HttpMethod.Post, url, "op=undo", new Dictionary<string, string>(), token).ConfigureAwait(false)).Expect(200, "undo");
            Assert.True(!await f.Server.Context.Email.IsSuppressedAsync(m.TenantId, m.Recipient, EmailCategoryEnum.RunEmails, f.Topic.Id, token).ConfigureAwait(false), "resubscribed");
        }

        private static async Task DeliveryAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            MyMorningNews.Core.Settings.EmailSettings email = f.Server.Context.Settings.Email;
            EmailProviderEnum provider = email.Provider;
            string domain = email.MailgunDomain;
            string key = email.MailgunApiKey;
            string baseUrl = email.MailgunBaseUrl;
            try
            {
                email.Provider = EmailProviderEnum.Mailgun;
                email.MailgunApiKey = "key-test";
                email.MailgunBaseUrl = StubModelServer.Shared.BaseUrl + "/mailgun/v3/";
                email.MailgunDomain = "mg.example.test";
                User me = (await f.Server.Context.Database.Users.ReadAsync(f.Topic.TenantId, f.User.User.Id, token).ConfigureAwait(false))!;
                EmailMessage ok = await f.Server.Context.Email.SendTestAsync(me, token).ConfigureAwait(false);
                await f.Server.Context.Email.DeliverDueAsync(DateTime.UtcNow, token).ConfigureAwait(false);
                EmailMessage sent = (await f.Server.Context.Database.EmailMessages.ReadAsync(ok.TenantId, ok.Id, token).ConfigureAwait(false))!;
                Assert.True(sent.Status == EmailStatusEnum.Sent && sent.Provider == EmailProviderEnum.Mailgun && sent.ProviderMessageId!.StartsWith("stub.", StringComparison.Ordinal) && !sent.ProviderMessageId.Contains('<'), "sent with a normalized ID: " + sent.ProviderMessageId);
                Assert.True(StubModelServer.Shared.MailgunRequests.Any(b => b.Contains("Test+email", StringComparison.Ordinal) || b.Contains("Test%20email", StringComparison.Ordinal)), "HTML posted to Mailgun");

                email.MailgunDomain = "fail.example.test";
                EmailMessage retry = await f.Server.Context.Email.SendTestAsync(me, token).ConfigureAwait(false);
                DateTime now = DateTime.UtcNow;
                await f.Server.Context.Email.DeliverDueAsync(now, token).ConfigureAwait(false);
                EmailMessage first = (await f.Server.Context.Database.EmailMessages.ReadAsync(retry.TenantId, retry.Id, token).ConfigureAwait(false))!;
                Assert.True(first.Status == EmailStatusEnum.Queued && first.Attempts == 1 && first.NextAttemptUtc > now && first.LastError != null, "retry scheduled: " + first.Status + " " + first.LastError);
                for (int i = 0; i < email.DeliveryMaxAttempts + 2; i++) await f.Server.Context.Email.DeliverDueAsync(DateTime.UtcNow.AddHours(1 + i), token).ConfigureAwait(false);
                EmailMessage failed = (await f.Server.Context.Database.EmailMessages.ReadAsync(retry.TenantId, retry.Id, token).ConfigureAwait(false))!;
                Assert.True(failed.Status == EmailStatusEnum.Failed && failed.Attempts == email.DeliveryMaxAttempts && failed.FailedUtc.HasValue, "failed after the last attempt (" + failed.Attempts + ")");
            }
            finally
            {
                email.Provider = provider;
                email.MailgunDomain = domain;
                email.MailgunApiKey = key;
                email.MailgunBaseUrl = baseUrl;
            }
        }

        private static async Task ApiAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            Run run = await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            EnumerationResult<EmailMessage> outbox = (await f.User.Client.GetAsync("/v1.0/api/email-messages?runId=" + run.Id, token).ConfigureAwait(false)).Expect(200, "outbox").Json<EnumerationResult<EmailMessage>>();
            Assert.True(outbox.Items.Count == 1 && outbox.Items[0].RenderedHtml == null, "listed without bodies");
            EmailMessage full = (await f.User.Client.GetAsync("/v1.0/api/email-messages/" + outbox.Items[0].Id, token).ConfigureAwait(false)).Expect(200, "read").Json<EmailMessage>();
            Assert.True(full.RenderedHtml!.Contains("<html", StringComparison.Ordinal), "read has the body");
            (await f.User.Client.PostAsync("/v1.0/api/email-messages/" + full.Id + "/resend", null, token).ConfigureAwait(false)).Expect(409, "already queued");
            await f.Server.Context.Email.DeliverDueAsync(DateTime.UtcNow, token).ConfigureAwait(false);
            (await f.User.Client.PostAsync("/v1.0/api/email-messages/" + full.Id + "/resend", null, token).ConfigureAwait(false)).Expect(202, "resend after sending");

            EmailMessage test = (await f.User.Client.PostAsync("/v1.0/api/email/test", null, token).ConfigureAwait(false)).Expect(202, "test email").Json<EmailMessage>();
            Assert.True(test.Template == EmailTemplateEnum.Test && test.Recipient == EmailService.Normalize(f.User.User.Email), "test to me");
            long summariesBefore = await f.Server.Context.Database.LlmCalls.CountAsync(new LlmCallFilter { TenantId = run.TenantId, RunId = run.Id, PromptKey = "email.run-summary" }, token).ConfigureAwait(false);
            EmailPreview preview = (await f.User.Client.GetAsync("/v1.0/api/runs/" + run.Id + "/email-preview", token).ConfigureAwait(false)).Expect(200, "preview").Json<EmailPreview>();
            Assert.Equal(summariesBefore, await f.Server.Context.Database.LlmCalls.CountAsync(new LlmCallFilter { TenantId = run.TenantId, RunId = run.Id, PromptKey = "email.run-summary" }, token).ConfigureAwait(false), "a preview makes no model call");
            Assert.True(preview.Subject.Contains(f.Topic.Name, StringComparison.Ordinal) && preview.Html.Contains("/v1.0/public/feedback/preview", StringComparison.Ordinal), "preview with sample links");
            Assert.Equal(1, (await f.User.Client.GetAsync("/v1.0/api/email-messages?runId=" + run.Id, token).ConfigureAwait(false)).Json<EnumerationResult<EmailMessage>>().Items.Count, "preview saved nothing");

            EmailPreferencesResponse prefs = (await f.User.Client.PutAsync("/v1.0/api/email-preferences", new { briefingEmails = false }, token).ConfigureAwait(false)).Expect(200, "preferences").Json<EmailPreferencesResponse>();
            Assert.True(prefs.RunEmails && !prefs.BriefingEmails && prefs.FailureEmails, "briefing emails off");
            (await f.User.Client.PutAsync("/v1.0/api/email-preferences", new { briefingEmails = true }, token).ConfigureAwait(false)).Expect(200, "back on");

            Finding finding = (await f.User.Client.GetAsync("/v1.0/api/findings?runId=" + run.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Finding>>().Items.First();
            Feedback given = (await f.User.Client.PostAsync("/v1.0/api/feedback", new { targetType = "Finding", targetId = finding.Id, action = "somewhat", reason = "Close" }, token).ConfigureAwait(false))
                .Expect(201, "dashboard feedback").Json<Feedback>();
            Assert.True(given.Relevance == RelevanceRatingEnum.SomewhatRelevant && given.Channel == FeedbackChannelEnum.Dashboard && given.TopicId == f.Topic.Id, "feedback from the dashboard");
            (await f.User.Client.PostAsync("/v1.0/api/feedback", new { targetType = "Finding", targetId = finding.Id, action = "accurate" }, token).ConfigureAwait(false)).Expect(400, "accuracy does not apply to findings");
            (await f.User.Client.PostAsync("/v1.0/api/feedback", new { targetType = "Finding", targetId = finding.Id }, token).ConfigureAwait(false)).Expect(400, "nothing rated");

            TestUser other = await TestFixtures.CreateUserAsync(await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false), f.Topic.TenantId, false, token).ConfigureAwait(false);
            (await other.Client.PostAsync("/v1.0/api/feedback", new { targetType = "Finding", targetId = finding.Id, action = "relevant" }, token).ConfigureAwait(false)).Expect(404, "not someone else's finding");
            (await other.Client.GetAsync("/v1.0/api/email-messages/" + full.Id, token).ConfigureAwait(false)).Expect(404, "another member cannot read my email");
            Assert.Equal(0, (await other.Client.GetAsync("/v1.0/api/email-messages", token).ConfigureAwait(false)).Json<EnumerationResult<EmailMessage>>().Items.Count(x => x.Id == full.Id), "and does not list it");
        }

        private static async Task BriefingEmailAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            EditionResponse e = (await f.User.Client.PostAsync("/v1.0/api/editions", new { name = "Mail Heat", freshResearchBeforeCompose = false, emailEnabled = true, audioEnabled = false, lookbackHours = 48 }, token).ConfigureAwait(false))
                .Expect(201, "edition").Json<EditionResponse>();
            (await f.User.Client.PutAsync("/v1.0/api/editions/" + e.Edition.Id + "/topics", new { topicIds = new[] { f.Topic.Id } }, token).ConfigureAwait(false)).Expect(200, "topics");
            Briefing queued = (await f.User.Client.PostAsync("/v1.0/api/editions/" + e.Edition.Id + "/compose", null, token).ConfigureAwait(false)).Expect(202, "compose").Json<Briefing>();
            await BriefingWaiter.FinishAsync(f.Server, queued.TenantId, queued.Id, token).ConfigureAwait(false);
            List<EmailMessage> mails = (await f.Server.Context.Database.EmailMessages.EnumerateAsync(new EmailMessageFilter { TenantId = queued.TenantId, BriefingId = queued.Id, PageSize = 10 }, token).ConfigureAwait(false)).Items;
            Assert.True(mails.Count == 1 && mails[0].Template == EmailTemplateEnum.BriefingPublished && mails[0].Subject == "[My Morning News] " + (await f.Server.Context.Database.Briefings.ReadAsync(queued.TenantId, queued.Id, token).ConfigureAwait(false))!.Title,
                "briefing email queued: " + String.Join(",", mails.Select(x => x.Subject)));
            string mailHtml = mails[0].RenderedHtml ?? String.Empty;
            Assert.True(mailHtml.Contains("Read the full briefing", StringComparison.Ordinal) && mailHtml.Contains("/v1.0/public/feedback/", StringComparison.Ordinal), "briefing email with feedback links");
        }
    }
}
