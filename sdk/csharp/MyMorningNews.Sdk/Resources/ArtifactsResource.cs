namespace MyMorningNews.Sdk.Resources
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;

    /// <summary>
    /// Stored files (briefing Markdown and HTML, audio, chapters).
    /// </summary>
    public class ArtifactsResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public ArtifactsResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List stored files.
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of Artifact items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<Artifact>> ListAsync(ArtifactListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<Artifact>>(HttpMethod.Get, "/v1.0/api/artifacts", (options ?? new ArtifactListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read stored file metadata.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Artifact.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Artifact> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<Artifact>(HttpMethod.Get, "/v1.0/api/artifacts/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Download a stored file, optionally a byte range (HTTP 206).
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="range">Byte range, or null for the whole file.</param>
        /// <param name="attachment">Ask for Content-Disposition attachment.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The downloaded file.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<DownloadResult> DownloadAsync(string id, ByteRange? range = null, bool attachment = false, CancellationToken token = default)
        {
            return Client.DownloadAsync(HttpMethod.Get, "/v1.0/api/artifacts/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/content", Query("download", attachment ? (object)true : null), null, range, null, token);
        }

        /// <summary>
        /// Download a stored file into a stream.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="destination">Stream to write to.</param>
        /// <param name="range">Byte range, or null for the whole file.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The downloaded file.</returns>
        /// <exception cref="ArgumentNullException">id or destination is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<DownloadResult> DownloadToAsync(string id, Stream destination, ByteRange? range = null, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(destination);
            return Client.DownloadAsync(HttpMethod.Get, "/v1.0/api/artifacts/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/content", null, null, range, destination, token);
        }

        /// <summary>
        /// A presigned download URL when the store offers one, plus the content path.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The DownloadUrlResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<DownloadUrlResponse> GetDownloadUrlAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<DownloadUrlResponse>(HttpMethod.Get, "/v1.0/api/artifacts/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/download-url", null, null, token);
        }
    }
}
