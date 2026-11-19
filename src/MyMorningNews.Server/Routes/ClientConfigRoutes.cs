namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using MyMorningNews.Core;
    using MyMorningNews.Core.Responses;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Public client configuration for the web and mobile apps.
    /// </summary>
    public class ClientConfigRoutes : RouteBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public ClientConfigRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/client-config", ConfigAsync, null, openApiMetadata: Doc("Public client configuration", "Server",
                "Feature flags, minimum app version, and legal links for the web and mobile apps. No authentication."));
        }

        private async Task ConfigAsync(HttpContextBase ctx)
        {
            await SendJsonAsync(ctx, 200, new ClientConfigResponse
            {
                ProductName = Constants.ProductName,
                ServerVersion = Constants.ProductVersion,
                SignupEnabled = false,
                PushEnabled = Server.SettingsService.Current.Push.Enabled,
                MinimumAppVersion = "0.1.0",
                Links = new Dictionary<string, string>
                {
                    { "privacy", "https://mymorningnews.ai/privacy.html" },
                    { "terms", "https://mymorningnews.ai/terms.html" },
                    { "support", "https://mymorningnews.ai/support.html" },
                    { "deleteAccount", "https://mymorningnews.ai/delete-account.html" }
                }
            }).ConfigureAwait(false);
        }
    }
}
