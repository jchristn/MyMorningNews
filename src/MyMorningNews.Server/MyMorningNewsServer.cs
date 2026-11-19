namespace MyMorningNews.Server
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Server.Routes;
    using SyslogLogging;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Server host. Owns the Watson webserver, wires the routing hooks, and registers feature route registrars.
    /// Thread safety: start and stop from a single thread.
    /// </summary>
    public class MyMorningNewsServer : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Server settings.
        /// </summary>
        public ServerSettings Settings { get; }

        /// <summary>
        /// Watson webserver instance.
        /// </summary>
        public Webserver Server { get; }

        #endregion

        #region Private-Members

        private readonly LoggingModule _Logging;
        private readonly string _Header = "[MyMorningNewsServer] ";
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the server host.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging module.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public MyMorningNewsServer(ServerSettings settings, LoggingModule logging)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(logging);

            Settings = settings;
            _Logging = logging;

            WebserverSettings webserverSettings = new WebserverSettings(Settings.Rest.Hostname, Settings.Rest.Port, Settings.Rest.Ssl);
            Server = new Webserver(webserverSettings, DefaultRouteAsync);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Configure routes and start listening.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public Task StartAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            ConfigureServer();
            ConfigureRoutes();
            Server.Start(token);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Stop listening.
        /// </summary>
        public void Stop()
        {
            if (Server.IsListening) Server.Stop();
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
                Server.Dispose();
            }

            _Disposed = true;
        }

        #endregion

        #region Private-Methods

        private void ConfigureServer()
        {
            Server.UseOpenApi(openApi =>
            {
                openApi.Info.Title = Constants.ProductName;
                openApi.Info.Version = Constants.ProductVersion;
                openApi.Info.Description = "REST API for My Morning News.";
            });

            Server.Routes.Preflight = PreflightRouteAsync;
            Server.Routes.PostRouting = PostRoutingRouteAsync;
        }

        private void ConfigureRoutes()
        {
            new HealthRoutes().Register(Server);
        }

        private async Task PreflightRouteAsync(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 200;
            AddCorsHeaders(ctx);
            ctx.Response.Headers.Add("Access-Control-Max-Age", "86400");
            await ctx.Response.Send(ctx.Token).ConfigureAwait(false);
        }

        private async Task PostRoutingRouteAsync(HttpContextBase ctx)
        {
            ctx.Timestamp.End = DateTime.UtcNow;
            AddCorsHeaders(ctx);
            _Logging.Debug(
                _Header +
                ctx.Request.Method + " " +
                ctx.Request.Url.RawWithQuery + " " +
                ctx.Response.StatusCode + " " +
                "(" + (ctx.Timestamp.TotalMs.HasValue ? ctx.Timestamp.TotalMs.Value.ToString("F2") : "?") + "ms)");
            await Task.CompletedTask.ConfigureAwait(false);
        }

        private void AddCorsHeaders(HttpContextBase ctx)
        {
            ctx.Response.Headers.Set("Access-Control-Allow-Origin", Settings.Rest.CorsAllowedOrigins);
            ctx.Response.Headers.Set("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS, HEAD");
            ctx.Response.Headers.Set("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Api-Key, X-Token, X-Email, X-Password, X-Tenant-Guid, X-Access-Key, X-Secret-Key, X-Request, Accept-Language");
            ctx.Response.Headers.Set("Access-Control-Expose-Headers", "X-Request-Id");
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
