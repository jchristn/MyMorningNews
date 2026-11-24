namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Security;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// One-time requests (plan section 13.1): reports, podcasts, and email summaries researched once, now or at a set time.
    /// </summary>
    public class ResearchRequestRoutes : RouteBase
    {
        private const string B = "/v1.0/api/requests";

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public ResearchRequestRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, B, CreateAsync, null, openApiMetadata: Doc("Create a one-time request", "Requests",
                "Plans the request (title, brief, dimensions, seed queries) and returns at most three clarifying questions. Status Declined when it is not a research request.")
                .WithRequestBody(Body<ResearchRequestCreateRequest>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, B, ListAsync, null, openApiMetadata: Paged(Doc("List my one-time requests", "Requests").WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("mine", typeof(bool))).WithParameter(QueryParam("status", typeof(ResearchRequestStatusEnum)))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, B + "/enumerate", ctx => EnumerateAsync(ctx, ListAsync), null, openApiMetadata: Paged(Doc("List my one-time requests", "Requests").WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("mine", typeof(bool))).WithParameter(QueryParam("status", typeof(ResearchRequestStatusEnum)))).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, B + "/{id}", ReadAsync, null, openApiMetadata: Doc("Read a request with its plan and questions", "Requests"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, B + "/{id}/questions/{questionId}", AnswerAsync, null, openApiMetadata: Doc("Answer a clarifying question", "Requests").WithRequestBody(Body<QuestionAnswerRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, B + "/{id}/replan", ReplanAsync, null, openApiMetadata: Doc("Plan again with the answers given", "Requests"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, B + "/{id}/confirm", ConfirmAsync, null, openApiMetadata: Doc("Confirm: run once now or at runAtUtc", "Requests").WithRequestBody(Body<ResearchRequestConfirmRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, B + "/{id}/cancel", CancelAsync, null, openApiMetadata: Doc("Cancel a request that has not started", "Requests"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, B + "/{id}/convert", ConvertAsync, null, openApiMetadata: Doc("Make this a recurring topic", "Requests"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, B + "/{id}", DeleteAsync, null, openApiMetadata: Doc("Delete a request", "Requests"));
        }

        private async Task CreateAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.ResearchRequestWrite).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            RequestContext rc = Context(ctx);
            User? user = String.IsNullOrEmpty(rc.UserId) ? null : await Server.Database.Users.ReadAsync(tenantId, rc.UserId, ctx.Token).ConfigureAwait(false);
            if (user == null)
            {
                await SendErrorAsync(ctx, 400, "BadRequest", "Requests belong to a signed-in user.").ConfigureAwait(false);
                return;
            }

            ResearchRequestCreateRequest? req = await ReadBodyAsync<ResearchRequestCreateRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            await Server.Setup.RequireAsync(tenantId, ModelTaskEnum.Refinement, null, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, await Server.ResearchRequests.CreateAsync(tenantId, user, req, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.ResearchRequestRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            RequestContext rc = Context(ctx);
            ResearchRequestFilter filter = new ResearchRequestFilter
            {
                TenantId = tenantId,
                UserId = ListScope(ctx),
                Status = QueryEnum<ResearchRequestStatusEnum>(ctx, "status")
            };
            ApplyPaging(ctx, filter);
            await SendJsonAsync(ctx, 200, await Server.Database.ResearchRequests.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            ResearchRequest? rr = await OwnedAsync(ctx, RequestTypeEnum.ResearchRequestRead).ConfigureAwait(false);
            if (rr == null) return;
            await SendJsonAsync(ctx, 200, await Server.ResearchRequests.GetAsync(rr.TenantId, rr.Id, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task AnswerAsync(HttpContextBase ctx)
        {
            ResearchRequest? rr = await OwnedAsync(ctx, RequestTypeEnum.ResearchRequestWrite).ConfigureAwait(false);
            if (rr == null) return;
            QuestionAnswerRequest? req = await ReadBodyAsync<QuestionAnswerRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            await SendJsonAsync(ctx, 200, await Server.ResearchRequests.AnswerAsync(rr.TenantId, rr.Id, Param(ctx, "questionId") ?? String.Empty, req, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ReplanAsync(HttpContextBase ctx)
        {
            ResearchRequest? rr = await OwnedAsync(ctx, RequestTypeEnum.ResearchRequestWrite).ConfigureAwait(false);
            if (rr == null) return;
            await SendJsonAsync(ctx, 200, await Server.ResearchRequests.ReplanAsync(rr.TenantId, rr.Id, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ConfirmAsync(HttpContextBase ctx)
        {
            ResearchRequest? rr = await OwnedAsync(ctx, RequestTypeEnum.ResearchRequestWrite).ConfigureAwait(false);
            if (rr == null) return;
            ResearchRequestConfirmRequest req = new ResearchRequestConfirmRequest();
            if (!String.IsNullOrWhiteSpace(ctx.Request.DataAsString))
            {
                ResearchRequestConfirmRequest? parsed = await ReadBodyAsync<ResearchRequestConfirmRequest>(ctx).ConfigureAwait(false);
                if (parsed == null) return;
                req = parsed;
            }

            await SendJsonAsync(ctx, 200, await Server.ResearchRequests.ConfirmAsync(rr.TenantId, rr.Id, req.RunAtUtc, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task CancelAsync(HttpContextBase ctx)
        {
            ResearchRequest? rr = await OwnedAsync(ctx, RequestTypeEnum.ResearchRequestWrite).ConfigureAwait(false);
            if (rr == null) return;
            await SendJsonAsync(ctx, 200, await Server.ResearchRequests.CancelAsync(rr.TenantId, rr.Id, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ConvertAsync(HttpContextBase ctx)
        {
            ResearchRequest? rr = await OwnedAsync(ctx, RequestTypeEnum.ResearchRequestWrite).ConfigureAwait(false);
            if (rr == null) return;
            await SendJsonAsync(ctx, 200, await Server.ResearchRequests.ConvertAsync(rr.TenantId, rr.Id, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            ResearchRequest? rr = await OwnedAsync(ctx, RequestTypeEnum.ResearchRequestDelete).ConfigureAwait(false);
            if (rr == null) return;
            await Server.ResearchRequests.DeleteAsync(rr.TenantId, rr.Id, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task<ResearchRequest?> OwnedAsync(HttpContextBase ctx, RequestTypeEnum type)
        {
            if (!await AuthorizeAsync(ctx, type).ConfigureAwait(false)) return null;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return null;
            ResearchRequest? rr = await Server.Database.ResearchRequests.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (rr == null || !IsOwnerOrAdmin(ctx, rr.UserId))
            {
                await SendNotFoundAsync(ctx, "Request").ConfigureAwait(false);
                return null;
            }

            return rr;
        }
    }
}
