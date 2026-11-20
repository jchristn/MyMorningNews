namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Analytics;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Learning and analytics (plan sections 18 to 20): learned preferences (write your own, accept or reject proposals,
    /// disable), feedback editing and retraction, a learning pass on demand, analytics reports, and ranking weight
    /// recommendations.
    /// </summary>
    public class LearningRoutes : RouteBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public LearningRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            const string p = "/v1.0/api/learned-preferences";
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, p, ListAsync, null, openApiMetadata: Paged(Doc("List learned preferences", "Learning")
                .WithParameter(QueryParam("topicId", typeof(string))).WithParameter(QueryParam("status", typeof(PreferenceStatusEnum))).WithParameter(QueryParam("origin", typeof(PreferenceOriginEnum)))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, p, CreateAsync, null, openApiMetadata: Doc("Write a preference (active at once)", "Learning").WithRequestBody(Body<PreferenceUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, p + "/{id}", ReadAsync, null, openApiMetadata: Doc("Read a preference with its supporting feedback", "Learning"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, p + "/{id}", UpdateAsync, null, openApiMetadata: Doc("Edit a preference", "Learning").WithRequestBody(Body<PreferenceUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, p + "/{id}", DeleteAsync, null, openApiMetadata: Doc("Delete a preference", "Learning"));
            foreach (string action in new[] { "accept", "reject", "disable", "enable" })
            {
                string a = action;
                webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, p + "/{id}/" + a, ctx => StatusAsync(ctx, a), null, openApiMetadata: Doc(Char.ToUpperInvariant(a[0]) + a.Substring(1) + " a preference", "Learning"));
            }

            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/topics/{id}/learn", LearnAsync, null, openApiMetadata: Doc("Run a learning pass for a topic now", "Learning"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/topics/{id}/ranking-recommendation", RankingAsync, null, openApiMetadata: Doc("Suggested ranking weights from your ratings", "Learning"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1.0/api/feedback/{id}", EditFeedbackAsync, null, openApiMetadata: Doc("Edit the reason of my feedback", "Feedback").WithRequestBody(Body<FeedbackUpdateRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/api/feedback/{id}", RetractFeedbackAsync, null, openApiMetadata: Doc("Retract my feedback (the earlier rating, if any, becomes current)", "Feedback"));

            const string a2 = "/v1.0/api/analytics/";
            foreach (string report in new[] { "overview", "relevance", "accuracy", "feedback", "suggestions", "reasons", "quality", "email" })
            {
                string r = report;
                webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, a2 + r, ctx => ReportAsync(ctx, r), null, openApiMetadata: Doc(Char.ToUpperInvariant(r[0]) + r.Substring(1) + " analytics", "Analytics",
                    "Defaults: my data for the last 90 days. Administrators can pass userId or omit it with all=true for the tenant.")
                    .WithParameter(QueryParam("topicId", typeof(string))).WithParameter(QueryParam("from", typeof(DateTime))).WithParameter(QueryParam("to", typeof(DateTime)))
                    .WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("all", typeof(bool))));
            }
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.PreferenceRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            LearnedPreferenceFilter filter = new LearnedPreferenceFilter
            {
                TenantId = tenantId, UserId = ListScope(ctx), TopicId = Query(ctx, "topicId"), Status = QueryEnum<PreferenceStatusEnum>(ctx, "status"), Origin = QueryEnum<PreferenceOriginEnum>(ctx, "origin")
            };
            ApplyPaging(ctx, filter);
            await SendJsonAsync(ctx, 200, await Server.Database.LearnedPreferences.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task CreateAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.PreferenceWrite).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            PreferenceUpsertRequest? req = await ReadBodyAsync<PreferenceUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            if (String.IsNullOrWhiteSpace(req.Statement) || req.Statement.Trim().Length > 500)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "statement is required (up to 500 characters).").ConfigureAwait(false);
                return;
            }

            string userId = Context(ctx).UserId ?? String.Empty;
            string? topicId = await TopicAsync(ctx, tenantId, userId, req.TopicId).ConfigureAwait(false);
            if (topicId == "!") return;
            LearnedPreference created = await Server.Database.LearnedPreferences.CreateAsync(new LearnedPreference
            {
                TenantId = tenantId, UserId = userId, TopicId = topicId, Kind = req.Kind ?? PreferenceKindEnum.Prefer, Statement = req.Statement.Trim(),
                Status = PreferenceStatusEnum.Active, Origin = PreferenceOriginEnum.User, Confidence = 1, DecidedUtc = DateTime.UtcNow
            }, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, created).ConfigureAwait(false);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            LearnedPreference? lp = await OwnedAsync(ctx, RequestTypeEnum.PreferenceRead).ConfigureAwait(false);
            if (lp == null) return;
            List<Feedback> support = new List<Feedback>();
            foreach (LearnedPreferenceFeedback link in (await Server.Database.LearnedPreferenceFeedback.EnumerateAsync(new LearnedPreferenceFeedbackFilter { TenantId = lp.TenantId, LearnedPreferenceId = lp.Id, PageSize = 200 }, ctx.Token).ConfigureAwait(false)).Items)
            {
                Feedback? f = await Server.Database.Feedback.ReadAsync(lp.TenantId, link.FeedbackId, ctx.Token).ConfigureAwait(false);
                if (f != null) support.Add(f);
            }

            await SendJsonAsync(ctx, 200, new PreferenceDetailResponse { Preference = lp, Support = support.OrderByDescending(f => f.CreatedUtc).ToList() }).ConfigureAwait(false);
        }

        private async Task UpdateAsync(HttpContextBase ctx)
        {
            LearnedPreference? lp = await OwnedAsync(ctx, RequestTypeEnum.PreferenceWrite).ConfigureAwait(false);
            if (lp == null) return;
            PreferenceUpsertRequest? req = await ReadBodyAsync<PreferenceUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            if (req.Statement != null)
            {
                if (req.Statement.Trim().Length == 0 || req.Statement.Trim().Length > 500) { await SendErrorAsync(ctx, 400, "ValidationFailed", "statement must be 1 to 500 characters.").ConfigureAwait(false); return; }
                lp.Statement = req.Statement.Trim();
            }

            if (req.Kind.HasValue) lp.Kind = req.Kind.Value;
            if (req.TopicId != null)
            {
                string? topicId = await TopicAsync(ctx, lp.TenantId, lp.UserId, req.TopicId).ConfigureAwait(false);
                if (topicId == "!") return;
                lp.TopicId = topicId;
            }

            await SendJsonAsync(ctx, 200, await Server.Database.LearnedPreferences.UpdateAsync(lp, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            LearnedPreference? lp = await OwnedAsync(ctx, RequestTypeEnum.PreferenceWrite).ConfigureAwait(false);
            if (lp == null) return;
            foreach (LearnedPreferenceFeedback link in (await Server.Database.LearnedPreferenceFeedback.EnumerateAsync(new LearnedPreferenceFeedbackFilter { TenantId = lp.TenantId, LearnedPreferenceId = lp.Id, PageSize = 500 }, ctx.Token).ConfigureAwait(false)).Items)
                await Server.Database.LearnedPreferenceFeedback.DeleteAsync(link.TenantId, link.Id, ctx.Token).ConfigureAwait(false);
            await Server.Database.LearnedPreferences.DeleteAsync(lp.TenantId, lp.Id, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task StatusAsync(HttpContextBase ctx, string action)
        {
            LearnedPreference? lp = await OwnedAsync(ctx, RequestTypeEnum.PreferenceWrite).ConfigureAwait(false);
            if (lp == null) return;
            PreferenceStatusEnum next = action == "accept" || action == "enable" ? PreferenceStatusEnum.Active : action == "reject" ? PreferenceStatusEnum.Rejected : PreferenceStatusEnum.Disabled;
            if ((action == "accept" || action == "reject") && lp.Status != PreferenceStatusEnum.Proposed)
            {
                await SendErrorAsync(ctx, 409, "Conflict", "Only proposed preferences can be accepted or rejected.").ConfigureAwait(false);
                return;
            }

            lp.Status = next;
            lp.DecidedUtc = DateTime.UtcNow;
            await SendJsonAsync(ctx, 200, await Server.Database.LearnedPreferences.UpdateAsync(lp, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task LearnAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.PreferenceWrite).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            Topic? topic = await Server.Database.Topics.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (topic == null || !IsOwnerOrAdmin(ctx, topic.UserId)) { await SendNotFoundAsync(ctx, "Topic").ConfigureAwait(false); return; }
            int proposed = await Server.Learning.LearnAsync(tenantId, topic.UserId, topic.Id, DateTime.UtcNow, ctx.Token).ConfigureAwait(false);
            List<LearnedPreference> prefs = (await Server.Database.LearnedPreferences.EnumerateAsync(new LearnedPreferenceFilter { TenantId = tenantId, UserId = topic.UserId, TopicId = topic.Id, PageSize = 100 }, ctx.Token).ConfigureAwait(false)).Items;
            await SendJsonAsync(ctx, 200, new { proposed = proposed, preferences = prefs }).ConfigureAwait(false);
        }

        private async Task RankingAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.AnalyticsRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            Topic? topic = await Server.Database.Topics.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (topic == null || !IsOwnerOrAdmin(ctx, topic.UserId)) { await SendNotFoundAsync(ctx, "Topic").ConfigureAwait(false); return; }
            await SendJsonAsync(ctx, 200, await Server.Analytics.RankingAsync(topic, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task EditFeedbackAsync(HttpContextBase ctx)
        {
            Feedback? f = await OwnedFeedbackAsync(ctx).ConfigureAwait(false);
            if (f == null) return;
            FeedbackUpdateRequest? req = await ReadBodyAsync<FeedbackUpdateRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            string? reason = String.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason.Trim();
            if (reason != null && reason.Length > MyMorningNews.Core.Feedback.FeedbackService.MaxReasonLength) reason = reason.Substring(0, MyMorningNews.Core.Feedback.FeedbackService.MaxReasonLength);
            f.Reason = reason;
            await SendJsonAsync(ctx, 200, await Server.Database.Feedback.UpdateAsync(f, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task RetractFeedbackAsync(HttpContextBase ctx)
        {
            Feedback? f = await OwnedFeedbackAsync(ctx).ConfigureAwait(false);
            if (f == null) return;
            if (f.IsCurrent && f.SupersedesFeedbackId != null)
            {
                Feedback? previous = await Server.Database.Feedback.ReadAsync(f.TenantId, f.SupersedesFeedbackId, ctx.Token).ConfigureAwait(false);
                if (previous != null)
                {
                    previous.IsCurrent = true;
                    await Server.Database.Feedback.UpdateAsync(previous, ctx.Token).ConfigureAwait(false);
                }
            }

            foreach (FeedbackReasonTag t in (await Server.Database.FeedbackReasonTags.EnumerateAsync(new FeedbackReasonTagFilter { TenantId = f.TenantId, FeedbackId = f.Id, PageSize = 50 }, ctx.Token).ConfigureAwait(false)).Items)
                await Server.Database.FeedbackReasonTags.DeleteAsync(t.TenantId, t.Id, ctx.Token).ConfigureAwait(false);
            foreach (LearnedPreferenceFeedback l in (await Server.Database.LearnedPreferenceFeedback.EnumerateAsync(new LearnedPreferenceFeedbackFilter { TenantId = f.TenantId, FeedbackId = f.Id, PageSize = 50 }, ctx.Token).ConfigureAwait(false)).Items)
                await Server.Database.LearnedPreferenceFeedback.DeleteAsync(l.TenantId, l.Id, ctx.Token).ConfigureAwait(false);
            await Server.Database.Feedback.DeleteAsync(f.TenantId, f.Id, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task ReportAsync(HttpContextBase ctx, string report)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.AnalyticsRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            RequestContext rc = Context(ctx);
            bool admin = rc.IsAdmin || rc.IsTenantAdmin;
            string? userId = admin ? (QueryBool(ctx, "all") == true ? null : Query(ctx, "userId") ?? rc.UserId) : rc.UserId;
            string? topicId = Query(ctx, "topicId");
            if (topicId != null)
            {
                Topic? topic = await Server.Database.Topics.ReadAsync(tenantId, topicId, ctx.Token).ConfigureAwait(false);
                if (topic == null || !IsOwnerOrAdmin(ctx, topic.UserId)) { await SendNotFoundAsync(ctx, "Topic").ConfigureAwait(false); return; }
            }

            DateTime to = QueryDate(ctx, "to") ?? DateTime.UtcNow;
            AnalyticsScope scope = new AnalyticsScope { TenantId = tenantId, UserId = userId, TopicId = topicId, FromUtc = QueryDate(ctx, "from") ?? to.AddDays(-90), ToUtc = to };
            object result = report switch
            {
                "overview" => await Server.Analytics.OverviewAsync(scope, ctx.Token).ConfigureAwait(false),
                "relevance" => await Server.Analytics.RelevanceAsync(scope, ctx.Token).ConfigureAwait(false),
                "accuracy" => await Server.Analytics.AccuracyAsync(scope, ctx.Token).ConfigureAwait(false),
                "feedback" => await Server.Analytics.FeedbackAsync(scope, ctx.Token).ConfigureAwait(false),
                "suggestions" => await Server.Analytics.SuggestionsAsync(scope, ctx.Token).ConfigureAwait(false),
                "reasons" => await Server.Analytics.ReasonsAsync(scope, ctx.Token).ConfigureAwait(false),
                "email" => await Server.Analytics.EmailAsync(scope, ctx.Token).ConfigureAwait(false),
                _ => await Server.Analytics.QualityAsync(scope, ctx.Token).ConfigureAwait(false)
            };
            await SendJsonAsync(ctx, 200, result).ConfigureAwait(false);
        }

        private async Task<string?> TopicAsync(HttpContextBase ctx, string tenantId, string userId, string? topicId)
        {
            if (String.IsNullOrEmpty(topicId)) return null;
            Topic? t = await Server.Database.Topics.ReadAsync(tenantId, topicId, ctx.Token).ConfigureAwait(false);
            if (t == null || t.UserId != userId)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "The topic was not found among yours.").ConfigureAwait(false);
                return "!";
            }

            return t.Id;
        }

        private async Task<LearnedPreference?> OwnedAsync(HttpContextBase ctx, RequestTypeEnum type)
        {
            if (!await AuthorizeAsync(ctx, type).ConfigureAwait(false)) return null;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return null;
            LearnedPreference? lp = await Server.Database.LearnedPreferences.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (lp == null || !IsOwnerOrAdmin(ctx, lp.UserId)) { await SendNotFoundAsync(ctx, "Preference").ConfigureAwait(false); return null; }
            return lp;
        }

        private async Task<Feedback?> OwnedFeedbackAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.FeedbackWrite).ConfigureAwait(false)) return null;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return null;
            Feedback? f = await Server.Database.Feedback.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (f == null || f.UserId != Context(ctx).UserId) { await SendNotFoundAsync(ctx, "Feedback").ConfigureAwait(false); return null; }
            return f;
        }
    }
}
