namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Services;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Model endpoint, role assignment, and model call log routes.
    /// </summary>
    public class ModelRoutes : RouteBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public ModelRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            string e = "/v1.0/api/model-endpoints";
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, e, ListAsync, null, openApiMetadata: Paged(Doc("List model endpoints", "Models").WithParameter(QueryParam("active", typeof(bool))).WithParameter(QueryParam("supportsChat", typeof(bool))).WithParameter(QueryParam("supportsEmbeddings", typeof(bool))).WithParameter(QueryParam("provider", typeof(ModelProviderEnum)))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, e, CreateAsync, null, openApiMetadata: Doc("Create a model endpoint", "Models").WithRequestBody(Body<ModelEndpointUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, e + "/{id}", ReadAsync, null, openApiMetadata: Doc("Read a model endpoint", "Models"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, e + "/{id}", UpdateAsync, null, openApiMetadata: Doc("Update a model endpoint", "Models", "Null or absent properties are unchanged. To set an optional value back to null, name it in clearFields (endpoint, credentialId, authMethod, model, embeddingModel, temperature, reasoningEffort, contextLength, inputCostPerMTok, outputCostPerMTok, cachedInputCostPerMTok, cacheWriteCostPerMTok); naming a property there and giving it a value in the same request is refused, as is any other name.").WithRequestBody(Body<ModelEndpointUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, e + "/{id}", DeleteAsync, null, openApiMetadata: Doc("Delete a model endpoint", "Models"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, e + "/{id}/validate", ValidateAsync, null, openApiMetadata: Doc("Validate connectivity, tool support, and embeddings", "Models"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, e + "/{id}/models", ModelsAsync, null, openApiMetadata: Doc("List models offered by an endpoint", "Models"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, e + "/{id}/test", TestAsync, null, openApiMetadata: Doc("Send a sample prompt", "Models").WithRequestBody(Body<ModelTestRequest>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/model-assignments", ListAssignmentsAsync, null, openApiMetadata: Paged(Doc("List task role assignments", "Models").WithParameter(QueryParam("topicId", typeof(string))).WithParameter(QueryParam("task", typeof(ModelTaskEnum)))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.PUT, "/v1.0/api/model-assignments", UpsertAssignmentAsync, null, openApiMetadata: Doc("Assign a task role to an endpoint", "Models").WithRequestBody(Body<ModelAssignmentUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/api/model-assignments/{id}", DeleteAssignmentAsync, null, openApiMetadata: Doc("Remove a topic assignment override", "Models"));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/llm-calls", ListCallsAsync, null, openApiMetadata: Paged(Doc("List model calls", "Models").WithParameter(QueryParam("runId", typeof(string))).WithParameter(QueryParam("runStageId", typeof(string))).WithParameter(QueryParam("refinementSessionId", typeof(string))).WithParameter(QueryParam("briefingId", typeof(string))).WithParameter(QueryParam("modelEndpointId", typeof(string))).WithParameter(QueryParam("model", typeof(string))).WithParameter(QueryParam("promptKey", typeof(string))).WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("topicId", typeof(string))).WithParameter(QueryParam("success", typeof(bool))).WithParameter(QueryParam("isDryRun", typeof(bool))).WithParameter(QueryParam("task", typeof(ModelTaskEnum)))));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/llm-calls/{id}", ReadCallAsync, null, openApiMetadata: Doc("Read a model call with payloads", "Models"));
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.ModelRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            ModelEndpointFilter filter = new ModelEndpointFilter { TenantId = tenantId, Provider = QueryEnum<ModelProviderEnum>(ctx, "provider"), Active = QueryBool(ctx, "active"), SupportsChat = QueryBool(ctx, "supportsChat"), SupportsEmbeddings = QueryBool(ctx, "supportsEmbeddings") };
            ApplyPaging(ctx, filter);
            await SendJsonAsync(ctx, 200, await Server.Database.ModelEndpoints.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task CreateAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.ModelWrite).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            MyMorningNews.Core.Security.RequestContext who = Context(ctx);
            if (!who.IsAdmin && !String.IsNullOrEmpty(who.UserId)
                && !MyMorningNews.Core.Entitlements.EntitlementService.Allowed(await Server.Entitlements.GetEffectiveAsync(tenantId, who.UserId, ctx.Token).ConfigureAwait(false), "endpoints.custom"))
                throw new MyMorningNews.Core.Exceptions.CapReachedException("endpoints.custom", "false", null, "Your plan uses the platform models; adding your own model endpoints is not included.");
            ModelEndpointUpsertRequest? req = await ReadBodyAsync<ModelEndpointUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            if (String.IsNullOrWhiteSpace(req.Name) || !req.Provider.HasValue)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "Name and provider are required.").ConfigureAwait(false);
                return;
            }

            ModelEndpoint endpoint = new ModelEndpoint { TenantId = tenantId, Name = req.Name.Trim(), Provider = req.Provider.Value };
            string? error = await ApplyAsync(endpoint, req, tenantId, ctx).ConfigureAwait(false);
            if (error != null)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", error).ConfigureAwait(false);
                return;
            }

            endpoint = await Server.Database.ModelEndpoints.CreateAsync(endpoint, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, endpoint).ConfigureAwait(false);
        }

        private async Task<ModelEndpoint?> LoadAsync(HttpContextBase ctx)
        {
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return null;
            ModelEndpoint? e = await Server.Database.ModelEndpoints.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (e == null) await SendNotFoundAsync(ctx, "Model endpoint").ConfigureAwait(false);
            return e;
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.ModelRead, Param(ctx, "id")).ConfigureAwait(false)) return;
            ModelEndpoint? e = await LoadAsync(ctx).ConfigureAwait(false);
            if (e != null) await SendJsonAsync(ctx, 200, e).ConfigureAwait(false);
        }

        private async Task UpdateAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.ModelWrite, Param(ctx, "id")).ConfigureAwait(false)) return;
            ModelEndpointUpsertRequest? req = await ReadBodyAsync<ModelEndpointUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            ModelEndpoint? e = await LoadAsync(ctx).ConfigureAwait(false);
            if (e == null) return;
            if (!String.IsNullOrWhiteSpace(req.Name)) e.Name = req.Name.Trim();
            ModelProviderEnum provider = e.Provider;
            string? endpoint = e.Endpoint;
            string? credential = e.CredentialId;
            string? model = e.Model;
            string? embeddingModel = e.EmbeddingModel;
            ModelAuthMethodEnum? authMethod = e.AuthMethod;
            if (req.Provider.HasValue) e.Provider = req.Provider.Value;
            string? error = await ApplyAsync(e, req, e.TenantId, ctx).ConfigureAwait(false);
            if (error != null)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", error).ConfigureAwait(false);
                return;
            }

            if (provider != e.Provider
                || !String.Equals(endpoint, e.Endpoint, StringComparison.Ordinal)
                || !String.Equals(credential, e.CredentialId, StringComparison.Ordinal)
                || !String.Equals(model, e.Model, StringComparison.Ordinal)
                || !String.Equals(embeddingModel, e.EmbeddingModel, StringComparison.Ordinal)
                || authMethod != e.AuthMethod)
            {
                e.LastValidationSucceeded = null;
            }

            e = await Server.Database.ModelEndpoints.UpdateAsync(e, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, e).ConfigureAwait(false);
        }

        private async Task<string?> ApplyAsync(ModelEndpoint e, ModelEndpointUpsertRequest req, string tenantId, HttpContextBase ctx)
        {
            string? clearError = MyMorningNews.Core.Helpers.ClearFieldList.Validate(req);
            if (clearError != null) return clearError;
            List<string>? clear = req.ClearFields;
            bool platform = String.Equals(e.CredentialId, MyMorningNews.Core.Services.VaultService.PlatformAnthropicCredentialId, StringComparison.Ordinal);
            bool admin = Context(ctx).IsAdmin;
            if (String.Equals(req.CredentialId, MyMorningNews.Core.Services.VaultService.PlatformAnthropicCredentialId, StringComparison.Ordinal) && !platform && !admin)
                return "The platform credential is assigned by the operator.";
            if (platform && !admin)
            {
                bool moves = (req.Endpoint != null && !String.Equals(req.Endpoint.Trim(), e.Endpoint ?? String.Empty, StringComparison.Ordinal))
                    || (req.Provider.HasValue && req.Provider.Value != e.Provider);
                if (moves) return "This endpoint uses the platform credential, so its provider and URL cannot be changed. Add your own endpoint instead.";
            }

            bool cli = AgentCliService.IsAgentCli(e.Provider);
            if (cli && !Server.Models.AgentClis.Allowed()) return "Agent CLIs are turned off on this server (Models.AllowAgentClis). Use a provider API key instead.";
            if (cli && !Server.Models.AgentClis.AllowedFor(Context(ctx).IsAdmin)) return "On this server, only administrators can connect agent CLIs. Use a provider API key instead.";
            if (Clears(clear, "authMethod")) e.AuthMethod = null;
            if (req.AuthMethod.HasValue)
            {
                if (!ModelCredentials.Accepts(e.Provider, req.AuthMethod.Value)) return e.Provider + " cannot sign in with " + AgentCliService.Describe(req.AuthMethod.Value, e.Provider) + ".";
                e.AuthMethod = req.AuthMethod.Value;
            }
            else if (e.AuthMethod.HasValue && !ModelCredentials.Accepts(e.Provider, e.AuthMethod.Value))
            {
                e.AuthMethod = null;
            }

            if (req.Endpoint != null) e.Endpoint = req.Endpoint.Trim().Length == 0 ? null : req.Endpoint.Trim();
            if (cli || Clears(clear, "endpoint")) e.Endpoint = null;
            if (e.Endpoint != null && !Uri.TryCreate(e.Endpoint, UriKind.Absolute, out Uri? _)) return "Endpoint must be an absolute URL.";
            if (e.Endpoint != null) await MyMorningNews.Core.Security.NetworkGuard.EnsureAllowedAsync(e.Endpoint, ctx.Token).ConfigureAwait(false);
            if (Clears(clear, "credentialId")) e.CredentialId = null;
            if (req.CredentialId != null)
            {
                if (req.CredentialId.Length == 0) e.CredentialId = null;
                else if (!String.Equals(req.CredentialId, MyMorningNews.Core.Services.VaultService.PlatformAnthropicCredentialId, StringComparison.Ordinal)
                    && await Server.Database.VaultCredentials.ReadAsync(tenantId, req.CredentialId, ctx.Token).ConfigureAwait(false) == null) return "Vault credential " + req.CredentialId + " was not found.";
                else e.CredentialId = req.CredentialId;
            }

            if (!String.IsNullOrEmpty(req.ApiKey))
            {
                ModelAuthMethodEnum method = AgentCliService.EffectiveMethod(e);
                if (!AgentCliService.NeedsSecret(method)) method = ModelAuthMethodEnum.ApiKey;
                string? bad = ModelCredentials.Check(e.Provider, method, req.ApiKey);
                if (bad != null) return bad;
                VaultCredential created = await Server.Vault.CreateAsync(tenantId, Context(ctx).UserId, new VaultCredentialUpsertRequest
                {
                    Name = e.Name + (method == ModelAuthMethodEnum.AuthFile ? " sign-in file " : method == ModelAuthMethodEnum.AccessToken ? " token " : " key ") + DateTime.UtcNow.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6),
                    Kind = ModelCredentials.KindFor(method),
                    Provider = ModelCredentials.VaultProviderFor(e.Provider),
                    Secret = req.ApiKey.Trim()
                }, ctx.Token).ConfigureAwait(false);
                e.CredentialId = created.Id;
            }

            if (req.Model != null) e.Model = req.Model.Trim().Length == 0 ? null : req.Model.Trim();
            if (req.EmbeddingModel != null) e.EmbeddingModel = req.EmbeddingModel.Trim().Length == 0 ? null : req.EmbeddingModel.Trim();
            if (req.SupportsChat.HasValue) e.SupportsChat = req.SupportsChat.Value;
            if (req.SupportsEmbeddings.HasValue) e.SupportsEmbeddings = req.SupportsEmbeddings.Value;
            if (req.Temperature.HasValue) e.Temperature = req.Temperature;
            if (e.Provider == ModelProviderEnum.Anthropic) e.Temperature = null;
            if (req.MaxTokens.HasValue) e.MaxTokens = req.MaxTokens.Value;
            if (req.ReasoningEffort.HasValue) e.ReasoningEffort = req.ReasoningEffort;
            if (req.ContextLength.HasValue) e.ContextLength = req.ContextLength;
            if (req.TimeoutMs.HasValue) e.TimeoutMs = req.TimeoutMs.Value;
            if (req.InputCostPerMTok.HasValue) e.InputCostPerMTok = req.InputCostPerMTok;
            if (req.OutputCostPerMTok.HasValue) e.OutputCostPerMTok = req.OutputCostPerMTok;
            if (req.CachedInputCostPerMTok.HasValue) e.CachedInputCostPerMTok = req.CachedInputCostPerMTok;
            if (req.CacheWriteCostPerMTok.HasValue) e.CacheWriteCostPerMTok = req.CacheWriteCostPerMTok;
            if (req.Active.HasValue) e.Active = req.Active.Value;
            if (Clears(clear, "model")) e.Model = null;
            if (Clears(clear, "embeddingModel")) e.EmbeddingModel = null;
            if (Clears(clear, "temperature")) e.Temperature = null;
            if (Clears(clear, "reasoningEffort")) e.ReasoningEffort = null;
            if (Clears(clear, "contextLength")) e.ContextLength = null;
            if (Clears(clear, "inputCostPerMTok")) e.InputCostPerMTok = null;
            if (Clears(clear, "outputCostPerMTok")) e.OutputCostPerMTok = null;
            if (Clears(clear, "cachedInputCostPerMTok")) e.CachedInputCostPerMTok = null;
            if (Clears(clear, "cacheWriteCostPerMTok")) e.CacheWriteCostPerMTok = null;
            if (cli)
            {
                e.SupportsTools = false;
                e.SupportsEmbeddings = false;
                e.Temperature = null;
            }

            if (e.Provider == ModelProviderEnum.OpenAiCompatible && String.IsNullOrEmpty(e.Endpoint)) return "OpenAI-compatible endpoints need a base URL.";
            if (e.SupportsChat && !cli && String.IsNullOrEmpty(e.Model)) return "A chat model is required when the endpoint serves chat.";
            if (e.SupportsEmbeddings && String.IsNullOrEmpty(e.EmbeddingModel)) return "An embedding model is required when the endpoint serves embeddings.";
            return null;
        }

        private static bool Clears(List<string>? clear, string field)
        {
            return MyMorningNews.Core.Helpers.ClearFieldList.Contains(clear, field);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.ModelDelete, Param(ctx, "id")).ConfigureAwait(false)) return;
            ModelEndpoint? e = await LoadAsync(ctx).ConfigureAwait(false);
            if (e == null) return;
            await Server.Database.ModelEndpoints.DeleteAsync(e.TenantId, e.Id, ctx.Token).ConfigureAwait(false);
            Server.Models.AgentClis.DeleteHome(e.TenantId, e.Id);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task ValidateAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.ModelExecute, Param(ctx, "id")).ConfigureAwait(false)) return;
            ModelEndpoint? e = await LoadAsync(ctx).ConfigureAwait(false);
            if (e == null) return;
            await SendJsonAsync(ctx, 200, await Server.Models.ValidateAsync(e, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ModelsAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.ModelExecute, Param(ctx, "id")).ConfigureAwait(false)) return;
            ModelEndpoint? e = await LoadAsync(ctx).ConfigureAwait(false);
            if (e == null) return;
            try
            {
                await SendJsonAsync(ctx, 200, await Server.Models.ListModelsAsync(e, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ModelCallException || ex is VaultSecretUnavailableException || ex is ArgumentException)
            {
                await SendErrorAsync(ctx, 502, "UpstreamError", MyMorningNews.Core.Helpers.ErrorText.Plain(ex)).ConfigureAwait(false);
            }
        }

        private async Task TestAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.ModelExecute, Param(ctx, "id")).ConfigureAwait(false)) return;
            ModelTestRequest req = Serializer(ctx) ?? new ModelTestRequest();
            ModelEndpoint? e = await LoadAsync(ctx).ConfigureAwait(false);
            if (e == null) return;
            ModelTestResponse result = new ModelTestResponse();
            try
            {
                ModelCallResult r = await Server.Models.ChatAsync(new ModelCallRequest
                {
                    TenantId = e.TenantId,
                    UserId = Context(ctx).UserId,
                    Task = ModelTaskEnum.QueryPlanning,
                    ModelEndpointId = e.Id,
                    Model = req.Model,
                    UserPrompt = req.Prompt,
                    IsDryRun = true
                }, ctx.Token).ConfigureAwait(false);
                result.Success = true;
                result.Text = r.Text;
                result.Model = r.Model;
                result.InputTokens = r.InputTokens;
                result.OutputTokens = r.OutputTokens;
                result.DurationMs = r.DurationMs;
                result.SupportsTools = e.SupportsTools;
            }
            catch (ModelCallException ex)
            {
                result.Error = ex.Message;
            }

            await SendJsonAsync(ctx, 200, result).ConfigureAwait(false);
        }

        private static ModelTestRequest? Serializer(HttpContextBase ctx)
        {
            string body = ctx.Request.DataAsString ?? String.Empty;
            if (String.IsNullOrWhiteSpace(body)) return null;
            try
            {
                return MyMorningNews.Core.Serialization.Serializer.Deserialize<ModelTestRequest>(body);
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }

        private async Task ListAssignmentsAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.ModelRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            ModelAssignmentFilter filter = new ModelAssignmentFilter { TenantId = tenantId, TopicId = Query(ctx, "topicId"), Task = QueryEnum<ModelTaskEnum>(ctx, "task"), PageSize = 1000 };
            ApplyPaging(ctx, filter);
            await SendJsonAsync(ctx, 200, await Server.Database.ModelAssignments.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task UpsertAssignmentAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.ModelWrite).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            ModelAssignmentUpsertRequest? req = await ReadBodyAsync<ModelAssignmentUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            ModelEndpoint? endpoint = await Server.Database.ModelEndpoints.ReadAsync(tenantId, req.ModelEndpointId, ctx.Token).ConfigureAwait(false);
            if (endpoint == null)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "Model endpoint " + req.ModelEndpointId + " was not found.").ConfigureAwait(false);
                return;
            }

            bool embedding = req.Task == ModelTaskEnum.Embedding;
            if (embedding ? !endpoint.SupportsEmbeddings : !endpoint.SupportsChat)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "Endpoint " + endpoint.Name + " does not serve " + (embedding ? "embeddings" : "chat") + ".").ConfigureAwait(false);
                return;
            }

            EnumerationResult<ModelAssignment> existing = await Server.Database.ModelAssignments.EnumerateAsync(new ModelAssignmentFilter { TenantId = tenantId, Task = req.Task, PageSize = 100 }, ctx.Token).ConfigureAwait(false);
            ModelAssignment? a = existing.Items.Find(x => String.Equals(x.TopicId, req.TopicId, StringComparison.Ordinal));
            bool created = a == null;
            a ??= new ModelAssignment { TenantId = tenantId, Task = req.Task, TopicId = req.TopicId, ModelEndpointId = endpoint.Id };
            a.ModelEndpointId = endpoint.Id;
            a.ModelOverride = String.IsNullOrWhiteSpace(req.ModelOverride) ? null : req.ModelOverride.Trim();
            a.ReasoningEffort = req.ReasoningEffort;
            a = created ? await Server.Database.ModelAssignments.CreateAsync(a, ctx.Token).ConfigureAwait(false) : await Server.Database.ModelAssignments.UpdateAsync(a, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, created ? 201 : 200, a).ConfigureAwait(false);
        }

        private async Task DeleteAssignmentAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.ModelWrite).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            ModelAssignment? a = await Server.Database.ModelAssignments.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (a == null)
            {
                await SendNotFoundAsync(ctx, "Assignment").ConfigureAwait(false);
                return;
            }

            if (a.TopicId == null)
            {
                await SendErrorAsync(ctx, 409, "Conflict", "Tenant default assignments cannot be deleted; reassign them instead.").ConfigureAwait(false);
                return;
            }

            await Server.Database.ModelAssignments.DeleteAsync(tenantId, a.Id, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task ListCallsAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.LlmCallRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            LlmCallFilter filter = new LlmCallFilter
            {
                TenantId = tenantId,
                Task = QueryEnum<ModelTaskEnum>(ctx, "task"),
                RunId = Query(ctx, "runId"),
                RunStageId = Query(ctx, "runStageId"),
                RefinementSessionId = Query(ctx, "refinementSessionId"),
                BriefingId = Query(ctx, "briefingId"),
                ModelEndpointId = Query(ctx, "modelEndpointId"),
                Model = Query(ctx, "model"),
                PromptKey = Query(ctx, "promptKey"),
                UserId = Query(ctx, "userId"),
                TopicId = Query(ctx, "topicId"),
                Success = QueryBool(ctx, "success"),
                IsDryRun = QueryBool(ctx, "isDryRun")
            };
            ApplyPaging(ctx, filter);
            EnumerationResult<LlmCall> page = await Server.Database.LlmCalls.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false);
            foreach (LlmCall c in page.Items)
            {
                c.PromptText = null;
                c.ResponseText = null;
            }

            await SendJsonAsync(ctx, 200, page).ConfigureAwait(false);
        }

        private async Task ReadCallAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.LlmCallRead, Param(ctx, "id")).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            LlmCall? c = await Server.Database.LlmCalls.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (c == null)
            {
                await SendNotFoundAsync(ctx, "Model call").ConfigureAwait(false);
                return;
            }

            await SendJsonAsync(ctx, 200, c).ConfigureAwait(false);
        }
    }
}
