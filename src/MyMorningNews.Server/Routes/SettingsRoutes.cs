namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using MyMorningNews.Core;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Settings;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Runtime settings (read with secrets masked, validated update, restart) and server information.
    /// </summary>
    public class SettingsRoutes : RouteBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public SettingsRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/settings", ReadAsync, null, openApiMetadata: Doc("Read server settings (secrets masked)", "Settings"));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.PUT, "/v1.0/api/settings", UpdateAsync, null, openApiMetadata: Doc("Update server settings", "Settings").WithRequestBody(Body<ServerSettings>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/api/settings/restart", RestartAsync, null, openApiMetadata: Doc("Restart the server", "Settings"));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/server-info", InfoAsync, null, openApiMetadata: Doc("Server information and bundled tool links", "Settings"));
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.SettingsRead).ConfigureAwait(false)) return;
            await SendJsonAsync(ctx, 200, Server.SettingsService.Describe()).ConfigureAwait(false);
        }

        private async Task UpdateAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.SettingsWrite).ConfigureAwait(false)) return;
            ServerSettings? incoming = await ReadBodyAsync<ServerSettings>(ctx).ConfigureAwait(false);
            if (incoming == null) return;
            SettingsUpdateResult result = await Server.SettingsService.UpdateAsync(incoming, ctx.Token).ConfigureAwait(false);
            if (!result.Success)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "One or more settings are invalid.", result.Errors).ConfigureAwait(false);
                return;
            }

            if (result.Changed.Count > 0)
            {
                Server.Logging.Info("[SettingsRoutes] settings changed by " + (Context(ctx).DisplayName ?? "unknown") + ": " + String.Join(", ", result.Changed));
                await Server.Audit.RecordAsync(AuditEventTypeEnum.SettingsChanged, Context(ctx), 200, "Changed: " + String.Join(", ", result.Changed), ctx.Token).ConfigureAwait(false);
            }

            await SendJsonAsync(ctx, 200, result).ConfigureAwait(false);
        }

        private async Task RestartAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.ServerRestart).ConfigureAwait(false)) return;
            Server.Logging.Warn("[SettingsRoutes] restart requested by " + (Context(ctx).DisplayName ?? "unknown"));
            await Server.Audit.RecordAsync(AuditEventTypeEnum.RestartRequested, Context(ctx), 202, "Restart requested.", ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 202, new ErrorResponse { Error = "Accepted", Message = "The server is restarting.", RequestId = Context(ctx).RequestId }).ConfigureAwait(false);
            Server.Lifetime.RequestRestart();
        }

        private async Task InfoAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.ServerInfoRead).ConfigureAwait(false)) return;
            TelemetrySettings t = Server.Settings.Telemetry;
            ServerInfoResponse info = new ServerInfoResponse
            {
                Product = Constants.ProductName,
                Version = Constants.ProductVersion,
                StartedUtc = Server.Lifetime.StartedUtc,
                UptimeSeconds = (long)(DateTime.UtcNow - Server.Lifetime.StartedUtc).TotalSeconds,
                DatabaseType = Server.Database.DatabaseType.ToString(),
                SchemaVersion = Server.Database.SchemaVersion,
                VectorAvailable = Server.Database is MyMorningNews.Core.Database.Postgresql.PostgresqlDatabaseDriver pg && pg.VectorAvailable,
                RuntimeVersion = RuntimeInformation.FrameworkDescription,
                OperatingSystem = RuntimeInformation.OSDescription,
                PendingRestart = Server.SettingsService.Describe().PendingRestart,
                ExternalServices = new List<ExternalServiceInfo>
                {
                    new ExternalServiceInfo { Name = "Grafana", Url = t.GrafanaUrl, Credentials = "admin / admin", Description = "Dashboards for metrics, traces, and logs." },
                    new ExternalServiceInfo { Name = "Prometheus", Url = t.PrometheusUrl, Description = "Metrics." },
                    new ExternalServiceInfo { Name = "Tempo", Url = t.TempoUrl, Description = "Traces." },
                    new ExternalServiceInfo { Name = "Loki", Url = t.LokiUrl, Description = "Logs." }
                }
            };
            await SendJsonAsync(ctx, 200, info).ConfigureAwait(false);
        }
    }
}
