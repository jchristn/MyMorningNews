namespace MyMorningNews.Sdk.Resources
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Briefings and their Markdown, HTML, and audio files.
    /// </summary>
    public class BriefingsResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public BriefingsResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List briefings, newest first (Markdown is left out).
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of Briefing items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<Briefing>> ListAsync(BriefingListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<Briefing>>(HttpMethod.Get, "/v1.0/api/briefings", (options ?? new BriefingListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read a briefing with its sections, items, files, and latest audio.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The BriefingDetailResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<BriefingDetailResponse> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<BriefingDetailResponse>(HttpMethod.Get, "/v1.0/api/briefings/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Delete a briefing and its files.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/briefings/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Pin or unpin a briefing past the retention window.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="pinned">True to pin, false to unpin.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Briefing.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Briefing> PinAsync(string id, bool pinned = true, CancellationToken token = default)
        {
            return Client.SendAsync<Briefing>(HttpMethod.Put, "/v1.0/api/briefings/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/pin", null, new BriefingPinRequest { Pinned = pinned }, token);
        }

        /// <summary>
        /// Store fresh Markdown and HTML files from the saved briefing.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Briefing.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Briefing> RegenerateAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<Briefing>(HttpMethod.Post, "/v1.0/api/briefings/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/regenerate", null, null, token);
        }

        /// <summary>
        /// Make (or remake) audio for a ready briefing (HTTP 202 with the queued episode). Uses the speech endpoint.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Optional voice, or null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The MediaAsset.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<MediaAsset> GenerateAudioAsync(string id, AudioGenerateRequest? request = null, CancellationToken token = default)
        {
            return Client.SendAsync<MediaAsset>(HttpMethod.Post, "/v1.0/api/briefings/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/audio", null, request, token);
        }

        /// <summary>
        /// Download the briefing file as Markdown (md) or HTML (html). 410 ArtifactExpired after retention; regenerate to restore it.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="format">md (default) or html.</param>
        /// <param name="range">Byte range, or null for the whole file.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The downloaded file.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<DownloadResult> DownloadAsync(string id, string format = "md", ByteRange? range = null, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(format)) throw new ArgumentNullException(nameof(format));
            return Client.DownloadAsync(HttpMethod.Get, "/v1.0/api/briefings/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/download", Query("format", format), null, range, null, token);
        }

        /// <summary>
        /// Download the briefing file into a stream.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="destination">Stream to write to.</param>
        /// <param name="format">md (default) or html.</param>
        /// <param name="range">Byte range, or null for the whole file.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The downloaded file.</returns>
        /// <exception cref="ArgumentNullException">id or destination is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<DownloadResult> DownloadToAsync(string id, Stream destination, string format = "md", ByteRange? range = null, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(destination);
            if (String.IsNullOrWhiteSpace(format)) throw new ArgumentNullException(nameof(format));
            return Client.DownloadAsync(HttpMethod.Get, "/v1.0/api/briefings/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/download", Query("format", format), null, range, destination, token);
        }

        /// <summary>
        /// The briefing as Markdown text.
        /// </summary>
        /// <param name="id">Briefing identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Markdown.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The request failed (409 not ready, 410 expired).</exception>
        public async Task<string> GetMarkdownAsync(string id, CancellationToken token = default)
        {
            DownloadResult result = await DownloadAsync(id, "md", null, token).ConfigureAwait(false);
            return result.AsText();
        }

        /// <summary>
        /// The briefing as HTML text.
        /// </summary>
        /// <param name="id">Briefing identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>HTML.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The request failed (409 not ready, 410 expired).</exception>
        public async Task<string> GetHtmlAsync(string id, CancellationToken token = default)
        {
            DownloadResult result = await DownloadAsync(id, "html", null, token).ConfigureAwait(false);
            return result.AsText();
        }

        /// <summary>
        /// Download the briefing's latest audio episode, optionally a byte range.
        /// </summary>
        /// <param name="id">Briefing identifier.</param>
        /// <param name="range">Byte range, or null for the whole file.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Audio, or null when the briefing has no ready audio.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The request failed.</exception>
        public async Task<DownloadResult?> DownloadAudioAsync(string id, ByteRange? range = null, CancellationToken token = default)
        {
            BriefingDetailResponse detail = await GetAsync(id, token).ConfigureAwait(false);
            string? artifactId = detail.Audio?.ArtifactId;
            if (String.IsNullOrEmpty(artifactId)) return null;
            return await Client.Artifacts.DownloadAsync(artifactId, range, false, token).ConfigureAwait(false);
        }

        /// <summary>
        /// The newest briefing matching the filter, with its detail, or null when there is none.
        /// </summary>
        /// <param name="options">Filters, or null for the caller's newest briefing. Paging is overridden.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Briefing detail, or null.</returns>
        /// <exception cref="MyMorningNewsException">The request failed.</exception>
        public async Task<BriefingDetailResponse?> GetLatestAsync(BriefingListOptions? options = null, CancellationToken token = default)
        {
            BriefingListOptions filter = options ?? new BriefingListOptions();
            filter.PageNumber = 1;
            filter.PageSize = 1;
            filter.SortBy = "createdUtc";
            filter.SortDirection = SortDirectionEnum.Descending;
            EnumerationResult<Briefing> page = await ListAsync(filter, token).ConfigureAwait(false);
            Briefing? newest = page.Items.FirstOrDefault();
            if (newest == null) return null;
            return await GetAsync(newest.Id, token).ConfigureAwait(false);
        }
    }
}
