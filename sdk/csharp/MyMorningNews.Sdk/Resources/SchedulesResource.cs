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
    /// Schedules for topics and editions.
    /// </summary>
    public class SchedulesResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public SchedulesResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List schedules.
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of Schedule items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<Schedule>> ListAsync(ScheduleListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<Schedule>>(HttpMethod.Get, "/v1.0/api/schedules", (options ?? new ScheduleListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read a schedule.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Schedule.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Schedule> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<Schedule>(HttpMethod.Get, "/v1.0/api/schedules/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Create a schedule.
        /// </summary>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Schedule.</returns>
        /// <exception cref="ArgumentNullException">request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Schedule> CreateAsync(ScheduleUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<Schedule>(HttpMethod.Post, "/v1.0/api/schedules", null, request, token);
        }

        /// <summary>
        /// Update a schedule.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Schedule.</returns>
        /// <exception cref="ArgumentNullException">id or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Schedule> UpdateAsync(string id, ScheduleUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<Schedule>(HttpMethod.Put, "/v1.0/api/schedules/" + MyMorningNewsClient.Segment(id, nameof(id)), null, request, token);
        }

        /// <summary>
        /// Delete a schedule.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/schedules/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Validate a schedule and preview its next runs without saving.
        /// </summary>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The SchedulePreviewResponse.</returns>
        /// <exception cref="ArgumentNullException">request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<SchedulePreviewResponse> PreviewAsync(ScheduleUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<SchedulePreviewResponse>(HttpMethod.Post, "/v1.0/api/schedules/preview", null, request, token);
        }
    }
}
