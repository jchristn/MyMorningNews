namespace MyMorningNews.Sdk.Resources
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;

    /// <summary>
    /// Audio episodes.
    /// </summary>
    public class MediaResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public MediaResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List audio episodes (scripts are left out).
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of MediaAsset items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<MediaAsset>> ListAsync(MediaListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<MediaAsset>>(HttpMethod.Get, "/v1.0/api/media", (options ?? new MediaListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read an episode with its chapters and content paths.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The MediaDetailResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<MediaDetailResponse> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<MediaDetailResponse>(HttpMethod.Get, "/v1.0/api/media/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Delete an episode and its files.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/media/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Download an episode's audio, optionally a byte range.
        /// </summary>
        /// <param name="id">Episode identifier.</param>
        /// <param name="range">Byte range, or null for the whole file.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Audio, or null when the episode has no audio file yet.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The request failed.</exception>
        public async Task<DownloadResult?> DownloadAudioAsync(string id, ByteRange? range = null, CancellationToken token = default)
        {
            MediaDetailResponse detail = await GetAsync(id, token).ConfigureAwait(false);
            string? artifactId = detail.Media.ArtifactId;
            if (String.IsNullOrEmpty(artifactId)) return null;
            return await Client.Artifacts.DownloadAsync(artifactId, range, false, token).ConfigureAwait(false);
        }
    }
}
