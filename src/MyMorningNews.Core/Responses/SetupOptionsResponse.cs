namespace MyMorningNews.Core.Responses
{
    using System.Collections.Generic;

    /// <summary>
    /// Model providers the Setup page can connect on this server.
    /// </summary>
    public class SetupOptionsResponse
    {
        /// <summary>
        /// Providers, recommended first within each group.
        /// </summary>
        public List<SetupProviderOption> Providers { get; set; } = new List<SetupProviderOption>();

        /// <summary>
        /// Name of the server container, for sign-in commands such as docker exec -it {container} claude.
        /// </summary>
        public string ServerContainer { get; set; } = "mymorningnews-server";
    }
}
