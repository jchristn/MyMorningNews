namespace MyMorningNews.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Requests;
    using MyMorningNews.Sdk.Resources;
    using MyMorningNews.Sdk.Security;
    using MyMorningNews.Sdk.Serialization;

    /// <summary>
    /// Client for the My Morning News REST API. Sign in with LoginAsync (email and password) or LoginWithAccessKeyAsync, or
    /// set Token, AccessKey and SecretKey, or AdminApiKey directly. Resource groups (Topics, Runs, Briefings, Admin, and so on)
    /// hang off this object.
    /// Thread safety: requests may run concurrently. Changing credentials (Token, AccessKey, SecretKey, TenantId, AdminApiKey,
    /// UseSignedRequests) while requests are in flight affects only requests started afterward.
    /// </summary>
    public class MyMorningNewsClient : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Default server address.
        /// </summary>
        public static string DefaultBaseUrl { get; } = "http://127.0.0.1:12100";

        /// <summary>
        /// Server base URL without a trailing slash, for example http://127.0.0.1:12100.
        /// </summary>
        public string BaseUrl { get; }

        /// <summary>
        /// Bearer session token, sent as Authorization: Bearer. Set by LoginAsync and LoginWithAccessKeyAsync, cleared by
        /// LogoutAsync. Null when not signed in.
        /// </summary>
        public string? Token { get; set; } = null;

        /// <summary>
        /// Access key. With SecretKey (and no Token) it is sent on every request as x-access-key, or used to sign requests when
        /// UseSignedRequests is true. Null by default.
        /// </summary>
        public string? AccessKey { get; set; } = null;

        /// <summary>
        /// Secret key paired with AccessKey. Sent as x-secret-key unless UseSignedRequests is true, in which case it only keys the
        /// signature and never leaves the process. Null by default.
        /// </summary>
        public string? SecretKey { get; set; } = null;

        /// <summary>
        /// Tenant identifier. Sent as x-tenant-guid with access keys, signed requests, and the administration key; set from the
        /// login response. Null by default.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Server administration key, sent as x-api-key when set. It takes precedence over every other credential. Null by default.
        /// </summary>
        public string? AdminApiKey { get; set; } = null;

        /// <summary>
        /// Sign every access-key request with MMN1-HMAC-SHA256 instead of sending the secret. Requires AccessKey, SecretKey, and
        /// TenantId, and applies only when Token is not set. Default false.
        /// </summary>
        public bool UseSignedRequests { get; set; } = false;

        /// <summary>
        /// Per-request timeout. Default 5 minutes (model-backed calls such as refinement can be slow). Minimum 1 second,
        /// maximum 1 hour; values outside the range are clamped.
        /// </summary>
        public TimeSpan Timeout
        {
            get
            {
                return _Timeout;
            }
            set
            {
                long ticks = Math.Clamp(value.Ticks, TimeSpan.FromSeconds(1).Ticks, TimeSpan.FromHours(1).Ticks);
                _Timeout = TimeSpan.FromTicks(ticks);
            }
        }

        /// <summary>
        /// Topics: CRUD, lifecycle, run now, dimensions, constraints, recipients, and search providers.
        /// </summary>
        public TopicsResource Topics { get; }

        /// <summary>
        /// Topic refinement (wizard) sessions.
        /// </summary>
        public RefinementResource Refinement { get; }

        /// <summary>
        /// One-time research requests.
        /// </summary>
        public RequestsResource Requests { get; }

        /// <summary>
        /// Schedules for topics and editions.
        /// </summary>
        public SchedulesResource Schedules { get; }

        /// <summary>
        /// Research runs.
        /// </summary>
        public RunsResource Runs { get; }

        /// <summary>
        /// Findings.
        /// </summary>
        public FindingsResource Findings { get; }

        /// <summary>
        /// Fetched sources.
        /// </summary>
        public SourcesResource Sources { get; }

        /// <summary>
        /// Predictions and prediction reviews.
        /// </summary>
        public PredictionsResource Predictions { get; }

        /// <summary>
        /// Suggestions.
        /// </summary>
        public SuggestionsResource Suggestions { get; }

        /// <summary>
        /// Feedback.
        /// </summary>
        public FeedbackResource Feedback { get; }

        /// <summary>
        /// Learned preferences.
        /// </summary>
        public PreferencesResource Preferences { get; }

        /// <summary>
        /// Editions.
        /// </summary>
        public EditionsResource Editions { get; }

        /// <summary>
        /// Briefings and their Markdown, HTML, and audio files.
        /// </summary>
        public BriefingsResource Briefings { get; }

        /// <summary>
        /// Briefing templates.
        /// </summary>
        public TemplatesResource Templates { get; }

        /// <summary>
        /// Prompt templates and the prompt lab.
        /// </summary>
        public PromptsResource Prompts { get; }

        /// <summary>
        /// Audio episodes.
        /// </summary>
        public MediaResource Media { get; }

        /// <summary>
        /// Stored files.
        /// </summary>
        public ArtifactsResource Artifacts { get; }

        /// <summary>
        /// Private podcast feed tokens.
        /// </summary>
        public FeedTokensResource FeedTokens { get; }

        /// <summary>
        /// Email outbox, test email, previews, and preferences.
        /// </summary>
        public EmailResource Email { get; }

        /// <summary>
        /// Analytics reports.
        /// </summary>
        public AnalyticsResource Analytics { get; }

        /// <summary>
        /// Model usage and cost.
        /// </summary>
        public UsageResource Usage { get; }

        /// <summary>
        /// Model call records.
        /// </summary>
        public LlmCallsResource LlmCalls { get; }

        /// <summary>
        /// Model endpoints and task assignments.
        /// </summary>
        public ModelEndpointsResource ModelEndpoints { get; }

        /// <summary>
        /// Search providers and the fetch tester.
        /// </summary>
        public SearchProvidersResource SearchProviders { get; }

        /// <summary>
        /// Voice profiles.
        /// </summary>
        public VoicesResource Voices { get; }

        /// <summary>
        /// Vault credentials.
        /// </summary>
        public VaultResource Vault { get; }

        /// <summary>
        /// Push notification devices.
        /// </summary>
        public DevicesResource Devices { get; }

        /// <summary>
        /// In-app notifications and notification preferences.
        /// </summary>
        public NotificationsResource Notifications { get; }

        /// <summary>
        /// The caller's account: plan, promotional codes, usage meter, data export, and deletion, plus the public sign-up,
        /// email verification, and password reset routes.
        /// </summary>
        public AccountResource Account { get; }

        /// <summary>
        /// MCP server registry for agentic research: register, test, discover tools, and turn tools on or off.
        /// </summary>
        public McpServersResource McpServers { get; }

        /// <summary>
        /// Workspace setup: status, live checks, and storing a model API key.
        /// </summary>
        public SetupResource Setup { get; }

        /// <summary>
        /// Administration: control plane, promotional codes, plans, audit, request history, and settings.
        /// </summary>
        public AdminResource Admin { get; }

        #endregion

        #region Private-Members

        private readonly HttpClient _Http;
        private readonly bool _OwnsHttpClient;
        private TimeSpan _Timeout = TimeSpan.FromMinutes(5);
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="baseUrl">Server base URL. Default http://127.0.0.1:12100.</param>
        /// <param name="httpClient">HttpClient to use, or null to create one. A supplied client is not disposed by this object,
        /// and its own Timeout should be at least this client's Timeout.</param>
        /// <exception cref="ArgumentNullException">baseUrl is null or empty.</exception>
        /// <exception cref="ArgumentException">baseUrl is not an absolute http or https URL.</exception>
        public MyMorningNewsClient(string baseUrl = "http://127.0.0.1:12100", HttpClient? httpClient = null)
        {
            if (String.IsNullOrWhiteSpace(baseUrl)) throw new ArgumentNullException(nameof(baseUrl));
            string trimmed = baseUrl.Trim().TrimEnd('/');
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? parsed) || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
                throw new ArgumentException("baseUrl must be an absolute http or https URL.", nameof(baseUrl));
            BaseUrl = trimmed;
            if (httpClient != null)
            {
                _Http = httpClient;
                _OwnsHttpClient = false;
            }
            else
            {
                _Http = new HttpClient();
                _Http.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
                _OwnsHttpClient = true;
            }

            Topics = new TopicsResource(this);
            Refinement = new RefinementResource(this);
            Requests = new RequestsResource(this);
            Schedules = new SchedulesResource(this);
            Runs = new RunsResource(this);
            Findings = new FindingsResource(this);
            Sources = new SourcesResource(this);
            Predictions = new PredictionsResource(this);
            Suggestions = new SuggestionsResource(this);
            Feedback = new FeedbackResource(this);
            Preferences = new PreferencesResource(this);
            Editions = new EditionsResource(this);
            Briefings = new BriefingsResource(this);
            Templates = new TemplatesResource(this);
            Prompts = new PromptsResource(this);
            Media = new MediaResource(this);
            Artifacts = new ArtifactsResource(this);
            FeedTokens = new FeedTokensResource(this);
            Email = new EmailResource(this);
            Analytics = new AnalyticsResource(this);
            Usage = new UsageResource(this);
            LlmCalls = new LlmCallsResource(this);
            ModelEndpoints = new ModelEndpointsResource(this);
            SearchProviders = new SearchProvidersResource(this);
            Voices = new VoicesResource(this);
            Vault = new VaultResource(this);
            Devices = new DevicesResource(this);
            Notifications = new NotificationsResource(this);
            Account = new AccountResource(this);
            McpServers = new McpServersResource(this);
            Setup = new SetupResource(this);
            Admin = new AdminResource(this);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Sign in with an email and password. Stores the session token and tenant on this client.
        /// </summary>
        /// <param name="email">Email.</param>
        /// <param name="password">Password.</param>
        /// <param name="tenantId">Tenant, or null to let the server pick the only matching tenant.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Token response.</returns>
        /// <exception cref="ArgumentNullException">email or password is null or empty.</exception>
        /// <exception cref="TenantSelectionRequiredException">The email exists in several tenants; pass tenantId.</exception>
        /// <exception cref="EmailNotVerifiedException">The account signed up publicly and its email is not confirmed yet (403).</exception>
        /// <exception cref="MyMorningNewsException">The server rejected the login.</exception>
        public async Task<TokenResponse> LoginAsync(string email, string password, string? tenantId = null, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(email)) throw new ArgumentNullException(nameof(email));
            if (String.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password));
            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "x-email", email.Trim() },
                { "x-password", password }
            };
            if (!String.IsNullOrWhiteSpace(tenantId)) headers["x-tenant-guid"] = tenantId.Trim();
            return await IssueTokenAsync(headers, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Exchange an access key and secret for a session token. Stores the token and tenant on this client.
        /// </summary>
        /// <param name="accessKey">Access key.</param>
        /// <param name="secretKey">Secret key.</param>
        /// <param name="tenantId">Tenant, or null to use TenantId.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Token response.</returns>
        /// <exception cref="ArgumentNullException">accessKey or secretKey is null or empty, or no tenant is known.</exception>
        /// <exception cref="MyMorningNewsException">The server rejected the credential.</exception>
        public async Task<TokenResponse> LoginWithAccessKeyAsync(string accessKey, string secretKey, string? tenantId = null, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(accessKey)) throw new ArgumentNullException(nameof(accessKey));
            if (String.IsNullOrEmpty(secretKey)) throw new ArgumentNullException(nameof(secretKey));
            string? tenant = !String.IsNullOrWhiteSpace(tenantId) ? tenantId.Trim() : TenantId;
            if (String.IsNullOrEmpty(tenant)) throw new ArgumentNullException(nameof(tenantId), "Access key login needs a tenant.");
            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "x-access-key", accessKey.Trim() },
                { "x-secret-key", secretKey },
                { "x-tenant-guid", tenant }
            };
            return await IssueTokenAsync(headers, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Refresh the current session token. Stores the new token.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Token response.</returns>
        /// <exception cref="MyMorningNewsException">The server rejected the refresh.</exception>
        public async Task<TokenResponse> RefreshAsync(CancellationToken token = default)
        {
            TokenResponse response = await SendAsync<TokenResponse>(HttpMethod.Post, "/v1.0/token/refresh", null, null, token).ConfigureAwait(false);
            Token = response.Token;
            return response;
        }

        /// <summary>
        /// Revoke the current session token and clear Token.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="MyMorningNewsException">The server rejected the request.</exception>
        public async Task LogoutAsync(CancellationToken token = default)
        {
            await SendAsync(HttpMethod.Delete, "/v1.0/token", null, null, token).ConfigureAwait(false);
            Token = null;
        }

        /// <summary>
        /// Validate the current credentials (GET /v1.0/token).
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Principal details.</returns>
        /// <exception cref="MyMorningNewsException">The credentials are not valid.</exception>
        public Task<TokenDetailsResponse> ValidateTokenAsync(CancellationToken token = default)
        {
            return SendAsync<TokenDetailsResponse>(HttpMethod.Get, "/v1.0/token", null, null, token);
        }

        /// <summary>
        /// Token and principal details, including issue and expiry times for session tokens.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Details.</returns>
        /// <exception cref="MyMorningNewsException">The credentials are not valid.</exception>
        public Task<TokenDetailsResponse> GetTokenDetailsAsync(CancellationToken token = default)
        {
            return SendAsync<TokenDetailsResponse>(HttpMethod.Get, "/v1.0/token/details", null, null, token);
        }

        /// <summary>
        /// Server health (anonymous). An unhealthy server (HTTP 503) is returned as a response with Status Unhealthy.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Health.</returns>
        /// <exception cref="MyMorningNewsException">The server answered with another error.</exception>
        public async Task<HealthResponse> GetHealthAsync(CancellationToken token = default)
        {
            try
            {
                return await SendAsync<HealthResponse>(HttpMethod.Get, "/v1.0/api/health", null, null, token, false).ConfigureAwait(false);
            }
            catch (MyMorningNewsException ex) when (ex.StatusCode == 503 && !String.IsNullOrWhiteSpace(ex.ResponseBody))
            {
                HealthResponse? health = TryDeserialize<HealthResponse>(ex.ResponseBody);
                if (health == null) throw;
                return health;
            }
        }

        /// <summary>
        /// Public client configuration: feature flags, minimum app version, and legal links (anonymous).
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Configuration.</returns>
        /// <exception cref="MyMorningNewsException">The request failed.</exception>
        public Task<ClientConfigResponse> GetClientConfigAsync(CancellationToken token = default)
        {
            return SendAsync<ClientConfigResponse>(HttpMethod.Get, "/v1.0/api/client-config", null, null, token, false);
        }

        /// <summary>
        /// Server information (version, schema, runtime, and bundled tool links).
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Information.</returns>
        /// <exception cref="MyMorningNewsException">The request failed.</exception>
        public Task<ServerInfoResponse> GetServerInfoAsync(CancellationToken token = default)
        {
            return SendAsync<ServerInfoResponse>(HttpMethod.Get, "/v1.0/api/server-info", null, null, token);
        }

        /// <summary>
        /// The signed-in user's profile.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Profile.</returns>
        /// <exception cref="MyMorningNewsException">The request failed (400 for the administration key, which has no profile).</exception>
        public Task<UserResponse> GetMeAsync(CancellationToken token = default)
        {
            return SendAsync<UserResponse>(HttpMethod.Get, "/v1.0/api/me", null, null, token);
        }

        /// <summary>
        /// Update the signed-in user's profile.
        /// </summary>
        /// <param name="request">Changes; null fields are left alone.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated profile.</returns>
        /// <exception cref="ArgumentNullException">request is null.</exception>
        /// <exception cref="MyMorningNewsException">The request failed.</exception>
        public Task<UserResponse> UpdateMeAsync(SelfUpdateRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return SendAsync<UserResponse>(HttpMethod.Put, "/v1.0/api/me", null, request, token);
        }

        /// <summary>
        /// Change the signed-in user's password.
        /// </summary>
        /// <param name="request">Current and new password.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">request is null.</exception>
        /// <exception cref="MyMorningNewsException">The request failed.</exception>
        public Task ChangePasswordAsync(PasswordChangeRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return SendAsync(HttpMethod.Put, "/v1.0/api/me/password", null, request, token);
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Send a request and deserialize the JSON response.
        /// </summary>
        /// <typeparam name="T">Response type.</typeparam>
        /// <param name="method">Method.</param>
        /// <param name="path">Path beginning with a slash.</param>
        /// <param name="query">Query values, or null.</param>
        /// <param name="body">JSON body, or null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <param name="authenticate">Whether to attach credentials.</param>
        /// <returns>Response.</returns>
        internal async Task<T> SendAsync<T>(HttpMethod method, string path, IDictionary<string, string>? query, object? body, CancellationToken token, bool authenticate = true)
        {
            string text = await SendCoreAsync(method, path, query, body, null, authenticate, null, ReadTextAsync, token).ConfigureAwait(false);
            if (String.IsNullOrWhiteSpace(text)) throw new MyMorningNewsException(0, "EmptyResponse", "The server returned no body for " + method + " " + path + ".", null, null, text);
            try
            {
                T? value = MyMorningNewsSerializer.Deserialize<T>(text);
                if (value == null) throw new MyMorningNewsException(0, "EmptyResponse", "The server returned null for " + method + " " + path + ".", null, null, text);
                return value;
            }
            catch (JsonException ex)
            {
                throw new MyMorningNewsException(0, "InvalidResponse", "The response to " + method + " " + path + " could not be read: " + ex.Message, null, null, text, ex);
            }
        }

        /// <summary>
        /// Send a request and ignore the response body.
        /// </summary>
        /// <param name="method">Method.</param>
        /// <param name="path">Path.</param>
        /// <param name="query">Query values, or null.</param>
        /// <param name="body">JSON body, or null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        internal async Task SendAsync(HttpMethod method, string path, IDictionary<string, string>? query, object? body, CancellationToken token)
        {
            await SendCoreAsync(method, path, query, body, null, true, null, ReadTextAsync, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Send a request and return the body as text.
        /// </summary>
        /// <param name="method">Method.</param>
        /// <param name="path">Path.</param>
        /// <param name="query">Query values, or null.</param>
        /// <param name="body">JSON body, or null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Body text.</returns>
        internal Task<string> SendTextAsync(HttpMethod method, string path, IDictionary<string, string>? query, object? body, CancellationToken token)
        {
            return SendCoreAsync(method, path, query, body, null, true, null, ReadTextAsync, token);
        }

        /// <summary>
        /// Download a binary response, optionally a byte range, into memory or a stream.
        /// </summary>
        /// <param name="method">Method.</param>
        /// <param name="path">Path.</param>
        /// <param name="query">Query values, or null.</param>
        /// <param name="body">JSON body, or null.</param>
        /// <param name="range">Byte range, or null for the whole file.</param>
        /// <param name="destination">Stream to write to, or null to buffer into DownloadResult.Data.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Download result.</returns>
        internal Task<DownloadResult> DownloadAsync(HttpMethod method, string path, IDictionary<string, string>? query, object? body, ByteRange? range, Stream? destination, CancellationToken token)
        {
            return SendCoreAsync(method, path, query, body, range, true, null, (response, ct) => ReadDownloadAsync(response, destination, ct), token);
        }

        /// <summary>
        /// Open a streaming GET (server-sent events) with the client's credentials. The client timeout applies only until
        /// the response headers arrive. The caller disposes the response.
        /// </summary>
        /// <param name="path">Path.</param>
        /// <param name="token">Cancellation token; cancel it to close the stream.</param>
        /// <returns>Response positioned at the start of the stream.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        internal async Task<HttpResponseMessage> OpenStreamAsync(string path, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(path);
            ObjectDisposedException.ThrowIf(_Disposed, this);
            Uri uri = new Uri(BaseUrl + path);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            ApplyCredentials(request, uri, null);
            HttpResponseMessage response;
            using (CancellationTokenSource headers = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                headers.CancelAfter(_Timeout);
                try
                {
                    response = await _Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, headers.Token).ConfigureAwait(false);
                }
                finally
                {
                    request.Dispose();
                }
            }

            if (!response.IsSuccessStatusCode)
            {
                using (response)
                {
                    throw await ToExceptionAsync(response, token).ConfigureAwait(false);
                }
            }

            return response;
        }

        /// <summary>
        /// Escape a path segment.
        /// </summary>
        /// <param name="value">Segment.</param>
        /// <param name="name">Argument name for errors.</param>
        /// <returns>Escaped segment.</returns>
        /// <exception cref="ArgumentNullException">value is null or empty.</exception>
        internal static string Segment(string value, string name)
        {
            if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(name);
            return Uri.EscapeDataString(value.Trim());
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Dispose resources.
        /// </summary>
        /// <param name="disposing">True when called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing && _OwnsHttpClient) _Http.Dispose();
            _Disposed = true;
        }

        #endregion

        #region Private-Methods

        private async Task<TokenResponse> IssueTokenAsync(Dictionary<string, string> headers, CancellationToken token)
        {
            string text;
            try
            {
                text = await SendCoreAsync(HttpMethod.Post, "/v1.0/token", null, null, null, false, headers, ReadTextAsync, token).ConfigureAwait(false);
            }
            catch (MyMorningNewsException ex) when (ex.StatusCode == 409 && !String.IsNullOrWhiteSpace(ex.ResponseBody))
            {
                TenantSelectionResponse? choose = TryDeserialize<TenantSelectionResponse>(ex.ResponseBody);
                if (choose == null || choose.Tenants.Count == 0) throw;
                throw new TenantSelectionRequiredException(choose.Message, choose.Tenants, ex.ResponseBody);
            }

            TokenResponse? response = TryDeserialize<TokenResponse>(text);
            if (response == null || String.IsNullOrEmpty(response.Token)) throw new MyMorningNewsException(0, "InvalidResponse", "The token response could not be read.", null, null, text);
            Token = response.Token;
            TenantId = response.TenantId;
            return response;
        }

        private async Task<TResult> SendCoreAsync<TResult>(
            HttpMethod method,
            string path,
            IDictionary<string, string>? query,
            object? body,
            ByteRange? range,
            bool authenticate,
            Dictionary<string, string>? extraHeaders,
            Func<HttpResponseMessage, CancellationToken, Task<TResult>> reader,
            CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(method);
            ArgumentNullException.ThrowIfNull(path);
            ArgumentNullException.ThrowIfNull(reader);
            ObjectDisposedException.ThrowIf(_Disposed, this);
            token.ThrowIfCancellationRequested();

            Uri uri = new Uri(BaseUrl + path + BuildQuery(query));
            byte[]? payload = body == null ? null : Encoding.UTF8.GetBytes(MyMorningNewsSerializer.Serialize(body));

            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(_Timeout);
                using (HttpRequestMessage request = new HttpRequestMessage(method, uri))
                {
                    if (payload != null)
                    {
                        ByteArrayContent content = new ByteArrayContent(payload);
                        content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
                        request.Content = content;
                    }

                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    if (range != null) request.Headers.TryAddWithoutValidation("Range", range.ToHeaderValue());
                    if (extraHeaders != null)
                        foreach (KeyValuePair<string, string> h in extraHeaders) request.Headers.TryAddWithoutValidation(h.Key, h.Value);
                    if (authenticate) ApplyCredentials(request, uri, payload);

                    try
                    {
                        using (HttpResponseMessage response = await _Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                        {
                            if (!response.IsSuccessStatusCode) throw await ToExceptionAsync(response, timeout.Token).ConfigureAwait(false);
                            return await reader(response, timeout.Token).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException ex) when (!token.IsCancellationRequested && timeout.IsCancellationRequested)
                    {
                        throw new TimeoutException(method + " " + path + " did not finish within " + _Timeout.TotalSeconds.ToString(CultureInfo.InvariantCulture) + " seconds.", ex);
                    }
                }
            }
        }

        private void ApplyCredentials(HttpRequestMessage request, Uri uri, byte[]? payload)
        {
            string? adminKey = AdminApiKey;
            string? bearer = Token;
            string? accessKey = AccessKey;
            string? secretKey = SecretKey;
            string? tenant = TenantId;

            if (!String.IsNullOrEmpty(adminKey))
            {
                request.Headers.TryAddWithoutValidation("x-api-key", adminKey);
                if (!String.IsNullOrEmpty(tenant)) request.Headers.TryAddWithoutValidation("x-tenant-guid", tenant);
                return;
            }

            if (!String.IsNullOrEmpty(bearer))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
                return;
            }

            if (String.IsNullOrEmpty(accessKey) || String.IsNullOrEmpty(secretKey)) return;
            request.Headers.TryAddWithoutValidation("x-access-key", accessKey);
            if (!String.IsNullOrEmpty(tenant)) request.Headers.TryAddWithoutValidation("x-tenant-guid", tenant);
            if (!UseSignedRequests)
            {
                request.Headers.TryAddWithoutValidation("x-secret-key", secretKey);
                return;
            }

            if (String.IsNullOrEmpty(tenant)) throw new InvalidOperationException("Signed requests need TenantId.");
            string date = RequestSigner.FormatDate(DateTime.UtcNow);
            string nonce = RequestSigner.CreateNonce();
            string signature = RequestSigner.SignRequest(secretKey, request.Method.Method, uri.PathAndQuery, tenant, date, nonce, payload);
            request.Headers.TryAddWithoutValidation("x-date", date);
            request.Headers.TryAddWithoutValidation("x-nonce", nonce);
            request.Headers.TryAddWithoutValidation("x-signature", signature);
        }

        private static string BuildQuery(IDictionary<string, string>? query)
        {
            if (query == null || query.Count == 0) return String.Empty;
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, string> kv in query.Where(kv => !String.IsNullOrEmpty(kv.Key) && kv.Value != null))
            {
                sb.Append(sb.Length == 0 ? '?' : '&');
                sb.Append(Uri.EscapeDataString(kv.Key)).Append('=').Append(Uri.EscapeDataString(kv.Value));
            }

            return sb.ToString();
        }

        private static async Task<string> ReadTextAsync(HttpResponseMessage response, CancellationToken token)
        {
            if (response.Content == null) return String.Empty;
            return await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
        }

        private static async Task<DownloadResult> ReadDownloadAsync(HttpResponseMessage response, Stream? destination, CancellationToken token)
        {
            DownloadResult result = new DownloadResult
            {
                StatusCode = (int)response.StatusCode,
                ContentType = response.Content?.Headers.ContentType?.MediaType,
                ETag = response.Headers.ETag?.Tag
            };

            ContentDispositionHeaderValue? disposition = response.Content?.Headers.ContentDisposition;
            string? name = disposition?.FileNameStar ?? disposition?.FileName;
            result.FileName = String.IsNullOrEmpty(name) ? null : name.Trim('"');
            ContentRangeHeaderValue? contentRange = response.Content?.Headers.ContentRange;
            if (contentRange != null)
            {
                result.ContentRange = contentRange.ToString();
                result.TotalLength = contentRange.Length;
            }

            if (response.Content == null) return result;
            using (Stream source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            {
                if (destination != null)
                {
                    long before = destination.CanSeek ? destination.Position : 0;
                    long copied = 0;
                    byte[] buffer = new byte[81920];
                    int read;
                    while ((read = await source.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
                    {
                        await destination.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
                        copied += read;
                    }

                    result.Length = destination.CanSeek ? destination.Position - before : copied;
                    return result;
                }

                using (MemoryStream ms = new MemoryStream())
                {
                    await source.CopyToAsync(ms, token).ConfigureAwait(false);
                    result.Data = ms.ToArray();
                    result.Length = result.Data.Length;
                }
            }

            return result;
        }

        private static async Task<MyMorningNewsException> ToExceptionAsync(HttpResponseMessage response, CancellationToken token)
        {
            int status = (int)response.StatusCode;
            string? text = null;
            try
            {
                if (response.Content != null) text = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                text = null;
            }

            string? requestId = response.Headers.TryGetValues("X-Request-Id", out IEnumerable<string>? ids) ? ids.FirstOrDefault() : null;
            string? error = null;
            string message = String.Empty;
            Dictionary<string, string> details = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!String.IsNullOrWhiteSpace(text))
            {
                try
                {
                    using (JsonDocument doc = JsonDocument.Parse(text))
                    {
                        if (doc.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (JsonProperty p in doc.RootElement.EnumerateObject())
                            {
                                string key = p.Name;
                                if (String.Equals(key, "error", StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.String) error = p.Value.GetString();
                                else if (String.Equals(key, "message", StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.String) message = p.Value.GetString() ?? String.Empty;
                                else if (String.Equals(key, "requestId", StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.String) requestId = p.Value.GetString() ?? requestId;
                                else if (String.Equals(key, "details", StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.Object)
                                {
                                    foreach (JsonProperty d in p.Value.EnumerateObject())
                                        details[d.Name] = d.Value.ValueKind == JsonValueKind.String ? d.Value.GetString() ?? String.Empty : d.Value.GetRawText();
                                }
                                else if (p.Value.ValueKind == JsonValueKind.String && !details.ContainsKey(key))
                                {
                                    details[key] = p.Value.GetString() ?? String.Empty;
                                }
                            }
                        }
                    }
                }
                catch (JsonException)
                {
                    message = text.Length > 500 ? text.Substring(0, 500) : text;
                }
            }

            if (String.IsNullOrWhiteSpace(message)) message = "The server returned HTTP " + status + " " + response.ReasonPhrase + ".";
            if (status == 402 && String.Equals(error, "CapReached", StringComparison.Ordinal)) return new CapReachedException(message, requestId, details, text);
            if (status == 403 && String.Equals(error, "EmailNotVerified", StringComparison.Ordinal)) return new EmailNotVerifiedException(message, requestId, details, text);
            if (status == 409 && String.Equals(error, "SetupRequired", StringComparison.Ordinal)) return new SetupRequiredException(message, requestId, details, text);
            return new MyMorningNewsException(status, error, message, requestId, details, text);
        }

        private static T? TryDeserialize<T>(string? text) where T : class
        {
            if (String.IsNullOrWhiteSpace(text)) return null;
            try
            {
                return MyMorningNewsSerializer.Deserialize<T>(text);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        #endregion
    }
}
