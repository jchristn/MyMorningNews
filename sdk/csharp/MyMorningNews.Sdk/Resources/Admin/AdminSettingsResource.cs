namespace MyMorningNews.Sdk.Resources.Admin
{
    using System;
    using System.Net.Http;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Resources;

    /// <summary>
    /// Server settings (secrets masked).
    /// </summary>
    public class AdminSettingsResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public AdminSettingsResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// Read server settings with field metadata.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The SettingsResponse.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<SettingsResponse> GetAsync(CancellationToken token = default)
        {
            return Client.SendAsync<SettingsResponse>(HttpMethod.Get, "/v1.0/api/settings", null, null, token);
        }

        /// <summary>
        /// Update server settings. Send the whole settings object (as read from GetAsync).
        /// </summary>
        /// <param name="settings">Settings object.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The SettingsUpdateResult.</returns>
        /// <exception cref="ArgumentNullException">settings is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<SettingsUpdateResult> UpdateAsync(JsonObject settings, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return Client.SendAsync<SettingsUpdateResult>(HttpMethod.Put, "/v1.0/api/settings", null, settings, token);
        }

        /// <summary>
        /// Restart the server (HTTP 202).
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task RestartAsync(CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Post, "/v1.0/api/settings/restart", null, null, token);
        }
    }
}
