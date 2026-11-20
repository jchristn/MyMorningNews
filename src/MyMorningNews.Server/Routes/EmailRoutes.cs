namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Email;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Email (plan section 23.7): the outbox (members see email sent to them; tenant administrators see all), resend,
    /// delete, a test email, a run digest preview, and my email preferences.
    /// </summary>
    public class EmailRoutes : RouteBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public EmailRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            const string b = "/v1.0/api/email-messages";
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, b, ListAsync, null, openApiMetadata: Paged(Doc("List the email outbox", "Email", "Rendered bodies are left out; read one message for them.")
                .WithParameter(QueryParam("status", typeof(EmailStatusEnum))).WithParameter(QueryParam("template", typeof(EmailTemplateEnum))).WithParameter(QueryParam("runId", typeof(string)))
                .WithParameter(QueryParam("briefingId", typeof(string))).WithParameter(QueryParam("topicId", typeof(string))).WithParameter(QueryParam("recipient", typeof(string)))));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, b + "/{id}", ReadAsync, null, openApiMetadata: Doc("Read an email with its rendered HTML and text", "Email"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, b + "/{id}/resend", ResendAsync, null, openApiMetadata: Doc("Send an email again", "Email"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, b + "/{id}", DeleteAsync, null, openApiMetadata: Doc("Delete an email from the outbox", "Email"));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/api/email/test", TestAsync, null, openApiMetadata: Doc("Send a test email to me", "Email"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/runs/{id}/email-preview", PreviewAsync, null, openApiMetadata: Doc("Render a run digest without sending it", "Email",
                "The lead paragraph comes from the stored run summary. Pass summarize=true to have the model write it as a real send would (one model call).").WithParameter(QueryParam("summarize", typeof(bool))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/email-preferences", PreferencesAsync, null, openApiMetadata: Doc("My email preferences", "Email"));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.PUT, "/v1.0/api/email-preferences", UpdatePreferencesAsync, null, openApiMetadata: Doc("Change my email preferences", "Email")
                .WithRequestBody(Body<EmailPreferencesRequest>()));
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.EmailRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            RequestContext rc = Context(ctx);
            bool admin = rc.IsAdmin || rc.IsTenantAdmin;
            EmailMessageFilter filter = new EmailMessageFilter
            {
                TenantId = tenantId,
                Status = QueryEnum<EmailStatusEnum>(ctx, "status"),
                Template = QueryEnum<EmailTemplateEnum>(ctx, "template"),
                RunId = Query(ctx, "runId"),
                BriefingId = Query(ctx, "briefingId"),
                TopicId = Query(ctx, "topicId"),
                Recipient = admin ? Query(ctx, "recipient")?.Trim().ToLowerInvariant() : await MyAddressAsync(ctx, tenantId).ConfigureAwait(false)
            };
            ApplyPaging(ctx, filter);
            EnumerationResult<EmailMessage> page = await Server.Database.EmailMessages.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false);
            foreach (EmailMessage m in page.Items) { m.RenderedHtml = null; m.RenderedText = null; }
            await SendJsonAsync(ctx, 200, page).ConfigureAwait(false);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            EmailMessage? m = await VisibleAsync(ctx, RequestTypeEnum.EmailRead).ConfigureAwait(false);
            if (m == null) return;
            await SendJsonAsync(ctx, 200, m).ConfigureAwait(false);
        }

        private async Task ResendAsync(HttpContextBase ctx)
        {
            EmailMessage? m = await VisibleAsync(ctx, RequestTypeEnum.EmailWrite).ConfigureAwait(false);
            if (m == null) return;
            if (m.Status == EmailStatusEnum.Queued || m.Status == EmailStatusEnum.Sending || String.IsNullOrEmpty(m.RenderedHtml))
            {
                await SendErrorAsync(ctx, 409, "Conflict", "This email is already on its way, or has no rendered body to send.").ConfigureAwait(false);
                return;
            }

            m.Status = EmailStatusEnum.Queued;
            m.Attempts = 0;
            m.NextAttemptUtc = DateTime.UtcNow;
            m.LastError = null;
            m.FailedUtc = null;
            await SendJsonAsync(ctx, 202, await Server.Database.EmailMessages.UpdateAsync(m, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            EmailMessage? m = await VisibleAsync(ctx, RequestTypeEnum.EmailWrite).ConfigureAwait(false);
            if (m == null) return;
            foreach (EmailActionToken t in (await Server.Database.EmailActionTokens.EnumerateAsync(new EmailActionTokenFilter { TenantId = m.TenantId, EmailMessageId = m.Id, PageSize = 1000 }, ctx.Token).ConfigureAwait(false)).Items)
                await Server.Database.EmailActionTokens.DeleteAsync(t.TenantId, t.Id, ctx.Token).ConfigureAwait(false);
            await Server.Database.EmailMessages.DeleteAsync(m.TenantId, m.Id, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task TestAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.EmailWrite).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            User? me = await Server.Database.Users.ReadAsync(tenantId, Context(ctx).UserId ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (me == null) { await SendErrorAsync(ctx, 409, "Conflict", "Test email needs a signed-in user.").ConfigureAwait(false); return; }
            EmailMessage m = await Server.Email.SendTestAsync(me, ctx.Token).ConfigureAwait(false);
            m.RenderedHtml = null;
            await SendJsonAsync(ctx, 202, m).ConfigureAwait(false);
        }

        private async Task PreviewAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.RunRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            Run? run = await Server.Database.Runs.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (run == null || !IsOwnerOrAdmin(ctx, run.UserId)) { await SendNotFoundAsync(ctx, "Run").ConfigureAwait(false); return; }
            await SendJsonAsync(ctx, 200, await Server.Email.PreviewRunDigestAsync(run, QueryBool(ctx, "summarize") == true, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task PreferencesAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.FeedbackRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            await SendJsonAsync(ctx, 200, await DescribeAsync(ctx, tenantId).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task UpdatePreferencesAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.FeedbackWrite).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            EmailPreferencesRequest? req = await ReadBodyAsync<EmailPreferencesRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            string? address = await MyAddressAsync(ctx, tenantId).ConfigureAwait(false);
            if (String.IsNullOrEmpty(address)) { await SendErrorAsync(ctx, 409, "Conflict", "Your account has no email address.").ConfigureAwait(false); return; }
            if (req.RunEmails.HasValue) await Server.Email.SetSuppressedAsync(tenantId, address, EmailCategoryEnum.RunEmails, null, !req.RunEmails.Value, "preferences", ctx.Token).ConfigureAwait(false);
            if (req.BriefingEmails.HasValue) await Server.Email.SetSuppressedAsync(tenantId, address, EmailCategoryEnum.BriefingEmails, null, !req.BriefingEmails.Value, "preferences", ctx.Token).ConfigureAwait(false);
            if (req.FailureEmails.HasValue) await Server.Email.SetSuppressedAsync(tenantId, address, EmailCategoryEnum.FailureEmails, null, !req.FailureEmails.Value, "preferences", ctx.Token).ConfigureAwait(false);
            foreach (string topicId in (req.ResubscribeTopicIds ?? new List<string>()).Where(t => !String.IsNullOrWhiteSpace(t)).Distinct())
                await Server.Email.SetSuppressedAsync(tenantId, address, EmailCategoryEnum.RunEmails, topicId.Trim(), false, "preferences", ctx.Token).ConfigureAwait(false);
            foreach (string topicId in (req.UnsubscribeTopicIds ?? new List<string>()).Where(t => !String.IsNullOrWhiteSpace(t)).Distinct())
            {
                Topic? t = await Server.Database.Topics.ReadAsync(tenantId, topicId.Trim(), ctx.Token).ConfigureAwait(false);
                if (t != null) await Server.Email.SetSuppressedAsync(tenantId, address, EmailCategoryEnum.RunEmails, t.Id, true, "preferences", ctx.Token).ConfigureAwait(false);
            }
            await SendJsonAsync(ctx, 200, await DescribeAsync(ctx, tenantId).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task<EmailPreferencesResponse> DescribeAsync(HttpContextBase ctx, string tenantId)
        {
            string address = await MyAddressAsync(ctx, tenantId).ConfigureAwait(false) ?? String.Empty;
            List<EmailSuppression> rows = address.Length == 0 ? new List<EmailSuppression>()
                : (await Server.Database.EmailSuppressions.EnumerateAsync(new EmailSuppressionFilter { TenantId = tenantId, Recipient = address, PageSize = 500 }, ctx.Token).ConfigureAwait(false)).Items;
            bool all = rows.Any(r => r.Category == EmailCategoryEnum.All);
            return new EmailPreferencesResponse
            {
                Address = address,
                RunEmails = !all && !rows.Any(r => r.Category == EmailCategoryEnum.RunEmails && r.TopicId == null),
                BriefingEmails = !all && !rows.Any(r => r.Category == EmailCategoryEnum.BriefingEmails && r.TopicId == null),
                FailureEmails = !all && !rows.Any(r => r.Category == EmailCategoryEnum.FailureEmails && r.TopicId == null),
                UnsubscribedTopicIds = rows.Where(r => r.Category == EmailCategoryEnum.RunEmails && r.TopicId != null).Select(r => r.TopicId!).Distinct().ToList()
            };
        }

        private async Task<string?> MyAddressAsync(HttpContextBase ctx, string tenantId)
        {
            User? me = await Server.Database.Users.ReadAsync(tenantId, Context(ctx).UserId ?? String.Empty, ctx.Token).ConfigureAwait(false);
            string a = EmailService.Normalize(me?.Email);
            return a.Length == 0 ? "-" : a;
        }

        private async Task<EmailMessage?> VisibleAsync(HttpContextBase ctx, RequestTypeEnum type)
        {
            if (!await AuthorizeAsync(ctx, type).ConfigureAwait(false)) return null;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return null;
            EmailMessage? m = await Server.Database.EmailMessages.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            RequestContext rc = Context(ctx);
            if (m == null || (!(rc.IsAdmin || rc.IsTenantAdmin) && m.Recipient != await MyAddressAsync(ctx, tenantId).ConfigureAwait(false)))
            {
                await SendNotFoundAsync(ctx, "Email").ConfigureAwait(false);
                return null;
            }

            return m;
        }
    }
}
