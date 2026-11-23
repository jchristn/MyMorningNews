namespace MyMorningNews.Server
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core;
    using MyMorningNews.Core.Briefings;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Database.Postgresql;
    using MyMorningNews.Core.Fetch;
    using MyMorningNews.Core.Pipeline;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Scheduling;
    using MyMorningNews.Core.Search;
    using MyMorningNews.Core.Security;
    using MyMorningNews.Core.Serialization;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Core.Storage;
    using MyMorningNews.Server.Routes;
    using MyMorningNews.Server.Services;
    using MyMorningNews.Server.Workers;
    using SyslogLogging;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Composed server host: database, services, Watson webserver, routing hooks, route registrars, and workers.
    /// Used by the bootstrapper and by in-process tests.
    /// Thread safety: start and stop from a single thread.
    /// </summary>
    public class MyMorningNewsServer : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Shared dependencies, available after <see cref="StartAsync"/>.
        /// </summary>
        public ServerContext Context
        {
            get { return _Context ?? throw new InvalidOperationException("The server has not been started."); }
        }

        /// <summary>
        /// Watson webserver, available after <see cref="StartAsync"/>.
        /// </summary>
        public Webserver Server
        {
            get { return _Webserver ?? throw new InvalidOperationException("The server has not been started."); }
        }

        /// <summary>
        /// Lifetime.
        /// </summary>
        public ServerLifetime Lifetime { get; }

        /// <summary>
        /// Number of attempts to reach the database at startup. Default 30. Minimum 1, maximum 600.
        /// </summary>
        public int DatabaseConnectAttempts
        {
            get
            {
                return _DatabaseConnectAttempts;
            }
            set
            {
                _DatabaseConnectAttempts = Math.Clamp(value, 1, 600);
            }
        }

        /// <summary>
        /// Whether background workers start with the server. Default true; tests may disable them.
        /// </summary>
        public bool StartWorkers { get; set; } = true;

        /// <summary>
        /// S3-compatible store to use instead of one built from settings (tests). Null builds from settings.
        /// </summary>
        public IArtifactStore? ArtifactStoreOverride { get; set; } = null;

        /// <summary>
        /// Speech provider to use instead of the OpenAI-compatible one (tests). Null uses the default.
        /// </summary>
        public MyMorningNews.Core.Audio.ITtsProvider? TtsOverride { get; set; } = null;

        #endregion

        #region Private-Members

        private readonly SettingsService _Settings;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[MyMorningNewsServer] ";
        private ServerContext? _Context = null;
        private Webserver? _Webserver = null;
        private DatabaseDriverBase? _Database = null;
        private RequestHistoryCaptureService? _Capture = null;
        private ModelGateway? _Models = null;
        private readonly List<Task> _Workers = new List<Task>();
        private int _DatabaseConnectAttempts = 30;
        private PushService? _Push = null;
        private RefinementService? _Refinement = null;
        private SearchService? _Search = null;
        private FetchService? _Fetch = null;
        private ArtifactService? _Artifacts = null;
        private MyMorningNews.Core.Audio.AudioService? _Audio = null;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Settings service.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="lifetime">Lifetime; created when null.</param>
        /// <exception cref="ArgumentNullException">settings or logging is null.</exception>
        public MyMorningNewsServer(SettingsService settings, LoggingModule logging, ServerLifetime? lifetime = null)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(logging);
            _Settings = settings;
            _Logging = logging;
            Lifetime = lifetime ?? new ServerLifetime();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Initialize the database, seed, wire routes, start listening, and start workers.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="InvalidOperationException">Request types lack permission mappings.</exception>
        public async Task StartAsync(CancellationToken token = default)
        {
            OperationScopeMap.Validate();
            ServerSettings settings = _Settings.Current;

            _Database = await ConnectDatabaseAsync(settings.Database, token).ConfigureAwait(false);
            await new SeedService(_Database, _Logging).EnsureAsync(token).ConfigureAwait(false);

            AuditService audit = new AuditService(_Database, _Logging);
            AuthenticationService authentication = new AuthenticationService(_Database, settings.Auth, audit);
            AuthorizationService authorization = new AuthorizationService(_Database);
            VaultService vault = new VaultService(_Database, settings.Auth.SigningKey);
            PromptService prompts = new PromptService(_Database);
            _Models = new ModelGateway(_Database, vault, settings, _Logging);
            TenantDefaultsService tenantDefaults = new TenantDefaultsService(_Database, prompts, vault, settings.Models, _Logging);
            _Search = new SearchService(_Database, vault, settings, _Logging);
            _Fetch = new FetchService(settings, _Logging);
            tenantDefaults.Search = _Search;
            _Push = new PushService(_Database, settings, _Logging);
            _Refinement = new RefinementService(_Database, prompts, _Models, _Logging);
            ResearchRequestService researchRequests = new ResearchRequestService(_Database, _Refinement);
            SchedulingService scheduling = new SchedulingService(_Database, settings, _Logging);
            _Context = new ServerContext(_Settings, _Database, _Logging, authentication, authorization, audit, Lifetime, vault, prompts, _Models, tenantDefaults, _Push, _Refinement, researchRequests, scheduling, _Search!, _Fetch!);
            _Artifacts = new ArtifactService(_Database, settings, _Logging, ArtifactStoreOverride);
            BriefingTemplateService templates = new BriefingTemplateService(_Database);
            BriefingComposer composer = new BriefingComposer(_Database, _Models, prompts, _Artifacts, settings, _Logging);
            BriefingService briefings = new BriefingService(_Database, composer, templates, scheduling, _Artifacts, settings, _Logging);
            scheduling.EditionTrigger = briefings;
            _Audio = new MyMorningNews.Core.Audio.AudioService(_Database, _Models, prompts, _Artifacts, vault, settings, _Logging, TtsOverride);
            briefings.Audio = _Audio;
            MyMorningNews.Core.Email.EmailService email = new MyMorningNews.Core.Email.EmailService(_Database, _Models, prompts, settings, _Logging);
            NotificationService notifications = new NotificationService(_Database, email, _Push, settings, _Logging);
            briefings.Notifications = notifications;
            _Audio.Notifications = notifications;
            tenantDefaults.Audio = _Audio;
            tenantDefaults.Briefings = briefings;
            await tenantDefaults.EnsureAllAsync(token).ConfigureAwait(false);
            _Context.Artifacts = _Artifacts;
            _Context.Briefings = briefings;
            _Context.Audio = _Audio;
            _Context.Email = email;
            _Context.Notifications = notifications;
            int keptPush = await notifications.Preferences.KeepEarlierPushForDevicesAsync(token).ConfigureAwait(false);
            if (keptPush > 0) _Logging.Info(_Header + "kept push on for " + keptPush + " users with devices registered before per-kind notification preferences");
            _Context.Feedback = new MyMorningNews.Core.Feedback.FeedbackService(_Database);
            MyMorningNews.Core.Feedback.LearningService learning = new MyMorningNews.Core.Feedback.LearningService(_Database, _Models, prompts, settings, _Logging);
            _Context.Learning = learning;
            _Context.Analytics = new MyMorningNews.Core.Analytics.AnalyticsService(_Database, settings);
            _Context.RunAnalytics = new MyMorningNews.Core.Analytics.RunAnalyticsService(_Database);
            _Context.Entitlements = new MyMorningNews.Core.Entitlements.EntitlementService(_Database, settings);
            int plans = await _Context.Entitlements.EnsureDefaultsAsync(token).ConfigureAwait(false);
            if (plans > 0) _Logging.Info(_Header + "seeded " + plans + " plans");
            _Context.PromoCodes = new MyMorningNews.Core.Entitlements.PromoCodeService(_Database);
            MyMorningNews.Core.Security.NetworkGuard.IsEnabled = () => _Context.SettingsService.Current.Hosting.RestrictPrivateNetworks;
            MyMorningNews.Core.Security.NetworkGuard.AllowedHosts = () => _Context.SettingsService.Current.Hosting.AllowedPrivateHosts;
            tenantDefaults.UsePlatformCredentials = () => _Context.SettingsService.Current.Hosting.UsePlatformCredentials;
            _Context.Accounts = new AccountDataService(_Database, briefings, _Artifacts);
            _Context.PredictionNotices = new PredictionNoticeService(_Database, notifications, _Logging);
            _Context.RunCosts = new RunCostService(_Database);
            _Context.Setup = new SetupService(_Database, _Models!, _Search!, _Context.Vault, () => _Context.SettingsService.Current, _Logging) { Audio = _Audio };
            _Context.Account = new AccountService(_Database, settings, email, tenantDefaults, _Context.PromoCodes, _Context.Accounts);
            MyMorningNews.Core.Entitlements.EntitlementService entitlements = _Context.Entitlements;
            AccountService account = _Context.Account;
            authentication.SignInGate = (user, t) => Task.FromResult(account.NeedsVerification(user) ? "Confirm your email address first: open the link we sent you, or ask for a new one." : (string?)null);
            authentication.OnSignIn = (user, t) => account.CancelDeletionAsync(user, t);
            _Models.PreCallCheck = (req, t) => String.IsNullOrEmpty(req.UserId) ? Task.CompletedTask : entitlements.CheckCostAsync(req.TenantId, req.UserId, t);
            scheduling.RunGate = async (tenantId, userId, t) =>
            {
                await entitlements.CheckCostAsync(tenantId, userId, t).ConfigureAwait(false);
                DateTime today = DateTime.UtcNow.Date;
                long runsToday = await _Database.Runs.CountAsync(new MyMorningNews.Core.Requests.RunFilter { TenantId = tenantId, UserId = userId, IsDryRun = false, FromUtc = today }, t).ConfigureAwait(false);
                await entitlements.CheckCountAsync(tenantId, userId, "runs.daily.max", runsToday, "runs a day", today.AddDays(1), t).ConfigureAwait(false);
            };
            scheduling.OnCapReached = async (tenantId, userId, topicName, cap, t) =>
            {
                await notifications.NotifyAsync(tenantId, userId, MyMorningNews.Core.Enums.NotificationKindEnum.CapReached, new NotificationContext { Cap = cap, TopicName = topicName }, t).ConfigureAwait(false);
            };
            scheduling.OnRunFailed = async (run, t) =>
            {
                await notifications.NotifyAsync(run.TenantId, run.UserId, MyMorningNews.Core.Enums.NotificationKindEnum.RunFailed, new NotificationContext { Run = run }, t).ConfigureAwait(false);
            };
            scheduling.MinimumIntervalForUser = async (tenantId, userId, t) =>
            {
                decimal? limit = MyMorningNews.Core.Entitlements.EntitlementService.Limit(await entitlements.GetEffectiveAsync(tenantId, userId, t).ConfigureAwait(false), "schedule.minIntervalMinutes");
                return limit.HasValue ? (int)limit.Value : null;
            };
            _Context.Mcp = new MyMorningNews.Core.Mcp.McpClientService(vault);
            PipelineServices pipelineServices = new PipelineServices(_Database, _Models, prompts, _Search!, _Fetch!, settings, _Logging) { Briefings = briefings, Email = email, Notifications = notifications, Learning = learning, Mcp = _Context.Mcp };
            _Context.RunExecutor = new ResearchPipeline(pipelineServices);

            _Capture = new RequestHistoryCaptureService(_Database, settings.RequestHistory, _Logging);
            RequestPipeline pipeline = new RequestPipeline(_Context, _Capture);

            WebserverSettings webserverSettings = new WebserverSettings(settings.Rest.Hostname, settings.Rest.Port, settings.Rest.Ssl);
            webserverSettings.Telemetry.Enable = settings.Telemetry.Enabled;
            webserverSettings.Telemetry.Prometheus.Enable = false;
            _Webserver = new Webserver(webserverSettings, DefaultRouteAsync);
            // Watson's default response headers include request-style ones (Host with the bind address, Accept,
            // Accept-Language, Accept-Charset) that mean nothing in a response and leak the internal address.
            foreach (string requestHeader in new[] { "Host", "Accept", "Accept-Language", "Accept-Charset" }) _Webserver.Settings.Headers.DefaultHeaders.Remove(requestHeader);
            _Webserver.UseOpenApi(openApi =>
            {
                openApi.Info.Title = Constants.ProductName;
                openApi.Info.Version = Constants.ProductVersion;
                openApi.Info.Description = "REST API for My Morning News. Authenticate with POST /v1.0/token, then send Authorization: Bearer <token>.";
                openApi.Schemas = OpenApiSchemaFactory.Components;
            });

            _Webserver.Routes.Preflight = pipeline.PreflightAsync;
            _Webserver.Routes.PreRouting = pipeline.PreRoutingAsync;
            _Webserver.Routes.AuthenticateRequest = pipeline.AuthenticateAsync;
            _Webserver.Routes.PostRouting = pipeline.PostRoutingAsync;
            _Webserver.Routes.Exception = pipeline.ExceptionAsync;

            foreach (RouteBase registrar in Registrars(_Context)) registrar.Register(_Webserver);

            _Webserver.Start(token);
            _Logging.Info(_Header + "listening on " + (settings.Rest.Ssl ? "https" : "http") + "://" + settings.Rest.Hostname + ":" + settings.Rest.Port);

            if (StartWorkers)
            {
                RetentionWorker retention = new RetentionWorker(_Context);
                _Workers.Add(Task.Run(() => retention.RunAsync(Lifetime.Token)));
                BriefingWorker briefingWorker = new BriefingWorker(_Context);
                _Workers.Add(Task.Run(() => briefingWorker.RunAsync(Lifetime.Token)));
                MediaWorker mediaWorker = new MediaWorker(_Context);
                _Workers.Add(Task.Run(() => mediaWorker.RunAsync(Lifetime.Token)));
                EmailDeliveryWorker emailWorker = new EmailDeliveryWorker(_Context);
                _Workers.Add(Task.Run(() => emailWorker.RunAsync(Lifetime.Token)));
                LearningWorker learningWorker = new LearningWorker(_Context);
                _Workers.Add(Task.Run(() => learningWorker.RunAsync(Lifetime.Token)));
                if (settings.Scheduler.Enabled)
                {
                    SchedulerWorker scheduler = new SchedulerWorker(_Context);
                    RunWorker runs = new RunWorker(_Context);
                    _Workers.Add(Task.Run(() => scheduler.RunAsync(Lifetime.Token)));
                    _Workers.Add(Task.Run(() => runs.RunAsync(Lifetime.Token)));
                }
            }
        }

        /// <summary>
        /// Stop listening and wait briefly for workers to finish.
        /// </summary>
        public void Stop()
        {
            Lifetime.RequestShutdown();
            if (_Webserver != null && _Webserver.IsListening) _Webserver.Stop();
            try
            {
                Task.WaitAll(_Workers.ToArray(), TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
            }
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
            if (disposing)
            {
                Stop();
                _Webserver?.Dispose();
                _Capture?.Dispose();
                _Push?.Dispose();
                _Context?.Setup?.Dispose();
                _Context?.Scheduling?.Dispose();
                _Refinement?.Dispose();
                _Search?.Dispose();
                _Fetch?.Dispose();
                _Audio?.Dispose();
                _Artifacts?.Dispose();
                _Models?.Dispose();
                _Database?.Dispose();
            }

            _Disposed = true;
        }

        #endregion

        #region Private-Methods

        private static IEnumerable<RouteBase> Registrars(ServerContext context)
        {
            return new List<RouteBase>
            {
                new HealthRoutes(context),
                new TokenRoutes(context),
                new TenantRoutes(context),
                new UserRoutes(context),
                new CredentialRoutes(context),
                new RbacRoutes(context),
                new AuditRoutes(context),
                new RequestHistoryRoutes(context),
                new SettingsRoutes(context),
                new VaultRoutes(context),
                new McpServerRoutes(context),
                new AccountRoutes(context),
                new ModelRoutes(context),
                new SetupRoutes(context),
                new UsageRoutes(context),
                new DeviceRoutes(context),
                new NotificationRoutes(context),
                new ClientConfigRoutes(context),
                new TopicRoutes(context),
                new RefinementRoutes(context),
                new ResearchRequestRoutes(context),
                new ScheduleRoutes(context),
                new RunRoutes(context),
                new SearchRoutes(context),
                new ResearchRoutes(context),
                new BriefingRoutes(context),
                new EditionRoutes(context),
                new TemplateRoutes(context),
                new MediaRoutes(context),
                new EmailRoutes(context),
                new FeedbackRoutes(context),
                new LearningRoutes(context),
                new PromoRoutes(context),
                new PromptRoutes(context)
            };
        }

        private async Task<DatabaseDriverBase> ConnectDatabaseAsync(DatabaseSettings settings, CancellationToken token)
        {
            Exception? last = null;
            for (int attempt = 1; attempt <= DatabaseConnectAttempts; attempt++)
            {
                token.ThrowIfCancellationRequested();
                DatabaseDriverBase driver = DatabaseDriverFactory.Create(settings);
                if (driver is PostgresqlDatabaseDriver pg) pg.Logger = message => _Logging.Info("[Database] " + message);
                try
                {
                    await driver.InitializeAsync(token).ConfigureAwait(false);
                    _Logging.Info(_Header + "database " + settings.Type + " at " + settings.Server + ":" + settings.Port + "/" + settings.DatabaseName + " ready, schema version " + driver.SchemaVersion);
                    return driver;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    last = ex;
                    driver.Dispose();
                    _Logging.Warn(_Header + "database not ready (attempt " + attempt + " of " + DatabaseConnectAttempts + "): " + ex.Message);
                    if (attempt < DatabaseConnectAttempts) await Task.Delay(2000, token).ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException("Unable to initialize the database after " + DatabaseConnectAttempts + " attempts.", last);
        }

        private static async Task DefaultRouteAsync(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 404;
            ctx.Response.ContentType = "application/json";
            // RouteNotFound (no such endpoint on this server version) is distinct from NotFound (the endpoint exists but the
            // record does not), so clients such as the mobile app can tell an older server from a missing item.
            await ctx.Response.Send(Serializer.Serialize(new ErrorResponse { Error = "RouteNotFound", Message = "This server has no " + ctx.Request.Method + " " + ctx.Request.Url.RawWithoutQuery + " endpoint.", RequestId = (ctx.Metadata as RequestContext)?.RequestId }), ctx.Token).ConfigureAwait(false);
        }

        #endregion
    }
}
