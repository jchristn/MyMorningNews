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
    /// Learned preferences.
    /// </summary>
    public class PreferencesResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public PreferencesResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List learned preferences.
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of LearnedPreference items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<LearnedPreference>> ListAsync(PreferenceListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<LearnedPreference>>(HttpMethod.Get, "/v1.0/api/learned-preferences", (options ?? new PreferenceListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read a learned preference with its supporting feedback.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The PreferenceDetailResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<PreferenceDetailResponse> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<PreferenceDetailResponse>(HttpMethod.Get, "/v1.0/api/learned-preferences/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Create a learned preference.
        /// </summary>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The LearnedPreference.</returns>
        /// <exception cref="ArgumentNullException">request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<LearnedPreference> CreateAsync(PreferenceUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<LearnedPreference>(HttpMethod.Post, "/v1.0/api/learned-preferences", null, request, token);
        }

        /// <summary>
        /// Update a learned preference.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The LearnedPreference.</returns>
        /// <exception cref="ArgumentNullException">id or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<LearnedPreference> UpdateAsync(string id, PreferenceUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<LearnedPreference>(HttpMethod.Put, "/v1.0/api/learned-preferences/" + MyMorningNewsClient.Segment(id, nameof(id)), null, request, token);
        }

        /// <summary>
        /// Delete a learned preference.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/learned-preferences/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Accept a proposed preference (409 unless Proposed).
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The LearnedPreference.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<LearnedPreference> AcceptAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<LearnedPreference>(HttpMethod.Post, "/v1.0/api/learned-preferences/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/accept", null, null, token);
        }

        /// <summary>
        /// Reject a proposed preference (409 unless Proposed).
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The LearnedPreference.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<LearnedPreference> RejectAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<LearnedPreference>(HttpMethod.Post, "/v1.0/api/learned-preferences/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/reject", null, null, token);
        }

        /// <summary>
        /// Enable a preference.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The LearnedPreference.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<LearnedPreference> EnableAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<LearnedPreference>(HttpMethod.Post, "/v1.0/api/learned-preferences/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/enable", null, null, token);
        }

        /// <summary>
        /// Disable a preference.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The LearnedPreference.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<LearnedPreference> DisableAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<LearnedPreference>(HttpMethod.Post, "/v1.0/api/learned-preferences/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/disable", null, null, token);
        }
    }
}
