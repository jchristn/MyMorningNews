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
    /// Voice profiles for audio.
    /// </summary>
    public class VoicesResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public VoicesResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List voice profiles.
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of VoiceProfile items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<VoiceProfile>> ListAsync(VoiceListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<VoiceProfile>>(HttpMethod.Get, "/v1.0/api/voices", (options ?? new VoiceListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read a voice profile.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The VoiceProfile.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<VoiceProfile> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<VoiceProfile>(HttpMethod.Get, "/v1.0/api/voices/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Create a voice profile.
        /// </summary>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The VoiceProfile.</returns>
        /// <exception cref="ArgumentNullException">request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<VoiceProfile> CreateAsync(VoiceProfileUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<VoiceProfile>(HttpMethod.Post, "/v1.0/api/voices", null, request, token);
        }

        /// <summary>
        /// Update a voice profile.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The VoiceProfile.</returns>
        /// <exception cref="ArgumentNullException">id or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<VoiceProfile> UpdateAsync(string id, VoiceProfileUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<VoiceProfile>(HttpMethod.Put, "/v1.0/api/voices/" + MyMorningNewsClient.Segment(id, nameof(id)), null, request, token);
        }

        /// <summary>
        /// Delete a voice profile.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/voices/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Speak a sample with the voice and return the audio (502 TtsError when the speech endpoint fails).
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Optional text, or null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The downloaded file.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<DownloadResult> TestAsync(string id, VoiceTestRequest? request = null, CancellationToken token = default)
        {
            return Client.DownloadAsync(HttpMethod.Post, "/v1.0/api/voices/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/test", null, request, null, null, token);
        }

        /// <summary>
        /// The voices a topic can use: the workspace's voice profiles, then the voices the speech server offers that have
        /// no profile yet, each with a readable label. Anyone who can read topics may call it. To use one for a topic, send
        /// its VoiceProfileId, or its Voice when it has no profile yet, in a TopicUpsertRequest.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The AvailableVoicesResponse.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<AvailableVoicesResponse> GetAvailableAsync(CancellationToken token = default)
        {
            return Client.SendAsync<AvailableVoicesResponse>(HttpMethod.Get, "/v1.0/api/voices/available", null, null, token);
        }

        /// <summary>
        /// Speak a short sample (up to 300 characters) with a speech server voice or a voice profile and return the audio.
        /// Limited to 20 previews a minute per user. 409 when speech is turned off, 429 TooManyRequests over the limit,
        /// 502 TtsError when the speech endpoint fails.
        /// </summary>
        /// <param name="request">Voice or VoiceProfileId, and optional text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The audio (audio/wav).</returns>
        /// <exception cref="ArgumentNullException">request is null.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<DownloadResult> PreviewAsync(VoicePreviewRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.DownloadAsync(HttpMethod.Post, "/v1.0/api/voices/preview", null, request, null, null, token);
        }
    }
}
