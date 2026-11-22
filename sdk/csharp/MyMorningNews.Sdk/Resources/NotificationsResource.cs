namespace MyMorningNews.Sdk.Resources
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// The caller's in-app notifications and notification preferences (which kinds they want by email, push, and in-app).
    /// </summary>
    public class NotificationsResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public NotificationsResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List the caller's in-app notifications, newest first.
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of Notification items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<Notification>> ListAsync(NotificationListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<Notification>>(HttpMethod.Get, "/v1.0/api/notifications", (options ?? new NotificationListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Count the caller's unread in-app notifications.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The NotificationCountResponse.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<NotificationCountResponse> UnreadCountAsync(CancellationToken token = default)
        {
            return Client.SendAsync<NotificationCountResponse>(HttpMethod.Get, "/v1.0/api/notifications/unread-count", null, null, token);
        }

        /// <summary>
        /// Mark an in-app notification as read.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Notification.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Notification> MarkReadAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<Notification>(HttpMethod.Post, "/v1.0/api/notifications/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/read", null, null, token);
        }

        /// <summary>
        /// Mark all of the caller's in-app notifications as read. The count is how many were marked.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The NotificationCountResponse.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<NotificationCountResponse> MarkAllReadAsync(CancellationToken token = default)
        {
            return Client.SendAsync<NotificationCountResponse>(HttpMethod.Post, "/v1.0/api/notifications/read-all", null, null, token);
        }

        /// <summary>
        /// Delete an in-app notification.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/notifications/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// The caller's notification preferences: every kind on every channel, and whether each channel can deliver.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The NotificationPreferencesResponse.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<NotificationPreferencesResponse> GetPreferencesAsync(CancellationToken token = default)
        {
            return Client.SendAsync<NotificationPreferencesResponse>(HttpMethod.Get, "/v1.0/api/notifications/preferences", null, null, token);
        }

        /// <summary>
        /// Change the caller's notification preferences. Only the cells that are sent change.
        /// </summary>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The NotificationPreferencesResponse.</returns>
        /// <exception cref="ArgumentNullException">request is null.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<NotificationPreferencesResponse> UpdatePreferencesAsync(NotificationPreferencesRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<NotificationPreferencesResponse>(HttpMethod.Put, "/v1.0/api/notifications/preferences", null, request, token);
        }
    }
}
