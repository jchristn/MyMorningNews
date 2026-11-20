namespace MyMorningNews.Benchmark.Client
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Benchmark.Reporting;

    /// <summary>
    /// Typed client for the My Morning News REST API: the only place in the harness that knows routes and payload
    /// shapes. Instances share one pooled HttpClient; ForUser returns a client that sends a session token, and the
    /// administration client sends the x-api-key header. Thread safety: safe for concurrent requests.
    /// </summary>
    public class MyMorningNewsClient
    {
        #region Public-Members

        /// <summary>
        /// Server base URL, such as http://127.0.0.1:12500.
        /// </summary>
        public string BaseUrl { get; }

        /// <summary>
        /// Session token, or null.
        /// </summary>
        public string? Token { get; }

        /// <summary>
        /// Administration key, or null.
        /// </summary>
        public string? AdminApiKey { get; }

        /// <summary>
        /// Tenant sent in x-tenant-guid with the administration key, or null.
        /// </summary>
        public string? TenantId { get; }

        /// <summary>
        /// The model tasks the product assigns (ModelTaskEnum).
        /// </summary>
        public static readonly IReadOnlyList<string> ModelTasks = new List<string>
        {
            "Refinement", "QueryPlanning", "Extraction", "Scoring", "Synthesis", "Prediction", "Suggestion", "Composition", "PodcastScript", "FeedbackAnalysis", "Embedding"
        };

        #endregion

        #region Private-Members

        private readonly HttpClient _Http;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="http">Shared HTTP client.</param>
        /// <param name="baseUrl">Server base URL.</param>
        /// <param name="token">Session token, or null.</param>
        /// <param name="adminApiKey">Administration key, or null.</param>
        /// <param name="tenantId">Tenant for administration-key calls, or null.</param>
        /// <exception cref="ArgumentNullException">http or baseUrl is null.</exception>
        public MyMorningNewsClient(HttpClient http, string baseUrl, string? token = null, string? adminApiKey = null, string? tenantId = null)
        {
            _Http = http ?? throw new ArgumentNullException(nameof(http));
            ArgumentNullException.ThrowIfNull(baseUrl);
            BaseUrl = baseUrl.TrimEnd('/');
            Token = token;
            AdminApiKey = adminApiKey;
            TenantId = tenantId;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// A client that acts as a signed-in user.
        /// </summary>
        /// <param name="token">Session token.</param>
        /// <returns>Client.</returns>
        public MyMorningNewsClient ForUser(string token)
        {
            ArgumentNullException.ThrowIfNull(token);
            return new MyMorningNewsClient(_Http, BaseUrl, token, null, null);
        }

        /// <summary>
        /// A client that sends no credentials (public pages).
        /// </summary>
        /// <returns>Client.</returns>
        public MyMorningNewsClient Anonymous()
        {
            return new MyMorningNewsClient(_Http, BaseUrl, null, null, null);
        }

        /// <summary>
        /// A client that uses the administration key in a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <returns>Client.</returns>
        public MyMorningNewsClient ForTenant(string tenantId)
        {
            ArgumentNullException.ThrowIfNull(tenantId);
            return new MyMorningNewsClient(_Http, BaseUrl, null, AdminApiKey, tenantId);
        }

        /// <summary>
        /// Send a request.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="path">Path and query.</param>
        /// <param name="body">Body to serialize as JSON, or null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <param name="headers">Extra headers, or null.</param>
        /// <returns>Result; transport failures return status 0 with Error set.</returns>
        public async Task<ApiResult> SendAsync(HttpMethod method, string path, object? body, CancellationToken token, IDictionary<string, string>? headers = null)
        {
            ArgumentNullException.ThrowIfNull(method);
            ArgumentNullException.ThrowIfNull(path);
            ApiResult result = new ApiResult { Method = method.Method, Path = path };
            Stopwatch sw = Stopwatch.StartNew();
            using (HttpRequestMessage req = new HttpRequestMessage(method, BaseUrl + path))
            {
                if (Token != null) req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + Token);
                if (AdminApiKey != null) req.Headers.TryAddWithoutValidation("x-api-key", AdminApiKey);
                if (TenantId != null) req.Headers.TryAddWithoutValidation("x-tenant-guid", TenantId);
                if (headers != null) foreach (KeyValuePair<string, string> h in headers) req.Headers.TryAddWithoutValidation(h.Key, h.Value);
                if (body != null) req.Content = new StringContent(body as string ?? Json.Serialize(body), Encoding.UTF8, "application/json");
                try
                {
                    using (HttpResponseMessage resp = await _Http.SendAsync(req, token).ConfigureAwait(false))
                    {
                        result.Status = (int)resp.StatusCode;
                        result.ContentType = resp.Content.Headers.ContentType?.ToString() ?? String.Empty;
                        result.Body = await resp.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !token.IsCancellationRequested))
                {
                    result.Status = 0;
                    result.Error = ex.Message;
                }
            }

            result.DurationMs = sw.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>
        /// POST a form (application/x-www-form-urlencoded), as the public email pages do. No credentials are sent.
        /// </summary>
        /// <param name="path">Path and query.</param>
        /// <param name="form">Form fields.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        public async Task<ApiResult> PostFormAsync(string path, Dictionary<string, string> form, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(path);
            ArgumentNullException.ThrowIfNull(form);
            ApiResult result = new ApiResult { Method = "POST", Path = path };
            Stopwatch sw = Stopwatch.StartNew();
            using (HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, BaseUrl + path))
            using (FormUrlEncodedContent content = new FormUrlEncodedContent(form))
            {
                req.Content = content;
                try
                {
                    using (HttpResponseMessage resp = await _Http.SendAsync(req, token).ConfigureAwait(false))
                    {
                        result.Status = (int)resp.StatusCode;
                        result.ContentType = resp.Content.Headers.ContentType?.ToString() ?? String.Empty;
                        result.Body = await resp.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !token.IsCancellationRequested))
                {
                    result.Error = ex.Message;
                }
            }

            result.DurationMs = sw.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>
        /// GET a path.
        /// </summary>
        /// <param name="path">Path and query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        public Task<ApiResult> GetAsync(string path, CancellationToken token)
        {
            return SendAsync(HttpMethod.Get, path, null, token);
        }

        /// <summary>
        /// Health check (GET /v1.0/api/health).
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        public Task<ApiResult> HealthAsync(CancellationToken token)
        {
            return GetAsync("/v1.0/api/health", token);
        }

        /// <summary>
        /// Server information (GET /v1.0/api/server-info) as raw JSON.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        public Task<ApiResult> ServerInfoAsync(CancellationToken token)
        {
            return GetAsync("/v1.0/api/server-info", token);
        }

        /// <summary>
        /// The scope guardrail from the server settings (GET /v1.0/api/settings), or empty when it cannot be read.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Guardrail text.</returns>
        public async Task<string> ScopeGuardrailAsync(CancellationToken token)
        {
            ApiResult r = await GetAsync("/v1.0/api/settings", token).ConfigureAwait(false);
            if (!r.Success) return String.Empty;
            using (JsonDocument doc = JsonDocument.Parse(r.Body))
            {
                return Find(doc.RootElement, "scopeGuardrail") ?? String.Empty;
            }
        }

        /// <summary>
        /// List tenants (administration key).
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tenants.</returns>
        public async Task<List<IdDto>> ListTenantsAsync(CancellationToken token)
        {
            ApiResult r = (await GetAsync("/v1.0/tenants?pageSize=1000", token).ConfigureAwait(false)).Expect(200);
            using (JsonDocument doc = JsonDocument.Parse(r.Body))
            {
                if (doc.RootElement.ValueKind == JsonValueKind.Array) return r.Json<List<IdDto>>();
            }

            return r.Json<PageDto<IdDto>>().Items;
        }

        /// <summary>
        /// Create a tenant (administration key).
        /// </summary>
        /// <param name="name">Name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tenant.</returns>
        public async Task<IdDto> CreateTenantAsync(string name, CancellationToken token)
        {
            return (await SendAsync(HttpMethod.Post, "/v1.0/tenants", new { name }, token).ConfigureAwait(false)).Expect(201).Json<IdDto>();
        }

        /// <summary>
        /// Delete a tenant and everything in it (administration key).
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        public Task<ApiResult> DeleteTenantAsync(string tenantId, CancellationToken token)
        {
            return SendAsync(HttpMethod.Delete, "/v1.0/tenants/" + tenantId, null, token);
        }

        /// <summary>
        /// Create a tenant administrator (administration key).
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="email">Email.</param>
        /// <param name="password">Password.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>User.</returns>
        public async Task<IdDto> CreateUserAsync(string tenantId, string email, string password, CancellationToken token)
        {
            return (await SendAsync(HttpMethod.Post, "/v1.0/tenants/" + tenantId + "/users", new { email, password, firstName = "Bench", lastName = "Reader", isTenantAdmin = true }, token).ConfigureAwait(false))
                .Expect(201).Json<IdDto>();
        }

        /// <summary>
        /// Sign in (POST /v1.0/token).
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="email">Email.</param>
        /// <param name="password">Password.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Token.</returns>
        public async Task<TokenDto> LoginAsync(string tenantId, string email, string password, CancellationToken token)
        {
            Dictionary<string, string> headers = new Dictionary<string, string> { { "x-tenant-guid", tenantId }, { "x-email", email }, { "x-password", password } };
            return (await SendAsync(HttpMethod.Post, "/v1.0/token", null, token, headers).ConfigureAwait(false)).Expect(200).Json<TokenDto>();
        }

        /// <summary>
        /// Create a model endpoint.
        /// </summary>
        /// <param name="body">ModelEndpointUpsertRequest fields.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Endpoint.</returns>
        public async Task<IdDto> CreateModelEndpointAsync(object body, CancellationToken token)
        {
            return (await SendAsync(HttpMethod.Post, "/v1.0/api/model-endpoints", body, token).ConfigureAwait(false)).Expect(201).Json<IdDto>();
        }

        /// <summary>
        /// Assign an endpoint to a task.
        /// </summary>
        /// <param name="task">Task.</param>
        /// <param name="endpointId">Endpoint.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        public async Task<ApiResult> AssignModelAsync(string task, string endpointId, CancellationToken token)
        {
            return (await SendAsync(HttpMethod.Put, "/v1.0/api/model-assignments", new { task, modelEndpointId = endpointId }, token).ConfigureAwait(false)).Expect(200);
        }

        /// <summary>
        /// Model assignments as raw JSON.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        public Task<ApiResult> ModelAssignmentsAsync(CancellationToken token)
        {
            return GetAsync("/v1.0/api/model-assignments", token);
        }

        /// <summary>
        /// Create a SearXNG search provider.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <param name="endpoint">Endpoint.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Provider.</returns>
        public async Task<IdDto> CreateSearchProviderAsync(string name, string endpoint, CancellationToken token)
        {
            return (await SendAsync(HttpMethod.Post, "/v1.0/api/search-providers", new { name, kind = "Searxng", endpoint }, token).ConfigureAwait(false)).Expect(201).Json<IdDto>();
        }

        /// <summary>
        /// Point a search provider at another endpoint.
        /// </summary>
        /// <param name="providerId">Provider.</param>
        /// <param name="endpoint">Endpoint.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        public async Task<ApiResult> UpdateSearchProviderAsync(string providerId, string endpoint, CancellationToken token)
        {
            return (await SendAsync(HttpMethod.Put, "/v1.0/api/search-providers/" + providerId, new { endpoint }, token).ConfigureAwait(false)).Expect(200);
        }

        /// <summary>
        /// Create a topic.
        /// </summary>
        /// <param name="body">TopicUpsertRequest fields.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Topic.</returns>
        public async Task<TopicDto> CreateTopicAsync(object body, CancellationToken token)
        {
            return (await SendAsync(HttpMethod.Post, "/v1.0/api/topics", body, token).ConfigureAwait(false)).Expect(201).Json<TopicDto>();
        }

        /// <summary>
        /// Add a dimension to a topic.
        /// </summary>
        /// <param name="topicId">Topic.</param>
        /// <param name="body">Dimension fields.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        public async Task<ApiResult> AddDimensionAsync(string topicId, object body, CancellationToken token)
        {
            return (await SendAsync(HttpMethod.Post, "/v1.0/api/topics/" + topicId + "/dimensions", body, token).ConfigureAwait(false)).Expect(201);
        }

        /// <summary>
        /// Add a constraint to a topic.
        /// </summary>
        /// <param name="topicId">Topic.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="value">Value.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        public async Task<ApiResult> AddConstraintAsync(string topicId, string kind, string value, CancellationToken token)
        {
            return (await SendAsync(HttpMethod.Post, "/v1.0/api/topics/" + topicId + "/constraints", new { kind, value }, token).ConfigureAwait(false)).Expect(201);
        }

        /// <summary>
        /// Set a topic's search providers.
        /// </summary>
        /// <param name="topicId">Topic.</param>
        /// <param name="providerId">The only provider.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        public async Task<ApiResult> SetTopicProviderAsync(string topicId, string providerId, CancellationToken token)
        {
            return (await SendAsync(HttpMethod.Put, "/v1.0/api/topics/" + topicId + "/search-providers", new[] { new { searchProviderId = providerId, enabled = true } }, token).ConfigureAwait(false)).Expect(200);
        }

        /// <summary>
        /// Queue a run now (202).
        /// </summary>
        /// <param name="topicId">Topic.</param>
        /// <param name="dryRun">Dry run.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result; the body is the queued run.</returns>
        public Task<ApiResult> RunTopicAsync(string topicId, bool dryRun, CancellationToken token)
        {
            return SendAsync(HttpMethod.Post, "/v1.0/api/topics/" + topicId + "/run" + (dryRun ? "?dryRun=true" : String.Empty), null, token);
        }

        /// <summary>
        /// Read a run.
        /// </summary>
        /// <param name="runId">Run.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Run.</returns>
        public async Task<RunDto> GetRunAsync(string runId, CancellationToken token)
        {
            return (await GetAsync("/v1.0/api/runs/" + runId, token).ConfigureAwait(false)).Expect(200).Json<RunDto>();
        }

        /// <summary>
        /// Poll a run until it finishes.
        /// </summary>
        /// <param name="runId">Run.</param>
        /// <param name="timeout">Timeout.</param>
        /// <param name="pollMs">Poll interval in milliseconds.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The finished run.</returns>
        /// <exception cref="TimeoutException">The run did not finish in time.</exception>
        public async Task<RunDto> WaitForRunAsync(string runId, TimeSpan timeout, int pollMs, CancellationToken token)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                ApiResult r = await GetAsync("/v1.0/api/runs/" + runId, token).ConfigureAwait(false);
                if (r.Success)
                {
                    RunDto run = r.Json<RunDto>();
                    if (run.Finished) return run;
                }

                if (DateTime.UtcNow > deadline) throw new TimeoutException("Run " + runId + " did not finish within " + timeout.TotalSeconds + " s.");
                await Task.Delay(pollMs, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// A run's stages.
        /// </summary>
        /// <param name="runId">Run.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Stages in order.</returns>
        public async Task<List<RunStageDto>> GetStagesAsync(string runId, CancellationToken token)
        {
            return (await GetAsync("/v1.0/api/runs/" + runId + "/stages", token).ConfigureAwait(false)).Expect(200).Json<List<RunStageDto>>().OrderBy(s => s.SortOrder).ToList();
        }

        /// <summary>
        /// A run's search results with their dispositions.
        /// </summary>
        /// <param name="runId">Run.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Results.</returns>
        public async Task<List<SearchResultDto>> GetResultsAsync(string runId, CancellationToken token)
        {
            return (await GetAsync("/v1.0/api/runs/" + runId + "/results", token).ConfigureAwait(false)).Expect(200).Json<List<SearchResultDto>>();
        }

        /// <summary>
        /// A run's model calls.
        /// </summary>
        /// <param name="runId">Run.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Calls.</returns>
        public async Task<List<LlmCallDto>> GetRunCallsAsync(string runId, CancellationToken token)
        {
            ApiResult r = (await GetAsync("/v1.0/api/runs/" + runId + "/llm-calls", token).ConfigureAwait(false)).Expect(200);
            using (JsonDocument doc = JsonDocument.Parse(r.Body))
            {
                if (doc.RootElement.ValueKind == JsonValueKind.Array) return r.Json<List<LlmCallDto>>();
            }

            return r.Json<PageDto<LlmCallDto>>().Items;
        }

        /// <summary>
        /// Every finding of a run, including duplicates and findings under the display threshold.
        /// </summary>
        /// <param name="runId">Run.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Findings.</returns>
        public async Task<List<FindingDto>> GetRunFindingsAsync(string runId, CancellationToken token)
        {
            ApiResult r = (await GetAsync("/v1.0/api/findings?runId=" + runId + "&includeDuplicates=true&includeBelowThreshold=true&includeDryRun=true&includeHidden=true&pageSize=1000", token).ConfigureAwait(false)).Expect(200);
            return r.Json<PageDto<FindingDto>>().Items;
        }

        /// <summary>
        /// A finding with its evidence.
        /// </summary>
        /// <param name="findingId">Finding.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Detail.</returns>
        public async Task<FindingDetailDto> GetFindingAsync(string findingId, CancellationToken token)
        {
            return (await GetAsync("/v1.0/api/findings/" + findingId, token).ConfigureAwait(false)).Expect(200).Json<FindingDetailDto>();
        }

        /// <summary>
        /// Rate a finding.
        /// </summary>
        /// <param name="findingId">Finding.</param>
        /// <param name="relevance">Relevant, SomewhatRelevant, or NotRelevant.</param>
        /// <param name="reason">Reason, or null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        public Task<ApiResult> RateFindingAsync(string findingId, string relevance, string? reason, CancellationToken token)
        {
            return SendAsync(HttpMethod.Post, "/v1.0/api/feedback", new { targetType = "Finding", targetId = findingId, relevance, reason, channel = "Api" }, token);
        }

        /// <summary>
        /// Briefings of a run.
        /// </summary>
        /// <param name="runId">Run.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Briefings.</returns>
        public async Task<List<BriefingDto>> GetRunBriefingsAsync(string runId, CancellationToken token)
        {
            return (await GetAsync("/v1.0/api/briefings?runId=" + runId + "&pageSize=100", token).ConfigureAwait(false)).Expect(200).Json<PageDto<BriefingDto>>().Items;
        }

        /// <summary>
        /// A briefing with its Markdown (list results omit it).
        /// </summary>
        /// <param name="briefingId">Briefing.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Briefing.</returns>
        public async Task<BriefingDto> GetBriefingAsync(string briefingId, CancellationToken token)
        {
            return (await GetAsync("/v1.0/api/briefings/" + briefingId, token).ConfigureAwait(false)).Expect(200).Json<BriefingDetailDto>().Briefing;
        }

        /// <summary>
        /// An email message with its rendered HTML and text (list results omit them).
        /// </summary>
        /// <param name="messageId">Message.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Message.</returns>
        public async Task<EmailMessageDto> GetEmailAsync(string messageId, CancellationToken token)
        {
            return (await GetAsync("/v1.0/api/email-messages/" + messageId, token).ConfigureAwait(false)).Expect(200).Json<EmailMessageDto>();
        }

        /// <summary>
        /// Request audio for a briefing (202).
        /// </summary>
        /// <param name="briefingId">Briefing.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result; the body is the queued episode.</returns>
        public Task<ApiResult> RequestAudioAsync(string briefingId, CancellationToken token)
        {
            return SendAsync(HttpMethod.Post, "/v1.0/api/briefings/" + briefingId + "/audio", new { }, token);
        }

        /// <summary>
        /// Audio episodes of a briefing.
        /// </summary>
        /// <param name="briefingId">Briefing.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Episodes.</returns>
        public async Task<List<MediaDto>> GetMediaAsync(string briefingId, CancellationToken token)
        {
            return (await GetAsync("/v1.0/api/media?briefingId=" + briefingId + "&pageSize=100", token).ConfigureAwait(false)).Expect(200).Json<PageDto<MediaDto>>().Items;
        }

        /// <summary>
        /// Email messages of a run.
        /// </summary>
        /// <param name="runId">Run.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Messages.</returns>
        public async Task<List<EmailMessageDto>> GetRunEmailsAsync(string runId, CancellationToken token)
        {
            return (await GetAsync("/v1.0/api/email-messages?runId=" + runId + "&pageSize=100", token).ConfigureAwait(false)).Expect(200).Json<PageDto<EmailMessageDto>>().Items;
        }

        /// <summary>
        /// Create a plan (administration key).
        /// </summary>
        /// <param name="key">Plan key.</param>
        /// <param name="name">Name.</param>
        /// <param name="features">Feature values.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result; the body is a PlanResponse.</returns>
        public Task<ApiResult> CreatePlanAsync(string key, string name, Dictionary<string, string> features, CancellationToken token)
        {
            return SendAsync(HttpMethod.Post, "/v1.0/api/admin/plans", new { key, name, description = "Benchmark spend cap.", features }, token);
        }

        /// <summary>
        /// Give a user a plan (administration key).
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User.</param>
        /// <param name="planId">Plan.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        public Task<ApiResult> AssignPlanAsync(string tenantId, string userId, string planId, CancellationToken token)
        {
            return SendAsync(HttpMethod.Post, "/v1.0/api/admin/plan-assignments", new { tenantId, userId, planId, durationDays = 1, note = "Benchmark spend cap." }, token);
        }

        #endregion

        #region Private-Methods

        private static string? Find(JsonElement e, string name)
        {
            if (e.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty p in e.EnumerateObject())
                {
                    if (String.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.String) return p.Value.GetString();
                    string? inner = Find(p.Value, name);
                    if (inner != null) return inner;
                }
            }
            else if (e.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in e.EnumerateArray())
                {
                    string? inner = Find(item, name);
                    if (inner != null) return inner;
                }
            }

            return null;
        }

        #endregion
    }
}
