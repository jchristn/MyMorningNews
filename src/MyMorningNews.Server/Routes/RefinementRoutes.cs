namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// The topic refinement wizard (plan section 13). Each generation step returns the full session state so a client
    /// can render the step rail, dimensions, questions, and context from one response.
    /// </summary>
    public class RefinementRoutes : RouteBase
    {
        private const string B = "/v1.0/api/refinement-sessions";

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public RefinementRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, B, StartAsync, null, openApiMetadata: Doc("Start the wizard for a new topic", "Refinement",
                "Creates the topic in Draft status and returns the session state.").WithRequestBody(Body<RefinementStartRequest>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, B, ListAsync, null, openApiMetadata: Paged(Doc("List my refinement sessions", "Refinement").WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("mine", typeof(bool))).WithParameter(QueryParam("status", typeof(RefinementStatusEnum))).WithParameter(QueryParam("topicId", typeof(string)))));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, B + "/{id}", ReadAsync, null, openApiMetadata: Doc("Read the session state", "Refinement"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, B + "/{id}/dimensions", DimensionsAsync, null, openApiMetadata: Doc("Propose dimensions (keeps locked and your own)", "Refinement").WithRequestBody(Body<RefinementGenerateRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, B + "/{id}/questions", QuestionsAsync, null, openApiMetadata: Doc("Ask the next round of questions", "Refinement").WithRequestBody(Body<RefinementGenerateRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, B + "/{id}/questions/{questionId}", AnswerAsync, null, openApiMetadata: Doc("Answer or skip a question", "Refinement").WithRequestBody(Body<QuestionAnswerRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, B + "/{id}/context", ContextAsync, null, openApiMetadata: Doc("Draft the brief and constraints", "Refinement"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, B + "/{id}/sources", SourcesAsync, null, openApiMetadata: Doc("Suggest feeds and seed pages, checked with a live fetch", "Refinement"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, B + "/{id}/sources/accept", AcceptSourcesAsync, null, openApiMetadata: Doc("Add accepted sources to the topic", "Refinement").WithRequestBody(Body<SourceAcceptRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, B + "/{id}/complete", CompleteAsync, null, openApiMetadata: Doc("Apply the topic (Draft becomes Active)", "Refinement"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, B + "/{id}/abandon", AbandonAsync, null, openApiMetadata: Doc("Abandon the session (deletes a new draft topic)", "Refinement"));
        }

        private async Task StartAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.RefinementWrite).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            RequestContext rc = Context(ctx);
            if (String.IsNullOrEmpty(rc.UserId))
            {
                await SendErrorAsync(ctx, 400, "BadRequest", "The wizard needs a signed-in user.").ConfigureAwait(false);
                return;
            }

            await CheckTopicLimitAsync(tenantId, rc.UserId, ctx.Token).ConfigureAwait(false);

            RefinementStartRequest? req = await ReadBodyAsync<RefinementStartRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            await SendJsonAsync(ctx, 201, await Server.Refinement.StartAsync(tenantId, rc.UserId, req, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.RefinementRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            RequestContext rc = Context(ctx);
            RefinementSessionFilter filter = new RefinementSessionFilter
            {
                TenantId = tenantId,
                UserId = ListScope(ctx),
                TopicId = Query(ctx, "topicId"),
                Status = QueryEnum<RefinementStatusEnum>(ctx, "status")
            };
            ApplyPaging(ctx, filter);
            await SendJsonAsync(ctx, 200, await Server.Database.RefinementSessions.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            RefinementSession? s = await SessionAsync(ctx, RequestTypeEnum.RefinementRead).ConfigureAwait(false);
            if (s == null) return;
            await SendJsonAsync(ctx, 200, await Server.Refinement.GetStateAsync(s.TenantId, s.Id, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task DimensionsAsync(HttpContextBase ctx)
        {
            RefinementSession? s = await SessionAsync(ctx, RequestTypeEnum.RefinementWrite).ConfigureAwait(false);
            if (s == null) return;
            RefinementGenerateRequest? req = await OptionalBodyAsync(ctx).ConfigureAwait(false);
            if (req == null) return;
            await SendJsonAsync(ctx, 200, await Server.Refinement.ProposeDimensionsAsync(s.TenantId, CallerOrOwner(ctx, s), s.Id, req.Guidance, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task QuestionsAsync(HttpContextBase ctx)
        {
            RefinementSession? s = await SessionAsync(ctx, RequestTypeEnum.RefinementWrite).ConfigureAwait(false);
            if (s == null) return;
            RefinementGenerateRequest? req = await OptionalBodyAsync(ctx).ConfigureAwait(false);
            if (req == null) return;
            await SendJsonAsync(ctx, 200, await Server.Refinement.AskQuestionsAsync(s.TenantId, CallerOrOwner(ctx, s), s.Id, req.Guidance, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task AnswerAsync(HttpContextBase ctx)
        {
            RefinementSession? s = await SessionAsync(ctx, RequestTypeEnum.RefinementWrite).ConfigureAwait(false);
            if (s == null) return;
            QuestionAnswerRequest? req = await ReadBodyAsync<QuestionAnswerRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            await SendJsonAsync(ctx, 200, await Server.Refinement.AnswerAsync(s.TenantId, s.Id, Param(ctx, "questionId") ?? String.Empty, req, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ContextAsync(HttpContextBase ctx)
        {
            RefinementSession? s = await SessionAsync(ctx, RequestTypeEnum.RefinementWrite).ConfigureAwait(false);
            if (s == null) return;
            await SendJsonAsync(ctx, 200, await Server.Refinement.SynthesizeAsync(s.TenantId, CallerOrOwner(ctx, s), s.Id, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task SourcesAsync(HttpContextBase ctx)
        {
            RefinementSession? s = await SessionAsync(ctx, RequestTypeEnum.RefinementWrite).ConfigureAwait(false);
            if (s == null) return;
            List<SourceCheck> checks = await Server.Refinement.SuggestSourcesAsync(s.TenantId, CallerOrOwner(ctx, s), s.Id, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, checks).ConfigureAwait(false);
        }

        private async Task AcceptSourcesAsync(HttpContextBase ctx)
        {
            RefinementSession? s = await SessionAsync(ctx, RequestTypeEnum.RefinementWrite).ConfigureAwait(false);
            if (s == null) return;
            SourceAcceptRequest? req = await ReadBodyAsync<SourceAcceptRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            await SendJsonAsync(ctx, 200, await Server.Refinement.AcceptSourcesAsync(s.TenantId, s.Id, req, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task CompleteAsync(HttpContextBase ctx)
        {
            RefinementSession? s = await SessionAsync(ctx, RequestTypeEnum.RefinementWrite).ConfigureAwait(false);
            if (s == null) return;
            await SendJsonAsync(ctx, 200, await Server.Refinement.CompleteAsync(s.TenantId, s.Id, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task AbandonAsync(HttpContextBase ctx)
        {
            RefinementSession? s = await SessionAsync(ctx, RequestTypeEnum.RefinementWrite).ConfigureAwait(false);
            if (s == null) return;
            await Server.Refinement.AbandonAsync(s.TenantId, s.Id, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task<RefinementSession?> SessionAsync(HttpContextBase ctx, RequestTypeEnum type)
        {
            if (!await AuthorizeAsync(ctx, type).ConfigureAwait(false)) return null;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return null;
            RefinementSession? s = await Server.Database.RefinementSessions.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (s == null || !IsOwnerOrAdmin(ctx, s.UserId))
            {
                await SendNotFoundAsync(ctx, "Refinement session").ConfigureAwait(false);
                return null;
            }

            return s;
        }

        private static async Task<RefinementGenerateRequest?> OptionalBodyAsync(HttpContextBase ctx)
        {
            if (String.IsNullOrWhiteSpace(ctx.Request.DataAsString)) return new RefinementGenerateRequest();
            return await ReadBodyAsync<RefinementGenerateRequest>(ctx).ConfigureAwait(false);
        }

        private static string CallerOrOwner(HttpContextBase ctx, RefinementSession s)
        {
            return Context(ctx).UserId ?? s.UserId;
        }
    }
}
