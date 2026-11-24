namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Prompts;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Services;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Prompt template routes: list, read, save a version, version history, rollback, reset to default, topic overrides,
    /// and the prompt lab (dry-run execution logged as such).
    /// </summary>
    public class PromptRoutes : RouteBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public PromptRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            string b = "/v1.0/api/prompts";
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, b, ListAsync, null, openApiMetadata: Paged(Doc("List prompt templates", "Prompts").WithParameter(QueryParam("key", typeof(string))).WithParameter(QueryParam("topicId", typeof(string))).WithParameter(QueryParam("isCustomized", typeof(bool))).WithParameter(QueryParam("tenantOnly", typeof(bool))).WithParameter(QueryParam("role", typeof(ModelTaskEnum)))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, b + "/enumerate", ctx => EnumerateAsync(ctx, ListAsync), null, openApiMetadata: Paged(Doc("List prompt templates", "Prompts").WithParameter(QueryParam("key", typeof(string))).WithParameter(QueryParam("topicId", typeof(string))).WithParameter(QueryParam("isCustomized", typeof(bool))).WithParameter(QueryParam("tenantOnly", typeof(bool))).WithParameter(QueryParam("role", typeof(ModelTaskEnum)))).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, b + "/lab", LabAsync, null, openApiMetadata: Doc("Run a prompt in the prompt lab", "Prompts").WithRequestBody(Body<PromptLabRequest>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, b + "/overrides", CreateOverrideAsync, null, openApiMetadata: Doc("Create a topic override of a prompt", "Prompts").WithRequestBody(Body<PromptOverrideRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, b + "/{id}", ReadAsync, null, openApiMetadata: Doc("Read a prompt template with its content", "Prompts"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, b + "/{id}", SaveAsync, null, openApiMetadata: Doc("Save a new version", "Prompts").WithRequestBody(Body<PromptTemplateSaveRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, b + "/{id}", DeleteAsync, null, openApiMetadata: Doc("Delete a topic override", "Prompts"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, b + "/{id}/versions", VersionsAsync, null, openApiMetadata: Paged(Doc("Version history", "Prompts")));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, b + "/{id}/versions" + "/enumerate", ctx => EnumerateAsync(ctx, VersionsAsync), null, openApiMetadata: Paged(Doc("Version history", "Prompts")).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, b + "/{id}/rollback", RollbackAsync, null, openApiMetadata: Doc("Restore an earlier version (query: version)", "Prompts").WithParameter(QueryParam("version", typeof(int))));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, b + "/{id}/reset", ResetAsync, null, openApiMetadata: Doc("Restore the shipped default", "Prompts"));
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.PromptRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            PromptTemplateFilter filter = new PromptTemplateFilter { TenantId = tenantId, Key = Query(ctx, "key"), TopicId = Query(ctx, "topicId"), Role = QueryEnum<ModelTaskEnum>(ctx, "role"), IsCustomized = QueryBool(ctx, "isCustomized"), TenantOnly = QueryBool(ctx, "tenantOnly"), SortBy = "key", SortDirection = SortDirectionEnum.Ascending };
            ApplyPaging(ctx, filter);
            EnumerationResult<PromptTemplate> page = await Server.Database.PromptTemplates.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, page).ConfigureAwait(false);
        }

        private async Task<PromptTemplate?> LoadAsync(HttpContextBase ctx)
        {
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return null;
            PromptTemplate? t = await Server.Database.PromptTemplates.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (t == null) await SendNotFoundAsync(ctx, "Prompt template").ConfigureAwait(false);
            return t;
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.PromptRead, Param(ctx, "id")).ConfigureAwait(false)) return;
            PromptTemplate? t = await LoadAsync(ctx).ConfigureAwait(false);
            if (t == null) return;
            await SendJsonAsync(ctx, 200, await Server.Prompts.DescribeAsync(t, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task SaveAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.PromptWrite, Param(ctx, "id")).ConfigureAwait(false)) return;
            PromptTemplateSaveRequest? req = await ReadBodyAsync<PromptTemplateSaveRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            PromptTemplate? t = await LoadAsync(ctx).ConfigureAwait(false);
            if (t == null) return;
            Dictionary<string, string> errors = PromptService.Validate(t.Key, req.Content);
            if (errors.Count > 0)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "The prompt is not valid.", errors).ConfigureAwait(false);
                return;
            }

            await Server.Prompts.SaveAsync(t, req.Content, req.ChangeNote, Context(ctx).UserId, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, await Server.Prompts.DescribeAsync(t, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.PromptDelete, Param(ctx, "id")).ConfigureAwait(false)) return;
            PromptTemplate? t = await LoadAsync(ctx).ConfigureAwait(false);
            if (t == null) return;
            if (t.TopicId == null)
            {
                await SendErrorAsync(ctx, 409, "Conflict", "Tenant prompts cannot be deleted; reset them to the default instead.").ConfigureAwait(false);
                return;
            }

            await Server.Database.PromptTemplates.DeleteAsync(t.TenantId, t.Id, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task VersionsAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.PromptRead, Param(ctx, "id")).ConfigureAwait(false)) return;
            PromptTemplate? t = await LoadAsync(ctx).ConfigureAwait(false);
            if (t == null) return;
            PromptTemplateVersionFilter query = new PromptTemplateVersionFilter { TenantId = t.TenantId, PromptTemplateId = t.Id, SortBy = "version" };
            ApplyPaging(ctx, query);
            await SendJsonAsync(ctx, 200, await Server.Database.PromptTemplateVersions.EnumerateAsync(query, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task RollbackAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.PromptWrite, Param(ctx, "id")).ConfigureAwait(false)) return;
            int? version = QueryInt(ctx, "version");
            if (!version.HasValue)
            {
                await SendErrorAsync(ctx, 400, "BadRequest", "version is required.").ConfigureAwait(false);
                return;
            }

            PromptTemplate? t = await LoadAsync(ctx).ConfigureAwait(false);
            if (t == null) return;
            try
            {
                await Server.Prompts.RollbackAsync(t, version.Value, Context(ctx).UserId, ctx.Token).ConfigureAwait(false);
            }
            catch (KeyNotFoundException ex)
            {
                await SendErrorAsync(ctx, 404, "NotFound", ex.Message).ConfigureAwait(false);
                return;
            }

            await SendJsonAsync(ctx, 200, await Server.Prompts.DescribeAsync(t, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ResetAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.PromptWrite, Param(ctx, "id")).ConfigureAwait(false)) return;
            PromptTemplate? t = await LoadAsync(ctx).ConfigureAwait(false);
            if (t == null) return;
            await Server.Prompts.ResetAsync(t, Context(ctx).UserId, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, await Server.Prompts.DescribeAsync(t, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task CreateOverrideAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.PromptWrite).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            PromptOverrideRequest? req = await ReadBodyAsync<PromptOverrideRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            if (DefaultPromptCatalog.Get(req.Key) == null || String.IsNullOrWhiteSpace(req.TopicId))
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "A known key and a topic are required.").ConfigureAwait(false);
                return;
            }

            if (await Server.Prompts.FindAsync(tenantId, req.Key, req.TopicId, ctx.Token).ConfigureAwait(false) != null)
            {
                await SendErrorAsync(ctx, 409, "Conflict", "The topic already overrides " + req.Key + ".").ConfigureAwait(false);
                return;
            }

            if (req.Content != null)
            {
                Dictionary<string, string> errors = PromptService.Validate(req.Key, req.Content);
                if (errors.Count > 0)
                {
                    await SendErrorAsync(ctx, 400, "ValidationFailed", "The prompt is not valid.", errors).ConfigureAwait(false);
                    return;
                }
            }

            PromptTemplate t = await Server.Prompts.CreateOverrideAsync(tenantId, req.Key, req.TopicId, req.Content, Context(ctx).UserId, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, await Server.Prompts.DescribeAsync(t, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task LabAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.PromptExecute).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            PromptLabRequest? req = await ReadBodyAsync<PromptLabRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;

            PromptDefinition def;
            string? versionId = null;
            if (!String.IsNullOrWhiteSpace(req.Content))
            {
                Dictionary<string, string> errors = PromptService.Validate(req.Key, req.Content);
                if (errors.ContainsKey("content") || errors.ContainsKey("key"))
                {
                    await SendErrorAsync(ctx, 400, "ValidationFailed", "The draft prompt is not valid.", errors).ConfigureAwait(false);
                    return;
                }

                def = PromptParser.Parse(req.Content);
            }
            else
            {
                ResolvedPrompt resolved;
                try
                {
                    resolved = await Server.Prompts.ResolveAsync(tenantId, req.Key, req.TopicId, ctx.Token).ConfigureAwait(false);
                }
                catch (KeyNotFoundException ex)
                {
                    await SendErrorAsync(ctx, 404, "NotFound", ex.Message).ConfigureAwait(false);
                    return;
                }

                def = resolved.Definition;
                versionId = resolved.Version.Id;
            }

            Dictionary<string, object?> values = req.Variables.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
            values.TryAdd("date.today", DateTime.UtcNow.ToString("yyyy-MM-dd"));
            RenderedPrompt rendered = PromptService.Render(def, values);
            PromptLabResponse response = new PromptLabResponse { RenderedSystem = rendered.System, RenderedUser = rendered.User, UnknownVariables = rendered.UnknownVariables };
            try
            {
                ModelCallResult result = await Server.Models.ChatAsync(new ModelCallRequest
                {
                    TenantId = tenantId,
                    UserId = Context(ctx).UserId,
                    TopicId = req.TopicId,
                    Task = def.Role,
                    SystemPrompt = rendered.System,
                    UserPrompt = rendered.User,
                    PromptKey = def.Key,
                    PromptTemplateVersionId = versionId,
                    ModelEndpointId = req.ModelEndpointId,
                    Model = req.Model,
                    IsDryRun = true
                }, ctx.Token).ConfigureAwait(false);
                response.Success = true;
                response.Text = result.Text;
                response.Model = result.Model;
                response.InputTokens = result.InputTokens;
                response.OutputTokens = result.OutputTokens;
                response.DurationMs = result.DurationMs;
                response.LlmCallId = result.LlmCallId;
                response.CachedInputTokens = result.CachedInputTokens;
                response.ReasoningTokens = result.ReasoningTokens;
                response.TimeToFirstTokenMs = result.TimeToFirstTokenMs;
                response.EstimatedCostUsd = result.EstimatedCostUsd;
            }
            catch (ModelCallException ex)
            {
                response.Error = ex.Message;
            }

            await SendJsonAsync(ctx, 200, response).ConfigureAwait(false);
        }
    }
}
