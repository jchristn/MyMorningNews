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
        /// GET a URL as text with the provider timeout. Transient statuses (408, 429, 500, 502, 503, 504) are retried up to
        /// SearchSettings.MaxAttempts in total, waiting for the Retry-After header (at most 5 seconds) or an exponential
        /// backoff from 500 ms. Each attempt has its own timeout.
        /// </summary>
        /// <param name="provider">Provider (timeout).</param>
        /// <param name="url">URL.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Body text.</returns>
        /// <exception cref="HttpRequestException">Non-success status after the last attempt.</exception>
        protected async Task<string> GetTextAsync(SearchProvider provider, string url, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(url);
            int attempts = Math.Max(1, Settings.MaxAttempts);
            for (int attempt = 1; ; attempt++)
            {
                TimeSpan? wait = null;
                using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    timeout.CancelAfter(provider.TimeoutMs);
                    request.Headers.TryAddWithoutValidation("User-Agent", Settings.UserAgent);
                    using (HttpResponseMessage response = await Http.SendAsync(request, timeout.Token).ConfigureAwait(false))
                    {
                        string body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
                        if (response.IsSuccessStatusCode) return body;
                        int status = (int)response.StatusCode;
                        if (attempt >= attempts || !IsTransient(status)) throw new HttpRequestException(provider.Kind + " returned " + status + (attempt > 1 ? " after " + attempt + " attempts." : "."), null, response.StatusCode);
                        wait = RetryDelay(response, attempt);
                    }
                }

                await Task.Delay(wait.Value, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Whether an HTTP status is worth retrying.
        /// </summary>
        /// <param name="status">Status code.</param>
        /// <returns>True for 408, 429, 500, 502, 503, and 504.</returns>
        public static bool IsTransient(int status)
        {
            return status == 408 || status == 429 || status == 500 || status == 502 || status == 503 || status == 504;
        }

        private static TimeSpan RetryDelay(HttpResponseMessage response, int attempt)
        {
            TimeSpan max = TimeSpan.FromSeconds(5);
            if (response.Headers.RetryAfter?.Delta is TimeSpan delta) return delta > max ? max : delta;
            if (response.Headers.RetryAfter?.Date is DateTimeOffset date)
            {
                TimeSpan until = date - DateTimeOffset.UtcNow;
                return until < TimeSpan.Zero ? TimeSpan.Zero : until > max ? max : until;
            }

            return TimeSpan.FromMilliseconds(Math.Min(max.TotalMilliseconds, 500 * Math.Pow(2, attempt - 1)));
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
