namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Email;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Feedback;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Core.Telemetry;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Feedback: the authenticated API (dashboard, mobile, SDKs) and the public email landing pages (plan sections 23.4 and
    /// 23.5). A GET on an email link never records feedback unless Email.RecordFeedbackOnClick is on; it shows a form with
    /// the choice preselected, and the POST records it. Unknown, expired, and revoked links all get the same page.
    /// </summary>
    public class FeedbackRoutes : RouteBase
    {
        private readonly LoginRateLimiter _Limiter;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public FeedbackRoutes(ServerContext server) : base(server)
        {
            _Limiter = new LoginRateLimiter(server.Settings.Email.PublicRequestsPerMinute);
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/api/feedback", CreateAsync, null, openApiMetadata: Doc("Give feedback on an item", "Feedback",
                "Send an action (relevant, somewhat, not-relevant, accurate, partially, inaccurate, too-early, useful, not-useful, done, snooze) or explicit ratings, with an optional reason.")
                .WithRequestBody(Body<FeedbackCreateRequest>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/feedback", ListAsync, null, openApiMetadata: Paged(Doc("List feedback", "Feedback", "Current ratings only unless includeHistory=true.")
                .WithParameter(QueryParam("targetType", typeof(FeedbackTargetTypeEnum))).WithParameter(QueryParam("targetId", typeof(string))).WithParameter(QueryParam("topicId", typeof(string)))
                .WithParameter(QueryParam("channel", typeof(FeedbackChannelEnum))).WithParameter(QueryParam("includeHistory", typeof(bool)))));

            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/public/feedback/{token}", FeedbackPageAsync, null, openApiMetadata: Doc("Email feedback page", "Public"));
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/public/feedback/{token}", FeedbackSubmitAsync, null, openApiMetadata: Doc("Submit email feedback (form post)", "Public"));
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/public/unsubscribe/{token}", UnsubscribePageAsync, null, openApiMetadata: Doc("Unsubscribe page", "Public"));
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/public/unsubscribe/{token}", UnsubscribeSubmitAsync, null, openApiMetadata: Doc("Unsubscribe or undo (form post)", "Public"));
        }

        private async Task CreateAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.FeedbackWrite).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            FeedbackCreateRequest? req = await ReadBodyAsync<FeedbackCreateRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            Feedback saved = await Server.Feedback.RecordAsync(new FeedbackInput
            {
                TenantId = tenantId,
                UserId = Context(ctx).UserId ?? String.Empty,
                TargetType = req.TargetType,
                TargetId = req.TargetId ?? String.Empty,
                Action = String.IsNullOrWhiteSpace(req.Action) ? null : req.Action.Trim().ToLowerInvariant(),
                Relevance = req.Relevance,
                Accuracy = req.Accuracy,
                Usefulness = req.Usefulness,
                Reason = req.Reason,
                Channel = req.Channel == FeedbackChannelEnum.Email ? FeedbackChannelEnum.Api : req.Channel
            }, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, saved).ConfigureAwait(false);
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.FeedbackRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            RequestContext rc = Context(ctx);
            FeedbackFilter filter = new FeedbackFilter
            {
                TenantId = tenantId,
                UserId = ListScope(ctx),
                TargetType = QueryEnum<FeedbackTargetTypeEnum>(ctx, "targetType"),
                TargetId = Query(ctx, "targetId"),
                TopicId = Query(ctx, "topicId"),
                Channel = QueryEnum<FeedbackChannelEnum>(ctx, "channel"),
                IsCurrent = QueryBool(ctx, "includeHistory") == true ? null : true
            };
            ApplyPaging(ctx, filter);
            await SendJsonAsync(ctx, 200, await Server.Database.Feedback.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task FeedbackPageAsync(HttpContextBase ctx)
        {
            if (!await AllowAsync(ctx).ConfigureAwait(false)) return;
            string secret = Param(ctx, "token") ?? String.Empty;
            EmailActionToken? t = await Server.Email.Tokens.ValidateAsync(secret, ctx.Token).ConfigureAwait(false);
            if (t == null || t.Kind != ActionTokenKindEnum.Feedback) { await InvalidAsync(ctx).ConfigureAwait(false); return; }
            string? action = Query(ctx, "a");
            if (action != null && !t.AllowedActions.Contains(action)) action = null;
            await Server.Email.Tokens.TouchAsync(t, false, ctx.Token).ConfigureAwait(false);
            MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.EmailFeedbackClicks, "targettype", t.TargetType.ToString(), "action", "open:" + (action ?? "why"));
            bool recorded = false;
            if (action != null && Server.Settings.Email.RecordFeedbackOnClick)
            {
                await RecordAsync(t, action, null, ctx).ConfigureAwait(false);
                recorded = true;
            }

            ItemText item = await ItemAsync(t, ctx).ConfigureAwait(false);
            await SendHtmlAsync(ctx, 200, PublicPages.Feedback(Logo(), PageUrl("feedback", secret), item.Title, item.Summary, t.AllowedActions, action, item.OpenUrl, recorded)).ConfigureAwait(false);
        }

        private async Task FeedbackSubmitAsync(HttpContextBase ctx)
        {
            if (!await AllowAsync(ctx).ConfigureAwait(false)) return;
            string secret = Param(ctx, "token") ?? String.Empty;
            EmailActionToken? t = await Server.Email.Tokens.ValidateAsync(secret, ctx.Token).ConfigureAwait(false);
            if (t == null || t.Kind != ActionTokenKindEnum.Feedback) { await InvalidAsync(ctx).ConfigureAwait(false); return; }
            Dictionary<string, string> form = PublicPages.ParseForm(ctx.Request.DataAsString);
            string? action = form.TryGetValue("a", out string? a) && t.AllowedActions.Contains(a) ? a : null;
            string? reason = form.TryGetValue("reason", out string? r) && !String.IsNullOrWhiteSpace(r) ? r : null;
            if (action == null && reason == null)
            {
                ItemText again = await ItemAsync(t, ctx).ConfigureAwait(false);
                await SendHtmlAsync(ctx, 400, PublicPages.Feedback(Logo(), PageUrl("feedback", secret), again.Title, again.Summary, t.AllowedActions, null, again.OpenUrl, false)).ConfigureAwait(false);
                return;
            }

            await RecordAsync(t, action, reason, ctx).ConfigureAwait(false);
            await Server.Email.Tokens.TouchAsync(t, true, ctx.Token).ConfigureAwait(false);
            MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.EmailFeedbackClicks, "targettype", t.TargetType.ToString(), "action", "submit:" + (action ?? "why"));
            ItemText item = await ItemAsync(t, ctx).ConfigureAwait(false);
            await SendHtmlAsync(ctx, 200, PublicPages.Thanks(Logo(), PageUrl("feedback", secret) + (action == null ? String.Empty : "?a=" + action), action, reason != null, item.OpenUrl)).ConfigureAwait(false);
        }

        private async Task UnsubscribePageAsync(HttpContextBase ctx)
        {
            if (!await AllowAsync(ctx).ConfigureAwait(false)) return;
            string secret = Param(ctx, "token") ?? String.Empty;
            EmailActionToken? t = await Server.Email.Tokens.ValidateAsync(secret, ctx.Token).ConfigureAwait(false);
            if (t == null || t.Kind != ActionTokenKindEnum.Unsubscribe) { await InvalidAsync(ctx).ConfigureAwait(false); return; }
            UnsubscribeScope scope = await ScopeAsync(t, ctx).ConfigureAwait(false);
            bool done = await Server.Email.IsSuppressedAsync(t.TenantId, t.Recipient, scope.Category, scope.TopicId, ctx.Token).ConfigureAwait(false);
            await SendHtmlAsync(ctx, 200, PublicPages.Unsubscribe(Logo(), PageUrl("unsubscribe", secret), scope.Description, done, false)).ConfigureAwait(false);
        }

        private async Task UnsubscribeSubmitAsync(HttpContextBase ctx)
        {
            if (!await AllowAsync(ctx).ConfigureAwait(false)) return;
            string secret = Param(ctx, "token") ?? String.Empty;
            EmailActionToken? t = await Server.Email.Tokens.ValidateAsync(secret, ctx.Token).ConfigureAwait(false);
            if (t == null || t.Kind != ActionTokenKindEnum.Unsubscribe) { await InvalidAsync(ctx).ConfigureAwait(false); return; }
            UnsubscribeScope scope = await ScopeAsync(t, ctx).ConfigureAwait(false);
            Dictionary<string, string> form = PublicPages.ParseForm(ctx.Request.DataAsString);
            bool undo = form.TryGetValue("op", out string? op) && op == "undo";
            await Server.Email.SetSuppressedAsync(t.TenantId, t.Recipient, scope.Category, scope.TopicId, !undo, "link", ctx.Token).ConfigureAwait(false);
            await Server.Email.Tokens.TouchAsync(t, true, ctx.Token).ConfigureAwait(false);
            await Server.Audit.RecordAsync(AuditEventTypeEnum.SettingsChanged, null, 200, (undo ? "Resubscribed " : "Unsubscribed ") + scope.Category + (scope.TopicId == null ? String.Empty : " for " + scope.TopicId) + " by email link " + t.Id + ".", ctx.Token).ConfigureAwait(false);
            await SendHtmlAsync(ctx, 200, PublicPages.Unsubscribe(Logo(), PageUrl("unsubscribe", secret), scope.Description, !undo, undo)).ConfigureAwait(false);
        }

        private async Task RecordAsync(EmailActionToken t, string? action, string? reason, HttpContextBase ctx)
        {
            await Server.Feedback.RecordAsync(new FeedbackInput
            {
                TenantId = t.TenantId,
                UserId = t.UserId,
                TargetType = t.TargetType,
                TargetId = t.TargetId,
                Action = action,
                Reason = reason,
                Channel = FeedbackChannelEnum.Email,
                EmailMessageId = t.EmailMessageId
            }, ctx.Token).ConfigureAwait(false);
            await Server.Audit.RecordAsync(AuditEventTypeEnum.SettingsChanged, null, 200, "Email feedback on " + t.TargetType + " " + t.TargetId + " (" + (action ?? "reason") + ") through link " + t.Id + ".", ctx.Token).ConfigureAwait(false);
        }

        private async Task<ItemText> ItemAsync(EmailActionToken t, HttpContextBase ctx)
        {
            string app = Server.Settings.Rest.AppBaseUrl + "/dashboard/";
            switch (t.TargetType)
            {
                case FeedbackTargetTypeEnum.Finding:
                    Finding? f = await Server.Database.Findings.ReadAsync(t.TenantId, t.TargetId, ctx.Token).ConfigureAwait(false);
                    return new ItemText { Title = f?.Title ?? "Finding", Summary = f?.Summary, OpenUrl = app + "findings/" + t.TargetId };
                case FeedbackTargetTypeEnum.Prediction:
                    Prediction? p = await Server.Database.Predictions.ReadAsync(t.TenantId, t.TargetId, ctx.Token).ConfigureAwait(false);
                    return new ItemText { Title = p?.Statement ?? "Prediction", Summary = p?.Rationale, OpenUrl = app + "predictions?id=" + t.TargetId };
                case FeedbackTargetTypeEnum.Suggestion:
                    Suggestion? s = await Server.Database.Suggestions.ReadAsync(t.TenantId, t.TargetId, ctx.Token).ConfigureAwait(false);
                    return new ItemText { Title = s?.Title ?? "Suggestion", Summary = s?.Description, OpenUrl = app + "suggestions?id=" + t.TargetId };
                default:
                    return new ItemText { Title = "Your feedback", OpenUrl = app + "today" };
            }
        }

        private async Task<UnsubscribeScope> ScopeAsync(EmailActionToken t, HttpContextBase ctx)
        {
            string[] parts = t.TargetId.Split(':', 2);
            EmailCategoryEnum category = Enum.TryParse(parts[0], out EmailCategoryEnum c) ? c : EmailCategoryEnum.RunEmails;
            string? topicId = parts.Length > 1 && parts[1].Length > 0 ? parts[1] : null;
            string? topicName = topicId == null ? null : (await Server.Database.Topics.ReadAsync(t.TenantId, topicId, ctx.Token).ConfigureAwait(false))?.Name;
            string what = category == EmailCategoryEnum.RunEmails ? "run emails" + (topicName != null ? " for " + topicName : String.Empty)
                : category == EmailCategoryEnum.BriefingEmails ? "briefing emails" : category == EmailCategoryEnum.FailureEmails ? "run failure emails" : "email from My Morning News";
            return new UnsubscribeScope { Category = category, TopicId = topicId, Description = what };
        }

        private async Task<bool> AllowAsync(HttpContextBase ctx)
        {
            string ip = ctx.Request.Source?.IpAddress ?? "unknown";
            if (_Limiter.TryAcquire("public:" + ip)) return true;
            await SendHtmlAsync(ctx, 429, PublicPages.TooMany(Logo())).ConfigureAwait(false);
            return false;
        }

        private async Task InvalidAsync(HttpContextBase ctx)
        {
            await SendHtmlAsync(ctx, 404, PublicPages.Invalid(Logo(), Server.Settings.Rest.AppBaseUrl + "/dashboard/today")).ConfigureAwait(false);
        }

        private string PageUrl(string kind, string secret)
        {
            return Server.Settings.Rest.PublicBaseUrl + "/v1.0/public/" + kind + "/" + Uri.EscapeDataString(secret);
        }

        private string Logo()
        {
            return Server.Settings.Rest.PublicBaseUrl + "/v1.0/public/assets/logo.png";
        }

        private static async Task SendHtmlAsync(HttpContextBase ctx, int status, string html)
        {
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.Headers.Add("Cache-Control", "no-store");
            ctx.Response.Headers.Add("X-Robots-Tag", "noindex, nofollow");
            ctx.Response.Headers.Add("Referrer-Policy", "no-referrer");
            ctx.Response.Headers.Add("Content-Security-Policy", "default-src 'none'; img-src 'self' https: data:; style-src 'unsafe-inline'; form-action 'self'");
            await ctx.Response.Send(Encoding.UTF8.GetBytes(html), ctx.Token).ConfigureAwait(false);
        }
    }
}
