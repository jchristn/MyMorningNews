namespace MyMorningNews.Sdk.Resources
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Push notification devices of the signed-in user.
    /// </summary>
    public class DevicesResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public DevicesResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List the caller's devices.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of Device items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<List<Device>> ListAsync(CancellationToken token = default)
        {
            return Client.SendAsync<List<Device>>(HttpMethod.Get, "/v1.0/api/devices", null, null, token);
        }

        /// <summary>
        /// Register a device (201 new, 200 when the same token is registered again).
        /// </summary>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Device.</returns>
        /// <exception cref="ArgumentNullException">request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Device> RegisterAsync(DeviceRegisterRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<Device>(HttpMethod.Post, "/v1.0/api/devices", null, request, token);
        }

        /// <summary>
        /// Remove a device.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/devices/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Send a test notification to a device.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The PushResult.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<PushResult> TestAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<PushResult>(HttpMethod.Post, "/v1.0/api/devices/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/test", null, null, token);
        }
    }
}
