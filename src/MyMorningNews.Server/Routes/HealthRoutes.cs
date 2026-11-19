namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using MyMorningNews.Core;
    using MyMorningNews.Core.Responses;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Anonymous health route. Reports Unhealthy (503) when the database is unreachable.
    /// </summary>
    public class HealthRoutes : RouteBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public HealthRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, Constants.HealthPath, GetHealthAsync, null, openApiMetadata: Doc("Server health", "Health"));
        }

        private async Task GetHealthAsync(HttpContextBase ctx)
        {
            HealthResponse response = new HealthResponse
            {
                Version = Constants.ProductVersion,
                StartedUtc = Server.Lifetime.StartedUtc,
                UptimeSeconds = (long)(DateTime.UtcNow - Server.Lifetime.StartedUtc).TotalSeconds
            };

            try
            {
                await Server.Database.ExecuteQueryAsync("SELECT 1", false, ctx.Token).ConfigureAwait(false);
                response.Components["database"] = "Healthy";
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                response.Components["database"] = "Unhealthy";
                response.Status = "Unhealthy";
            }

            response.Components["requestHistory"] = Server.Settings.RequestHistory.Enabled ? "Enabled" : "Disabled";
            await SendJsonAsync(ctx, response.Status == "Healthy" ? 200 : 503, response).ConfigureAwait(false);
        }
    }
}
