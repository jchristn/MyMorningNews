namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Keep-or-delete prompts for the caller's topics that stopped producing new findings.
    /// </summary>
    public class TopicReviewRoutes : RouteBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public TopicReviewRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            const string b = "/v1.0/api/topic-reviews";
            const string listHelp = "Pending keep-or-delete prompts for my topics, earliest deletion date first. A recurring topic gets one when its runs found nothing new for " +
                "topicLifecycle.staleDays (default 7). Without Keep by deleteAfterUtc (default 14 days later) the topic is deleted.";
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, b, ListAsync, null, openApiMetadata: Paged(Doc("List my pending topic reviews", "Topic reviews", listHelp)));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, b + "/enumerate", ctx => EnumerateAsync(ctx, ListAsync), null,
                openApiMetadata: Paged(Doc("List my pending topic reviews", "Topic reviews", listHelp)).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, b + "/{id}", ReadAsync, null, openApiMetadata: Doc("Read one of my topic reviews", "Topic reviews"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, b + "/{id}/keep", KeepAsync, null, openApiMetadata: Doc("Keep the topic", "Topic reviews",
                "Closes the review; no new prompt for topicLifecycle.keepDays (default 7). Idempotent. 409 when the topic was already deleted."));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, b + "/{id}/delete", DeleteAsync, null, openApiMetadata: Doc("Delete the topic", "Topic reviews",
                "Deletes the topic with its briefings, findings, and files. Returns 204. 409 when the review was already answered with Keep or closed by new findings."));
        }

        private async Task<RequestContext?> UserAsync(HttpContextBase ctx, RequestTypeEnum type)
        {
            if (!await AuthorizeAsync(ctx, type).ConfigureAwait(false)) return null;
            RequestContext rc = Context(ctx);
            if (String.IsNullOrEmpty(rc.UserId) || String.IsNullOrEmpty(rc.TenantId))
            {
                await SendErrorAsync(ctx, 400, "BadRequest", "Topic reviews belong to a signed-in user.").ConfigureAwait(false);
                return null;
            }

            return rc;
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            RequestContext? rc = await UserAsync(ctx, RequestTypeEnum.TopicRead).ConfigureAwait(false);
            if (rc == null) return;
            TopicReviewFilter filter = new TopicReviewFilter { TenantId = rc.TenantId };
            ApplyPaging(ctx, filter);
            await SendJsonAsync(ctx, 200, await Server.TopicLifecycle.ListPendingAsync(rc.TenantId!, rc.UserId!, filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            RequestContext? rc = await UserAsync(ctx, RequestTypeEnum.TopicRead).ConfigureAwait(false);
            if (rc == null) return;
            TopicReviewResponse? review = await Server.TopicLifecycle.ReadAsync(rc.TenantId!, rc.UserId!, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (review == null) { await SendNotFoundAsync(ctx, "Topic review").ConfigureAwait(false); return; }
            await SendJsonAsync(ctx, 200, review).ConfigureAwait(false);
        }

        private async Task KeepAsync(HttpContextBase ctx)
        {
            RequestContext? rc = await UserAsync(ctx, RequestTypeEnum.TopicWrite).ConfigureAwait(false);
            if (rc == null) return;
            try
            {
                TopicReviewResponse? review = await Server.TopicLifecycle.KeepAsync(rc.TenantId!, rc.UserId!, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
                if (review == null) { await SendNotFoundAsync(ctx, "Topic review").ConfigureAwait(false); return; }
                await SendJsonAsync(ctx, 200, review).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                await SendErrorAsync(ctx, 409, "Conflict", ex.Message).ConfigureAwait(false);
            }
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            RequestContext? rc = await UserAsync(ctx, RequestTypeEnum.TopicDelete).ConfigureAwait(false);
            if (rc == null) return;
            try
            {
                if (!await Server.TopicLifecycle.DeleteTopicAsync(rc.TenantId!, rc.UserId!, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false))
                {
                    await SendNotFoundAsync(ctx, "Topic review").ConfigureAwait(false);
                    return;
                }

                await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                await SendErrorAsync(ctx, 409, "Conflict", ex.Message).ConfigureAwait(false);
            }
        }
    }
}
