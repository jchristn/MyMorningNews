namespace MyMorningNews.Core.Search
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Settings;

    /// <summary>
    /// Shared HTTP plumbing for search sources: one HttpClient, the configured user agent, and the provider timeout.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public abstract class HttpSearchSourceBase
    {
        /// <summary>
        /// Shared HTTP client.
        /// </summary>
        protected HttpClient Http { get; }

        /// <summary>
        /// Search settings.
        /// </summary>
        protected SearchSettings Settings { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="http">Shared HTTP client.</param>
        /// <param name="settings">Search settings.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        protected HttpSearchSourceBase(HttpClient http, SearchSettings settings)
        {
            Http = http ?? throw new ArgumentNullException(nameof(http));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// GET a URL as text with the provider timeout.
        /// </summary>
        /// <param name="provider">Provider (timeout).</param>
        /// <param name="url">URL.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Body text.</returns>
        /// <exception cref="HttpRequestException">Non-success status.</exception>
        protected async Task<string> GetTextAsync(SearchProvider provider, string url, CancellationToken token)
        {
            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                timeout.CancelAfter(provider.TimeoutMs);
                request.Headers.TryAddWithoutValidation("User-Agent", Settings.UserAgent);
                using (HttpResponseMessage response = await Http.SendAsync(request, timeout.Token).ConfigureAwait(false))
                {
                    string body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode) throw new HttpRequestException(provider.Kind + " returned " + (int)response.StatusCode + ".", null, response.StatusCode);
                    return body;
                }
            }
        }

        /// <summary>
        /// Combine a base URL and a relative path.
        /// </summary>
        /// <param name="baseUrl">Base URL.</param>
        /// <param name="path">Path (with query).</param>
        /// <returns>URL.</returns>
        protected static string Combine(string baseUrl, string path)
        {
            return (baseUrl ?? String.Empty).TrimEnd('/') + "/" + path.TrimStart('/');
        }
    }
}
