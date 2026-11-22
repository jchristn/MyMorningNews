namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Helpers;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Models.Results;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Serialization;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Core.Telemetry;
    using PolyPrompt.Clients;
    using PolyPrompt.Models;
    using SyslogLogging;

    /// <summary>
    /// Every model call goes through the gateway: it resolves the endpoint and model assigned to a task role, applies
    /// provider rules (Anthropic: no sampling parameters, no prefill, no forced tool choice, explicit reasoning effort),
    /// retries transient failures, enforces structured output (a submit_result tool offered with tool choice auto, or a
    /// JSON fallback for models without tool support, with bounded repair turns), logs the call, and emits telemetry.
    /// Thread safety: safe for concurrent use; concurrency is bounded by Pipeline.MaxConcurrentModelCalls.
    /// </summary>
    public class ModelGateway : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Optional check before every chat call (the plan's monthly cost cap). Throws
        /// <see cref="Exceptions.CapReachedException"/> to refuse. Set by the server.
        /// </summary>
        public Func<ModelCallRequest, CancellationToken, Task>? PreCallCheck { get; set; } = null;

        /// <summary>
        /// Runner for agent CLI endpoints (Claude Code, Codex, Gemini CLI, Mux).
        /// </summary>
        public AgentCliService AgentClis
        {
            get { return _Clis; }
        }

        /// <summary>
        /// Name of the tool offered for structured output.
        /// </summary>
        public const string SubmitToolName = "submit_result";

        #endregion

        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly VaultService _Vault;
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly ModelClientFactory _Factory;
        private readonly AgentCliService _Clis;
        private readonly SemaphoreSlim _Concurrency;
        private readonly string _Header = "[ModelGateway] ";
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="vault">Vault.</param>
        /// <param name="settings">Settings (read live).</param>
        /// <param name="logging">Logging.</param>
        /// <param name="factory">Client factory; created when null.</param>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        public ModelGateway(DatabaseDriverBase database, VaultService vault, ServerSettings settings, LoggingModule logging, ModelClientFactory? factory = null)
        {
            ArgumentNullException.ThrowIfNull(database);
            ArgumentNullException.ThrowIfNull(vault);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(logging);
            _Database = database;
            _Vault = vault;
            _Settings = settings;
            _Logging = logging;
            _Factory = factory ?? new ModelClientFactory(null, logging);
            _Clis = new AgentCliService(() => _Settings.Models, logging);
            _Concurrency = new SemaphoreSlim(settings.Pipeline.MaxConcurrentModelCalls, settings.Pipeline.MaxConcurrentModelCalls);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Resolve the endpoint, model, and effort for a task: topic assignment, then tenant assignment, then the first
        /// active endpoint that serves the task.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="task">Task role.</param>
        /// <param name="topicId">Topic scope, when any.</param>
        /// <param name="endpointOverride">Endpoint override.</param>
        /// <param name="modelOverride">Model override.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Resolved model.</returns>
        /// <exception cref="ModelCallException">No endpoint can serve the task.</exception>
        public async Task<ResolvedModel> ResolveAsync(string tenantId, ModelTaskEnum task, string? topicId, string? endpointOverride, string? modelOverride, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            bool embedding = task == ModelTaskEnum.Embedding;
            ModelEndpoint? endpoint = null;
            ModelAssignment? assignment = null;

            if (!String.IsNullOrEmpty(endpointOverride))
            {
                endpoint = await _Database.ModelEndpoints.ReadAsync(tenantId, endpointOverride, token).ConfigureAwait(false);
                if (endpoint == null) throw new ModelCallException("Model endpoint " + endpointOverride + " was not found.");
            }
            else
            {
                EnumerationResult<ModelAssignment> assignments = await _Database.ModelAssignments.EnumerateAsync(
                    new ModelAssignmentFilter { TenantId = tenantId, Task = task, PageSize = 100 }, token).ConfigureAwait(false);
                assignment = assignments.Items.FirstOrDefault(a => topicId != null && a.TopicId == topicId && a.Active)
                    ?? assignments.Items.FirstOrDefault(a => a.TopicId == null && a.Active);
                if (assignment != null) endpoint = await _Database.ModelEndpoints.ReadAsync(tenantId, assignment.ModelEndpointId, token).ConfigureAwait(false);
                if (endpoint == null || !endpoint.Active)
                {
                    EnumerationResult<ModelEndpoint> all = await _Database.ModelEndpoints.EnumerateAsync(
                        new ModelEndpointFilter { TenantId = tenantId, Active = true, PageSize = 100, SortBy = "createdUtc", SortDirection = SortDirectionEnum.Ascending }, token).ConfigureAwait(false);
                    endpoint = all.Items.FirstOrDefault(e => embedding ? e.SupportsEmbeddings : e.SupportsChat);
                }
            }

            if (endpoint == null) throw new ModelCallException("No model endpoint is configured for " + task + ". Add one on the Models page.");

            // A workspace on the server's shared model, when the operator set no shared key in the environment, uses the
            // model the operator connected in the default workspace (an API key or an agent CLI sign-in).
            if (!embedding
                && String.IsNullOrEmpty(endpointOverride)
                && !String.Equals(tenantId, Constants.DefaultTenantId, StringComparison.Ordinal)
                && String.Equals(endpoint.CredentialId, VaultService.PlatformAnthropicCredentialId, StringComparison.Ordinal)
                && !VaultService.PlatformKeyConfigured())
            {
                ResolvedModel? shared = await SharedModelAsync(task, token).ConfigureAwait(false);
                if (shared != null) return shared;
            }

            string? model = modelOverride ?? assignment?.ModelOverride ?? (embedding ? endpoint.EmbeddingModel : endpoint.Model);
            if (String.IsNullOrWhiteSpace(model) && !embedding && AgentCliService.IsAgentCli(endpoint.Provider)) model = AgentCliService.DefaultModel;
            if (String.IsNullOrWhiteSpace(model)) throw new ModelCallException("Endpoint " + endpoint.Name + " has no " + (embedding ? "embedding " : String.Empty) + "model configured.");
            return new ResolvedModel(endpoint, model, assignment?.ReasoningEffort ?? endpoint.ReasoningEffort);
        }

        /// <summary>
        /// The operator's model for a task: what the default workspace resolves to, when that endpoint has proven it works
        /// and is not itself waiting on the shared key. Workspaces on the shared model use it when no shared key is set
        /// in the environment.
        /// </summary>
        /// <param name="task">Task role.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The operator's resolved model, or null when the default workspace has none usable.</returns>
        public async Task<ResolvedModel?> SharedModelAsync(ModelTaskEnum task, CancellationToken token = default)
        {
            try
            {
                ResolvedModel shared = await ResolveAsync(Constants.DefaultTenantId, task, null, null, null, token).ConfigureAwait(false);
                if (String.Equals(shared.Endpoint.CredentialId, VaultService.PlatformAnthropicCredentialId, StringComparison.Ordinal)) return null;
                if (shared.Endpoint.LastValidationSucceeded != true) return null;
                return shared;
            }
            catch (ModelCallException)
            {
                return null;
            }
        }

        /// <summary>
        /// Text completion.
        /// </summary>
        /// <param name="request">Request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        /// <exception cref="ModelCallException">The call failed.</exception>
        public async Task<ModelCallResult> ChatAsync(ModelCallRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (PreCallCheck != null) await PreCallCheck(request, token).ConfigureAwait(false);
            ResolvedModel resolved = await ResolveAsync(request.TenantId, request.Task, request.TopicId, request.ModelEndpointId, request.Model, token).ConfigureAwait(false);
            List<ChatMessage> messages = BuildMessages(request.SystemPrompt, request.UserPrompt);
            CallState state = new CallState(request, resolved);
            try
            {
                ModelExchange response = await SendAsync(resolved, messages, null, request, state, token).ConfigureAwait(false);
                state.Output = response.Text ?? String.Empty;
                await LogAsync(state, true, null, response.StatusCode, response.FinishReason, token).ConfigureAwait(false);
                return state.ToResult<ModelCallResult>(new ModelCallResult { Text = state.Output });
            }
            catch (ModelCallException ex)
            {
                await LogAsync(state, false, ex.Message, ex.StatusCode, null, token).ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>
        /// Structured completion. The model returns a value of type T, validated with <see cref="IStructuredResult.Validate"/>.
        /// </summary>
        /// <typeparam name="T">Result type.</typeparam>
        /// <param name="request">Request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        /// <exception cref="StructuredOutputException">No valid output within the repair budget.</exception>
        /// <exception cref="ModelCallException">The call failed.</exception>
        public async Task<StructuredCallResult<T>> ChatStructuredAsync<T>(ModelCallRequest request, CancellationToken token = default) where T : class, IStructuredResult
        {
            ArgumentNullException.ThrowIfNull(request);
            if (PreCallCheck != null) await PreCallCheck(request, token).ConfigureAwait(false);
            ResolvedModel resolved = await ResolveAsync(request.TenantId, request.Task, request.TopicId, request.ModelEndpointId, request.Model, token).ConfigureAwait(false);
            Dictionary<string, object> schema = JsonSchemaBuilder.Build(typeof(T));
            bool useTools = resolved.Endpoint.SupportsTools != false;
            string instruction = useTools
                ? "Return your answer by calling the " + SubmitToolName + " tool exactly once, with arguments that match its schema. Do not answer in plain text."
                : "Respond with only one JSON object that matches this JSON Schema, with no commentary and no code fences:\n" + Serializer.Serialize(schema);
            string system = String.IsNullOrWhiteSpace(request.SystemPrompt) ? instruction : request.SystemPrompt.TrimEnd() + "\n\n" + instruction;
            List<ChatMessage> messages = BuildMessages(system, request.UserPrompt);
            List<ToolDefinition>? tools = useTools
                ? new List<ToolDefinition> { ToolDefinition.Function(SubmitToolName, "Submit the final structured result.", schema) }
                : null;

            CallState state = new CallState(request, resolved);
            int maxRepairs = _Settings.Pipeline.StructuredOutputRepairs;
            string? lastRaw = null;
            try
            {
                for (int attempt = 0; attempt <= maxRepairs; attempt++)
                {
                    ModelExchange response = await SendAsync(resolved, messages, tools, request, state, token).ConfigureAwait(false);
                    ToolCall? call = response.ToolCalls?.FirstOrDefault(c => String.Equals(c.Name, SubmitToolName, StringComparison.Ordinal));
                    string raw = call != null ? call.ArgumentsJson : ExtractJson(response.Text);
                    lastRaw = raw;
                    List<string> errors = new List<string>();
                    T? value = null;
                    try
                    {
                        value = String.IsNullOrWhiteSpace(raw) ? null : Serializer.Deserialize<T>(raw);
                        if (value == null) errors.Add(call == null && useTools ? "You did not call " + SubmitToolName + "." : "The output was empty.");
                        else errors.AddRange(value.Validate());
                    }
                    catch (JsonException ex)
                    {
                        errors.Add("The output is not valid JSON for the schema: " + ex.Message);
                    }

                    if (errors.Count == 0 && value != null)
                    {
                        state.Output = raw;
                        await LogAsync(state, true, null, response.StatusCode, response.FinishReason, token).ConfigureAwait(false);
                        StructuredCallResult<T> ok = state.ToResult(new StructuredCallResult<T> { Value = value });
                        ok.Text = raw;
                        return ok;
                    }

                    if (attempt == maxRepairs) break;
                    state.Repairs++;
                    MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.LlmStructuredRepairs, "task", request.Task.ToString(), "provider", resolved.Endpoint.Provider.ToString());
                    string feedback = "Your result failed validation: " + String.Join(" ", errors) + " Fix these problems and submit again.";
                    if (call != null)
                    {
                        messages.Add(response.AssistantMessage);
                        messages.Add(ChatMessage.ToolResult(call.Id, call.Name, feedback));
                    }
                    else
                    {
                        messages.Add(ChatMessage.Assistant(String.IsNullOrEmpty(response.Text) ? "(no answer)" : response.Text));
                        messages.Add(ChatMessage.User(feedback + (useTools ? " Call " + SubmitToolName + " with the corrected values." : " Reply with only the corrected JSON object.")));
                    }
                }

                state.Output = lastRaw ?? String.Empty;
                StructuredOutputException failure = new StructuredOutputException("The model's reply could not be used as a " + ErrorText.Describe(typeof(T)) + " after " + (maxRepairs + 1) + " attempts. Try again, or choose a different model under Models.", lastRaw);
                await LogAsync(state, false, failure.Message, null, null, token).ConfigureAwait(false);
                throw failure;
            }
            catch (ModelCallException ex) when (!(ex is StructuredOutputException))
            {
                await LogAsync(state, false, ex.Message, ex.StatusCode, null, token).ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>
        /// Agentic structured completion: the model may call the given tools (up to <paramref name="maxToolCalls"/>
        /// executions) before returning a value of type T through the submit tool. Tool results go back to the model,
        /// a tool error is returned to the model as text, and once the budget is spent only the submit tool is offered.
        /// The whole loop is one llmcalls record with every tool name. Needs a model whose endpoint supports tools.
        /// </summary>
        /// <typeparam name="T">Result type.</typeparam>
        /// <param name="request">Request.</param>
        /// <param name="tools">Tools the model may call.</param>
        /// <param name="maxToolCalls">Most tool executions. Minimum 0, maximum 200.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="InvalidOperationException">The endpoint does not support tools.</exception>
        /// <exception cref="StructuredOutputException">No valid output within the turn budget.</exception>
        /// <exception cref="ModelCallException">The call failed.</exception>
        public async Task<StructuredCallResult<T>> ChatAgenticAsync<T>(ModelCallRequest request, IReadOnlyList<AgentTool> tools, int maxToolCalls, CancellationToken token = default) where T : class, IStructuredResult
        {
            ArgumentNullException.ThrowIfNull(request);
            if (PreCallCheck != null) await PreCallCheck(request, token).ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(tools);
            int budget = Math.Clamp(maxToolCalls, 0, 200);
            ResolvedModel resolved = await ResolveAsync(request.TenantId, request.Task, request.TopicId, request.ModelEndpointId, request.Model, token).ConfigureAwait(false);
            if (resolved.Endpoint.SupportsTools == false) throw new InvalidOperationException("The " + request.Task + " model does not support tool calls.");
            Dictionary<string, object> schema = JsonSchemaBuilder.Build(typeof(T));
            ToolDefinition submit = ToolDefinition.Function(SubmitToolName, "Submit the final structured result. Call it exactly once, when you are done.", schema);
            List<ToolDefinition> all = tools.Select(t => ToolDefinition.Function(t.Name, t.Description, t.Parameters)).Append(submit).ToList();
            Dictionary<string, AgentTool> byName = tools.ToDictionary(t => t.Name, StringComparer.Ordinal);
            string instruction = "Use the tools as needed (at most " + budget + " tool calls in total), then return your answer by calling " + SubmitToolName
                + " exactly once with arguments that match its schema. Do not answer in plain text.";
            string system = String.IsNullOrWhiteSpace(request.SystemPrompt) ? instruction : request.SystemPrompt.TrimEnd() + "\n\n" + instruction;
            List<ChatMessage> messages = BuildMessages(system, request.UserPrompt);
            CallState state = new CallState(request, resolved);
            int used = 0;
            int repairs = 0;
            int maxRepairs = _Settings.Pipeline.StructuredOutputRepairs;
            string? lastRaw = null;
            try
            {
                for (int turn = 0; turn < budget + maxRepairs + 4; turn++)
                {
                    List<ToolDefinition> offered = used >= budget ? new List<ToolDefinition> { submit } : all;
                    ModelExchange response = await SendAsync(resolved, messages, offered, request, state, token).ConfigureAwait(false);
                    List<ToolCall> calls = response.ToolCalls ?? new List<ToolCall>();
                    if (calls.Count == 0)
                    {
                        if (repairs >= maxRepairs) break;
                        repairs++;
                        messages.Add(ChatMessage.Assistant(String.IsNullOrEmpty(response.Text) ? "(no answer)" : response.Text));
                        messages.Add(ChatMessage.User("Call " + SubmitToolName + " now with your result."));
                        continue;
                    }

                    messages.Add(response.AssistantMessage);
                    foreach (ToolCall call in calls)
                    {
                        if (String.Equals(call.Name, SubmitToolName, StringComparison.Ordinal))
                        {
                            lastRaw = call.ArgumentsJson;
                            List<string> errors = new List<string>();
                            T? value = null;
                            try
                            {
                                value = String.IsNullOrWhiteSpace(call.ArgumentsJson) ? null : Serializer.Deserialize<T>(call.ArgumentsJson);
                                if (value == null) errors.Add("The result was empty.");
                                else errors.AddRange(value.Validate());
                            }
                            catch (JsonException ex)
                            {
                                errors.Add("The result is not valid JSON for the schema: " + ex.Message);
                            }

                            if (errors.Count == 0 && value != null)
                            {
                                state.Output = call.ArgumentsJson ?? String.Empty;
                                await LogAsync(state, true, null, response.StatusCode, response.FinishReason, token).ConfigureAwait(false);
                                StructuredCallResult<T> ok = state.ToResult(new StructuredCallResult<T> { Value = value });
                                ok.Text = call.ArgumentsJson ?? String.Empty;
                                return ok;
                            }

                            repairs++;
                            messages.Add(ChatMessage.ToolResult(call.Id, call.Name, "Your result failed validation: " + String.Join(" ", errors) + " Fix these problems and submit again."));
                            continue;
                        }

                        string result;
                        if (!byName.TryGetValue(call.Name, out AgentTool? tool)) result = "Error: there is no tool named " + call.Name + ".";
                        else if (used >= budget) result = "Error: the tool budget is used up. Call " + SubmitToolName + " now.";
                        else
                        {
                            used++;
                            try
                            {
                                result = await tool.Execute(call.ArgumentsJson ?? "{}", token).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException) when (token.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                result = "Error: " + ex.Message;
                            }
                        }

                        if (result.Length > 12000) result = result.Substring(0, 12000) + "\n[truncated]";
                        messages.Add(ChatMessage.ToolResult(call.Id, call.Name, result));
                    }

                    if (repairs > maxRepairs) break;
                }

                state.Output = lastRaw ?? String.Empty;
                StructuredOutputException failure = new StructuredOutputException("The model did not submit a usable " + ErrorText.Describe(typeof(T)) + " after " + used + " tool calls. Try again, or choose a different model under Models.", lastRaw);
                await LogAsync(state, false, failure.Message, null, null, token).ConfigureAwait(false);
                throw failure;
            }
            catch (ModelCallException ex) when (!(ex is StructuredOutputException))
            {
                await LogAsync(state, false, ex.Message, ex.StatusCode, null, token).ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>
        /// Embed texts with the tenant's Embedding assignment.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="topicId">Topic scope, when any.</param>
        /// <param name="texts">Texts.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Vectors with the model and dimensions used.</returns>
        /// <exception cref="ModelCallException">The call failed.</exception>
        public async Task<EmbeddingBatch> EmbedAsync(string tenantId, string? topicId, List<string> texts, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(texts);
            ResolvedModel resolved = await ResolveAsync(tenantId, ModelTaskEnum.Embedding, topicId, null, null, token).ConfigureAwait(false);
            if (texts.Count == 0) return new EmbeddingBatch { ModelEndpointId = resolved.Endpoint.Id, Model = resolved.Model };
            string? key = await _Vault.ResolveAsync(tenantId, resolved.Endpoint.CredentialId, token).ConfigureAwait(false);
            Stopwatch sw = Stopwatch.StartNew();
            await _Concurrency.WaitAsync(token).ConfigureAwait(false);
            try
            {
                using (ProviderClientBase client = _Factory.Create(resolved.Endpoint, key))
                {
                    EmbeddingResponse response = await client.EmbedAsync(texts, new EmbeddingOptions { Model = resolved.Model }, token).ConfigureAwait(false);
                    if (!response.Success) throw new ModelCallException("Embedding failed: " + (response.Error ?? "unknown error"), response.StatusCode, IsTransient(response.StatusCode));
                    EmbeddingBatch batch = new EmbeddingBatch
                    {
                        ModelEndpointId = resolved.Endpoint.Id,
                        Model = resolved.Model,
                        Vectors = response.Embeddings.OrderBy(e => e.Index).Select(e => e.Embedding).ToList()
                    };
                    batch.Dimensions = batch.Vectors.Count > 0 ? batch.Vectors[0].Length : 0;
                    MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.LlmCalls, "task", "Embedding", "provider", resolved.Endpoint.Provider.ToString(), "outcome", "success");
                    MyMorningNewsTelemetry.Record(MyMorningNewsTelemetry.LlmCallDuration, sw.Elapsed.TotalSeconds, "task", "Embedding", "provider", resolved.Endpoint.Provider.ToString());
                    return batch;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException && !token.IsCancellationRequested)
            {
                MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.LlmCalls, "task", "Embedding", "provider", resolved.Endpoint.Provider.ToString(), "outcome", "error");
                throw new ModelCallException("Embedding endpoint " + resolved.Endpoint.Name + " is unreachable: " + ex.Message, null, true, ex);
            }
            finally
            {
                _Concurrency.Release();
            }
        }

        /// <summary>
        /// Validate an endpoint: reachability, model presence, tool support, and embedding dimensions. Updates the endpoint.
        /// </summary>
        /// <param name="endpoint">Endpoint.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Validation result.</returns>
        public async Task<ModelValidationResponse> ValidateAsync(ModelEndpoint endpoint, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            ModelValidationResponse result = new ModelValidationResponse();
            try
            {
                if (AgentCliService.IsAgentCli(endpoint.Provider))
                {
                    ModelCallRequest cliProbe = new ModelCallRequest
                    {
                        TenantId = endpoint.TenantId,
                        Task = ModelTaskEnum.QueryPlanning,
                        ModelEndpointId = endpoint.Id,
                        UserPrompt = "Reply with the word ready.",
                        SystemPrompt = "You are a connectivity probe. Do not use any tools.",
                        MaxTokens = 64,
                        IsDryRun = true
                    };
                    ModelCallResult answered = await ChatAsync(cliProbe, token).ConfigureAwait(false);
                    result.Reachable = true;
                    result.ModelFound = true;
                    result.SupportsTools = false;
                    result.Messages.Add("The CLI answered in " + Math.Round(answered.DurationMs) + " ms. Structured results use JSON replies.");
                }
                else
                {
                    string? key = await _Vault.ResolveAsync(endpoint.TenantId, endpoint.CredentialId, token).ConfigureAwait(false);
                    using (ProviderClientBase client = _Factory.Create(endpoint, key))
                    {
                        if (endpoint.SupportsChat && !String.IsNullOrWhiteSpace(endpoint.Model))
                        {
                            ModelCallRequest probe = new ModelCallRequest
                            {
                                TenantId = endpoint.TenantId,
                                Task = ModelTaskEnum.QueryPlanning,
                                ModelEndpointId = endpoint.Id,
                                UserPrompt = "Reply with the word ready.",
                                SystemPrompt = "You are a connectivity probe.",
                                MaxTokens = 64,
                                IsDryRun = true
                            };
                            ModelCallResult chat = await ChatAsync(probe, token).ConfigureAwait(false);
                            result.Reachable = true;
                            result.ModelFound = true;
                            result.Messages.Add("Chat responded in " + Math.Round(chat.DurationMs) + " ms.");

                            ModelEndpoint toolProbe = Clone(endpoint);
                            toolProbe.SupportsTools = null;
                            try
                            {
                                ModelCallRequest tools = new ModelCallRequest
                                {
                                    TenantId = endpoint.TenantId,
                                    Task = ModelTaskEnum.QueryPlanning,
                                    ModelEndpointId = endpoint.Id,
                                    UserPrompt = "Submit the answer 'ready'.",
                                    MaxTokens = 256,
                                    IsDryRun = true
                                };
                                await ChatStructuredWithEndpointAsync<TextAnswerResult>(tools, toolProbe, token).ConfigureAwait(false);
                                result.SupportsTools = true;
                                result.Messages.Add("Tool calls work.");
                            }
                            catch (ModelCallException ex)
                            {
                                result.SupportsTools = false;
                                result.Messages.Add("Tool calls are not supported or failed (" + ex.Message + "); structured output will use JSON mode.");
                            }
                        }
                        else
                        {
                            result.ModelFound = true;
                        }

                        if (endpoint.SupportsEmbeddings && !String.IsNullOrWhiteSpace(endpoint.EmbeddingModel))
                        {
                            EmbeddingResponse emb = await client.EmbedAsync("dimension probe", new EmbeddingOptions { Model = endpoint.EmbeddingModel }, token).ConfigureAwait(false);
                            if (emb.Success && emb.Embeddings.Count > 0)
                            {
                                result.Reachable = true;
                                result.EmbeddingDimensions = emb.Embeddings[0].Embedding.Length;
                                result.Messages.Add("Embeddings return " + result.EmbeddingDimensions + " dimensions.");
                            }
                            else
                            {
                                result.Messages.Add("Embedding failed: " + (emb.Error ?? "no vectors returned") + ".");
                            }
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is ModelCallException || ex is ArgumentException || ex is VaultSecretUnavailableException || ex is HttpRequestException)
            {
                result.Messages.Add(ErrorText.Plain(ex));
            }

            bool cli = AgentCliService.IsAgentCli(endpoint.Provider);
            bool chatChecked = endpoint.SupportsChat && (cli || !String.IsNullOrWhiteSpace(endpoint.Model));
            bool embeddingChecked = !cli && endpoint.SupportsEmbeddings && !String.IsNullOrWhiteSpace(endpoint.EmbeddingModel);
            bool succeeded = (chatChecked || embeddingChecked)
                && (!chatChecked || (result.Reachable && result.ModelFound))
                && (!embeddingChecked || result.EmbeddingDimensions.HasValue);
            endpoint.LastValidatedUtc = result.ValidatedUtc;
            endpoint.LastValidationResult = String.Join(" ", result.Messages);
            endpoint.LastValidationSucceeded = succeeded;
            result.Succeeded = succeeded;
            if (result.SupportsTools.HasValue) endpoint.SupportsTools = result.SupportsTools;
            if (result.EmbeddingDimensions.HasValue) endpoint.EmbeddingDimensions = result.EmbeddingDimensions;
            await _Database.ModelEndpoints.UpdateAsync(endpoint, token).ConfigureAwait(false);
            return result;
        }

        /// <summary>
        /// List models reported by an endpoint.
        /// </summary>
        /// <param name="endpoint">Endpoint.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Models.</returns>
        /// <exception cref="ModelCallException">The endpoint cannot be reached.</exception>
        public async Task<List<ModelListItem>> ListModelsAsync(ModelEndpoint endpoint, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            if (AgentCliService.IsAgentCli(endpoint.Provider)) return new List<ModelListItem>();
            string? key = await _Vault.ResolveAsync(endpoint.TenantId, endpoint.CredentialId, token).ConfigureAwait(false);
            List<ModelListItem> items = new List<ModelListItem>();
            try
            {
                using (ProviderClientBase client = _Factory.Create(endpoint, key))
                {
                    await foreach (ModelInformation m in client.ListModelsAsync(token).ConfigureAwait(false))
                    {
                        items.Add(new ModelListItem { Name = m.Name, DisplayName = m.DisplayName, InputTokenLimit = m.InputTokenLimit, OutputTokenLimit = m.OutputTokenLimit, SupportsTools = m.SupportsTools });
                        if (items.Count >= 500) break;
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is CapabilityNotSupportedException || ex is InvalidOperationException)
            {
                throw new ModelCallException("Unable to list models for " + endpoint.Name + ": " + ex.Message, null, false, ex);
            }

            return items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        /// <param name="disposing">True when called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing) _Concurrency.Dispose();
            _Disposed = true;
        }

        #endregion

        #region Private-Methods

        private async Task<StructuredCallResult<T>> ChatStructuredWithEndpointAsync<T>(ModelCallRequest request, ModelEndpoint endpoint, CancellationToken token) where T : class, IStructuredResult
        {
            bool? original = endpoint.SupportsTools;
            ModelEndpoint? stored = await _Database.ModelEndpoints.ReadAsync(endpoint.TenantId, endpoint.Id, token).ConfigureAwait(false);
            if (stored != null && stored.SupportsTools == false)
            {
                stored.SupportsTools = null;
                await _Database.ModelEndpoints.UpdateAsync(stored, token).ConfigureAwait(false);
            }

            try
            {
                return await ChatStructuredAsync<T>(request, token).ConfigureAwait(false);
            }
            finally
            {
                endpoint.SupportsTools = original;
            }
        }

        private static ModelEndpoint Clone(ModelEndpoint e)
        {
            return Serializer.Deserialize<ModelEndpoint>(Serializer.Serialize(e))!;
        }

        private List<ChatMessage> BuildMessages(string? system, string user)
        {
            List<ChatMessage> messages = new List<ChatMessage>();
            string guardrail = _Settings.Pipeline.ScopeGuardrail;
            string combined = String.IsNullOrWhiteSpace(system) ? guardrail : guardrail + "\n\n" + system.Trim();
            messages.Add(ChatMessage.System(combined));
            messages.Add(ChatMessage.User(String.IsNullOrWhiteSpace(user) ? "(no input)" : user));
            return messages;
        }

        private CompletionOptions BuildOptions(ResolvedModel resolved, ModelCallRequest request)
        {
            ModelEndpoint e = resolved.Endpoint;
            CompletionOptions options = new CompletionOptions
            {
                Model = resolved.Model,
                MaxTokens = request.MaxTokens ?? e.MaxTokens
            };

            if (e.Provider != ModelProviderEnum.Anthropic && e.Temperature.HasValue) options.Temperature = e.Temperature;
            if (resolved.ReasoningEffort.HasValue) options.ReasoningEffort = ToPolyPrompt(resolved.ReasoningEffort.Value);
            return options;
        }

        private static ReasoningEffort ToPolyPrompt(ReasoningEffortEnum effort)
        {
            switch (effort)
            {
                case ReasoningEffortEnum.None: return ReasoningEffort.None;
                case ReasoningEffortEnum.Minimal: return ReasoningEffort.Minimal;
                case ReasoningEffortEnum.Low: return ReasoningEffort.Low;
                case ReasoningEffortEnum.Medium: return ReasoningEffort.Medium;
                case ReasoningEffortEnum.High: return ReasoningEffort.High;
                case ReasoningEffortEnum.XHigh: return ReasoningEffort.XHigh;
                default: return ReasoningEffort.Max;
            }
        }

        private async Task<ModelExchange> SendAsync(ResolvedModel resolved, List<ChatMessage> messages, List<ToolDefinition>? tools, ModelCallRequest request, CallState state, CancellationToken token)
        {
            if (AgentCliService.IsAgentCli(resolved.Endpoint.Provider)) return await SendCliAsync(resolved, messages, request, state, token).ConfigureAwait(false);
            string? key;
            try
            {
                key = await _Vault.ResolveAsync(resolved.Endpoint.TenantId, resolved.Endpoint.CredentialId, token).ConfigureAwait(false);
            }
            catch (VaultSecretUnavailableException ex)
            {
                throw new ModelCallException(ex.Message, null, false, ex);
            }

            ChatRequest chat = new ChatRequest
            {
                Messages = messages,
                Tools = tools ?? new List<ToolDefinition>(),
                ToolChoice = tools != null && tools.Count > 0 ? "auto" : null,
                Options = BuildOptions(resolved, request)
            };
            state.Prompt = Render(messages);
            int attempts = _Settings.Pipeline.ModelCallAttempts;
            bool streaming = _Settings.Pipeline.UseStreaming;
            string task = request.Task.ToString();
            string provider = resolved.Endpoint.Provider.ToString();
            Exception? last = null;

            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                token.ThrowIfCancellationRequested();
                state.Attempts++;
                Stopwatch sw = Stopwatch.StartNew();
                using (Activity? span = MyMorningNewsTelemetry.StartSpan("polyprompt chat", ActivityKind.Client))
                {
                    span?.SetTag("mymorningnews.task", task);
                    span?.SetTag("gen_ai.system", provider);
                    span?.SetTag("gen_ai.request.model", resolved.Model);
                    span?.SetTag("mymorningnews.streaming", streaming);
                    await _Concurrency.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        ModelExchange exchange;
                        bool success;
                        string? error;
                        TokenUsage? usage;
                        try
                        {
                            using (ProviderClientBase client = _Factory.Create(resolved.Endpoint, key))
                            {
                                if (streaming)
                                {
                                    ChatStreamingResponse stream = await client.ChatStreamingAsync(chat, token).ConfigureAwait(false);
                                    if (stream.State != StreamingState.Failed)
                                    {
                                        await foreach (ChatStreamingChunk chunk in stream.Chunks.WithCancellation(token).ConfigureAwait(false))
                                        {
                                            state.ChunkCount++;
                                        }
                                    }

                                    success = stream.State == StreamingState.Completed;
                                    error = stream.StreamError ?? stream.Error;
                                    usage = stream.Usage;
                                    state.Streamed = true;
                                    state.TimeToFirstTokenMs = stream.TimeToFirstTokenMs >= 0 ? stream.TimeToFirstTokenMs : null;
                                    state.StreamingDurationMs = stream.TimeToLastTokenMs >= 0 && stream.TimeToFirstTokenMs >= 0 ? stream.TimeToLastTokenMs - stream.TimeToFirstTokenMs : null;
                                    state.OutputTokensPerSecond = stream.OverallTokensPerSecond > 0 ? Math.Round(stream.OverallTokensPerSecond, 2) : null;
                                    exchange = new ModelExchange
                                    {
                                        Text = stream.Text,
                                        ToolCalls = stream.ToolCalls ?? new List<ToolCall>(),
                                        FinishReason = stream.FinishReason,
                                        StatusCode = stream.StatusCode,
                                        AssistantMessage = success ? stream.ToAssistantMessage() : ChatMessage.Assistant(stream.Text ?? String.Empty)
                                    };
                                }
                                else
                                {
                                    ChatResponse response = await client.ChatAsync(chat, token).ConfigureAwait(false);
                                    success = response.Success;
                                    error = response.Error;
                                    usage = response.Usage;
                                    state.Streamed = false;
                                    exchange = new ModelExchange
                                    {
                                        Text = response.Text,
                                        ToolCalls = response.ToolCalls ?? new List<ToolCall>(),
                                        FinishReason = response.FinishReason,
                                        StatusCode = response.StatusCode,
                                        AssistantMessage = response.Success ? response.ToAssistantMessage() : ChatMessage.Assistant(response.Text ?? String.Empty)
                                    };
                                }
                            }
                        }
                        catch (ArgumentException ex)
                        {
                            throw new ModelCallException(ErrorText.Plain(ex), null, false, ex);
                        }
                        catch (Exception ex) when (ex is HttpRequestException || (ex is OperationCanceledException && !token.IsCancellationRequested))
                        {
                            last = ex;
                            state.DurationMs += sw.Elapsed.TotalMilliseconds;
                            span?.SetStatus(ActivityStatusCode.Error, ex.Message);
                            MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.LlmCalls, "task", task, "provider", provider, "outcome", "unreachable");
                            if (attempt < attempts) await Task.Delay(Backoff(attempt), token).ConfigureAwait(false);
                            continue;
                        }

                        double seconds = sw.Elapsed.TotalSeconds;
                        state.DurationMs += seconds * 1000;
                        Accumulate(state, usage, task, provider);
                        foreach (ToolCall call in exchange.ToolCalls) state.ToolNames.Add(call.Name);
                        MyMorningNewsTelemetry.Record(MyMorningNewsTelemetry.LlmCallDuration, seconds, "task", task, "provider", provider);
                        if (state.Streamed && state.TimeToFirstTokenMs.HasValue)
                            MyMorningNewsTelemetry.Record(MyMorningNewsTelemetry.LlmTimeToFirstToken, state.TimeToFirstTokenMs.Value / 1000.0, "task", task, "provider", provider);
                        if (exchange.ToolCalls.Count > 0) MyMorningNewsTelemetry.Add(MyMorningNewsTelemetry.LlmToolCalls, exchange.ToolCalls.Count, "task", task, "provider", provider);

                        if (success && String.Equals(exchange.FinishReason, "refusal", StringComparison.OrdinalIgnoreCase))
                        {
                            MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.LlmCalls, "task", task, "provider", provider, "outcome", "refusal");
                            throw new ModelRefusalException("The model declined the request.");
                        }

                        if (success)
                        {
                            MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.LlmCalls, "task", task, "provider", provider, "outcome", "success");
                            return exchange;
                        }

                        bool transient = IsTransient(exchange.StatusCode);
                        MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.LlmCalls, "task", task, "provider", provider, "outcome", transient ? "retryable_error" : "error");
                        span?.SetStatus(ActivityStatusCode.Error, error);
                        last = new ModelCallException("Model call to " + resolved.Endpoint.Name + " failed (" + (exchange.StatusCode?.ToString() ?? "no status") + "): " + (error ?? "unknown error"), exchange.StatusCode, transient);
                        if (!transient) throw (ModelCallException)last;
                        if (attempt < attempts) await Task.Delay(Backoff(attempt), token).ConfigureAwait(false);
                    }
                    finally
                    {
                        _Concurrency.Release();
                    }
                }
            }

            if (last is ModelCallException mce) throw mce;
            throw new ModelCallException("Model endpoint " + resolved.Endpoint.Name + " is unreachable: " + (last?.Message ?? "unknown error"), null, true, last);
        }

        private async Task<ModelExchange> SendCliAsync(ResolvedModel resolved, List<ChatMessage> messages, ModelCallRequest request, CallState state, CancellationToken token)
        {
            ModelEndpoint endpoint = resolved.Endpoint;
            string? secret = null;
            if (AgentCliService.NeedsSecret(AgentCliService.EffectiveMethod(endpoint)))
            {
                try
                {
                    secret = await _Vault.ResolveAsync(endpoint.TenantId, endpoint.CredentialId, token).ConfigureAwait(false);
                }
                catch (VaultSecretUnavailableException ex)
                {
                    throw new ModelCallException(ex.Message, null, false, ex);
                }
            }

            state.Prompt = Render(messages);
            state.Streamed = false;
            int attempts = _Settings.Pipeline.ModelCallAttempts;
            string task = request.Task.ToString();
            string provider = endpoint.Provider.ToString();
            ModelCallException? last = null;
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                token.ThrowIfCancellationRequested();
                state.Attempts++;
                Stopwatch sw = Stopwatch.StartNew();
                using (Activity? span = MyMorningNewsTelemetry.StartSpan("polyprompt cli", ActivityKind.Client))
                {
                    span?.SetTag("mymorningnews.task", task);
                    span?.SetTag("gen_ai.system", provider);
                    span?.SetTag("gen_ai.request.model", resolved.Model);
                    await _Concurrency.WaitAsync(token).ConfigureAwait(false);
                    CliAgentResult result;
                    try
                    {
                        result = await _Clis.RunAsync(endpoint, secret, resolved.Model, messages, token).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception || ex is System.IO.IOException || ex is UnauthorizedAccessException)
                    {
                        throw new ModelCallException(ErrorText.Plain(ex), null, false, ex);
                    }
                    finally
                    {
                        _Concurrency.Release();
                    }

                    double seconds = sw.Elapsed.TotalSeconds;
                    state.DurationMs += seconds * 1000;
                    Accumulate(state, result.Usage, task, provider);
                    if (result.CostUsd.HasValue && AgentCliService.EffectiveMethod(endpoint) == ModelAuthMethodEnum.ApiKey)
                        state.ReportedCostUsd = (state.ReportedCostUsd ?? 0) + result.CostUsd.Value;
                    MyMorningNewsTelemetry.Record(MyMorningNewsTelemetry.LlmCallDuration, seconds, "task", task, "provider", provider);
                    if (result.Succeeded)
                    {
                        MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.LlmCalls, "task", task, "provider", provider, "outcome", "success");
                        return new ModelExchange { Text = result.Text, FinishReason = "stop", StatusCode = 200, AssistantMessage = ChatMessage.Assistant(result.Text ?? String.Empty) };
                    }

                    int? status;
                    bool transient;
                    string reason;
                    switch (result.FailureKind)
                    {
                        case CliFailureKind.NotInstalled:
                            status = null; transient = false; reason = "the " + provider + " CLI is not installed on the server";
                            break;
                        case CliFailureKind.NotAuthenticated:
                            status = 401; transient = false; reason = "the CLI is not signed in or its sign-in was rejected (" + AgentCliService.Describe(AgentCliService.EffectiveMethod(endpoint), endpoint.Provider) + ")";
                            break;
                        case CliFailureKind.RateLimited:
                            status = 429; transient = true; reason = "the provider's usage limit was reached";
                            break;
                        case CliFailureKind.TimedOut:
                            status = 504; transient = true; reason = "the CLI did not finish within " + Math.Round(endpoint.TimeoutMs / 1000.0) + " seconds";
                            break;
                        default:
                            status = null; transient = false; reason = "the CLI failed";
                            break;
                    }

                    string detail = FirstLine(result.Error) ?? FirstLine(result.StandardError) ?? result.FailureKind.ToString();
                    span?.SetStatus(ActivityStatusCode.Error, detail);
                    MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.LlmCalls, "task", task, "provider", provider, "outcome", transient ? "retryable_error" : "error");
                    last = new ModelCallException(endpoint.Name + ": " + reason + ". " + detail, status, transient);
                    if (!transient) throw last;
                    if (attempt < attempts) await Task.Delay(Backoff(attempt), token).ConfigureAwait(false);
                }
            }

            throw last ?? new ModelCallException(endpoint.Name + " did not answer.", null, true);
        }

        private static string? FirstLine(string? text)
        {
            if (String.IsNullOrWhiteSpace(text)) return null;
            string plain = System.Text.RegularExpressions.Regex.Replace(text, "\u001b\\[[0-9;?]*[A-Za-z]", String.Empty);
            string line = plain.Trim().Split('\n')[0].Trim();
            if (line.Length == 0) return null;
            return line.Length > 400 ? line.Substring(0, 400) : line;
        }

        private static void Accumulate(CallState state, TokenUsage? usage, string task, string provider)
        {
            if (usage == null) return;
            int input = usage.PromptTokens ?? 0;
            int output = usage.CompletionTokens ?? 0;
            int cached = usage.CachedPromptTokens ?? 0;
            int write = usage.CacheCreationTokens ?? 0;
            int reasoning = usage.ReasoningTokens ?? 0;
            state.InputTokens += input;
            state.OutputTokens += output;
            state.CachedInputTokens += cached;
            state.CacheWriteTokens += write;
            state.ReasoningTokens += reasoning;
            MyMorningNewsTelemetry.Add(MyMorningNewsTelemetry.LlmTokens, input, "task", task, "provider", provider, "type", "input");
            MyMorningNewsTelemetry.Add(MyMorningNewsTelemetry.LlmTokens, output, "task", task, "provider", provider, "type", "output");
            if (cached > 0) MyMorningNewsTelemetry.Add(MyMorningNewsTelemetry.LlmTokens, cached, "task", task, "provider", provider, "type", "cached_input");
            if (write > 0) MyMorningNewsTelemetry.Add(MyMorningNewsTelemetry.LlmTokens, write, "task", task, "provider", provider, "type", "cache_write");
            if (reasoning > 0) MyMorningNewsTelemetry.Add(MyMorningNewsTelemetry.LlmTokens, reasoning, "task", task, "provider", provider, "type", "reasoning");
        }

        private static TimeSpan Backoff(int attempt)
        {
            return TimeSpan.FromMilliseconds(Math.Min(8000, 500 * Math.Pow(2, attempt - 1)) + Random.Shared.Next(0, 250));
        }

        private static bool IsTransient(int? status)
        {
            if (!status.HasValue) return true;
            int s = status.Value;
            return s == 408 || s == 409 || s == 425 || s == 429 || s >= 500;
        }

        private static string ExtractJson(string? text)
        {
            if (String.IsNullOrWhiteSpace(text)) return String.Empty;
            string t = text.Trim();
            int fence = t.IndexOf("```", StringComparison.Ordinal);
            if (fence >= 0)
            {
                int start = t.IndexOf('\n', fence);
                int end = t.IndexOf("```", start > 0 ? start : fence + 3, StringComparison.Ordinal);
                if (start > 0 && end > start) t = t.Substring(start + 1, end - start - 1).Trim();
            }

            int open = t.IndexOf('{');
            int close = t.LastIndexOf('}');
            return open >= 0 && close > open ? t.Substring(open, close - open + 1) : t;
        }

        private static string Render(List<ChatMessage> messages)
        {
            return String.Join("\n\n", messages.Select(m => "[" + m.Role + "]\n" + (m.Text ?? String.Empty)));
        }

        private async Task LogAsync(CallState state, bool success, string? error, int? statusCode, string? finishReason, CancellationToken token)
        {
            try
            {
                ModelEndpoint e = state.Resolved.Endpoint;
                decimal? inputCost = null;
                decimal? outputCost = null;
                if (e.InputCostPerMTok.HasValue || e.OutputCostPerMTok.HasValue)
                {
                    decimal inRate = e.InputCostPerMTok ?? 0;
                    int uncached = Math.Max(0, state.InputTokens - state.CachedInputTokens - state.CacheWriteTokens);
                    inputCost = Math.Round(uncached / 1000000m * inRate
                        + state.CachedInputTokens / 1000000m * (e.CachedInputCostPerMTok ?? inRate)
                        + state.CacheWriteTokens / 1000000m * (e.CacheWriteCostPerMTok ?? inRate), 6);
                    outputCost = Math.Round(state.OutputTokens / 1000000m * (e.OutputCostPerMTok ?? 0), 6);
                }

                decimal? cost = inputCost.HasValue || outputCost.HasValue ? (inputCost ?? 0) + (outputCost ?? 0) : null;
                if (!cost.HasValue && state.ReportedCostUsd.HasValue) cost = Math.Round(state.ReportedCostUsd.Value, 6);
                state.EstimatedCostUsd = cost;
                if (cost.HasValue) MyMorningNewsTelemetry.AddDouble(MyMorningNewsTelemetry.LlmCostUsd, (double)cost.Value, "task", state.Request.Task.ToString(), "provider", e.Provider.ToString());
                bool capture = _Settings.Diagnostics.CaptureLlmPayloads;
                int max = _Settings.Diagnostics.MaxPayloadCharacters;
                LlmCall call = new LlmCall
                {
                    TenantId = state.Request.TenantId,
                    UserId = state.Request.UserId,
                    TopicId = state.Request.TopicId,
                    Task = state.Request.Task,
                    RunId = state.Request.RunId,
                    RunStageId = state.Request.RunStageId ?? (state.Request.RunId != null ? RunStageScope.Current : null),
                    RefinementSessionId = state.Request.RefinementSessionId,
                    BriefingId = state.Request.BriefingId,
                    ModelEndpointId = e.Id,
                    Provider = e.Provider.ToString(),
                    Model = state.Resolved.Model,
                    PromptKey = state.Request.PromptKey,
                    PromptTemplateVersionId = state.Request.PromptTemplateVersionId,
                    Streamed = state.Streamed,
                    Attempts = Math.Max(1, state.Attempts),
                    InputTokens = state.InputTokens,
                    CachedInputTokens = state.CachedInputTokens,
                    CacheWriteTokens = state.CacheWriteTokens,
                    OutputTokens = state.OutputTokens,
                    ReasoningTokens = state.ReasoningTokens,
                    TotalTokens = state.InputTokens + state.OutputTokens,
                    DurationMs = Math.Round(state.DurationMs, 2),
                    TimeToFirstTokenMs = state.TimeToFirstTokenMs,
                    StreamingDurationMs = state.StreamingDurationMs,
                    ChunkCount = state.ChunkCount,
                    OutputTokensPerSecond = state.OutputTokensPerSecond,
                    ToolCallCount = state.ToolNames.Count,
                    ToolNames = state.ToolNames.Distinct(StringComparer.Ordinal).ToList(),
                    Success = success,
                    StatusCode = statusCode,
                    FinishReason = finishReason,
                    ErrorMessage = error,
                    StructuredOutputRepairs = state.Repairs,
                    InputCostUsd = inputCost,
                    OutputCostUsd = outputCost,
                    EstimatedCostUsd = cost,
                    IsDryRun = state.Request.IsDryRun,
                    PromptText = capture ? Clip(state.Prompt, max) : null,
                    ResponseText = capture ? Clip(state.Output, max) : null
                };
                call = await _Database.LlmCalls.CreateAsync(call, token).ConfigureAwait(false);
                state.LlmCallId = call.Id;
                bool rejected = statusCode == 401 || statusCode == 403;
                if ((success && e.LastValidationSucceeded != true) || (rejected && e.LastValidationSucceeded != false))
                    await RecordVerificationAsync(e, success, success ? null : error, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _Logging.Warn(_Header + "unable to log model call: " + ex.Message);
            }
        }

        private async Task RecordVerificationAsync(ModelEndpoint used, bool succeeded, string? error, CancellationToken token)
        {
            ModelEndpoint? current = await _Database.ModelEndpoints.ReadAsync(used.TenantId, used.Id, token).ConfigureAwait(false);
            if (current == null) return;
            if (!String.Equals(current.CredentialId, used.CredentialId, StringComparison.Ordinal)
                || !String.Equals(current.Endpoint, used.Endpoint, StringComparison.Ordinal)
                || current.Provider != used.Provider) return;
            current.LastValidationSucceeded = succeeded;
            current.LastValidatedUtc = DateTime.UtcNow;
            if (!succeeded && !String.IsNullOrWhiteSpace(error)) current.LastValidationResult = error;
            await _Database.ModelEndpoints.UpdateAsync(current, token).ConfigureAwait(false);
            used.LastValidationSucceeded = succeeded;
        }

        private static string? Clip(string? value, int max)
        {
            if (value == null) return null;
            return value.Length > max ? value.Substring(0, max) : value;
        }

        #endregion
    }
}
