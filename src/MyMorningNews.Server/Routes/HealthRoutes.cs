namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using MyMorningNews.Core;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Serialization;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Anonymous health route.
    /// </summary>
    public class HealthRoutes
    {
        #region Private-Members

        private readonly DateTime _StartedUtc = DateTime.UtcNow;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Register routes.
        /// </summary>
        /// <param name="server">Watson server.</param>
        /// <exception cref="ArgumentNullException">server is null.</exception>
        public void Register(Webserver server)
        {
            ArgumentNullException.ThrowIfNull(server);
            server.Routes.PreAuthentication.Static.Add(
                HttpMethod.GET,
                Constants.HealthPath,
                GetHealthAsync,
                null,
                openApiMetadata: OpenApiRouteMetadata.Create("Server health", "Health"));
        }

        #endregion

        #region Private-Methods

        private async Task GetHealthAsync(HttpContextBase ctx)
        {
            HealthResponse response = new HealthResponse
            {
                Status = "Healthy",
                Version = Constants.ProductVersion,
                StartedUtc = _StartedUtc,
                UptimeSeconds = (long)(DateTime.UtcNow - _StartedUtc).TotalSeconds
            };

            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Send(Serializer.Serialize(response), ctx.Token).ConfigureAwait(false);
        }

        #endregion
    }
}
