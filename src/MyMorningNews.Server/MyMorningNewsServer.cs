namespace MyMorningNews.Server
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Database.Postgresql;
    using MyMorningNews.Core.Security;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Core.Settings;
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
            await tenantDefaults.EnsureAllAsync(token).ConfigureAwait(false);
            _Context = new ServerContext(_Settings, _Database, _Logging, authentication, authorization, audit, Lifetime, vault, prompts, _Models, tenantDefaults);

            _Capture = new RequestHistoryCaptureService(_Database, settings.RequestHistory, _Logging);
            RequestPipeline pipeline = new RequestPipeline(_Context, _Capture);

            WebserverSettings webserverSettings = new WebserverSettings(settings.Rest.Hostname, settings.Rest.Port, settings.Rest.Ssl);
            webserverSettings.Telemetry.Enable = settings.Telemetry.Enabled;
            webserverSettings.Telemetry.Prometheus.Enable = false;
            _Webserver = new Webserver(webserverSettings, DefaultRouteAsync);
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
                new ModelRoutes(context),
                new UsageRoutes(context),
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
            await ctx.Response.Send("{\"error\":\"NotFound\",\"message\":\"The requested resource was not found.\"}", ctx.Token).ConfigureAwait(false);
        }

        #endregion
    }
}
