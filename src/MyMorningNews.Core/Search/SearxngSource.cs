namespace MyMorningNews.Core.Search
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Settings;

    /// <summary>
    /// SearXNG metasearch (the bundled, keyless default) through its JSON API.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class SearxngSource : HttpSearchSourceBase, ISearchSource
    {
        /// <inheritdoc />
        public SearchProviderKindEnum Kind
        {
            get { return SearchProviderKindEnum.Searxng; }
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="http">Shared HTTP client.</param>
        /// <param name="settings">Search settings.</param>
        public SearxngSource(HttpClient http, SearchSettings settings) : base(http, settings)
        {
        }

        /// <inheritdoc />
        public async Task<List<SearchHit>> SearchAsync(SearchProvider provider, string? apiKey, ResearchSearchRequest request, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(request);
            string baseUrl = String.IsNullOrWhiteSpace(provider.Endpoint) ? Settings.SearxngUrl : provider.Endpoint;
            string body = await GetTextAsync(provider, Combine(baseUrl, "search?format=json&pageno=1&q=" + WebUtility.UrlEncode(request.Query)), token).ConfigureAwait(false);
            List<SearchHit> hits = new List<SearchHit>();
            using (JsonDocument doc = JsonDocument.Parse(body))
            {
                if (!doc.RootElement.TryGetProperty("results", out JsonElement results) || results.ValueKind != JsonValueKind.Array) return hits;
                foreach (JsonElement r in results.EnumerateArray())
                {
                    string url = Str(r, "url");
                    if (url.Length == 0) continue;
                    DateTime? published = null;
                    string date = Str(r, "publishedDate");
                    if (date.Length > 0 && DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime d)) published = d;
                    hits.Add(new SearchHit
                    {
                        Url = url,
                        Title = Str(r, "title"),
                        Snippet = Str(r, "content").Length == 0 ? null : Str(r, "content"),
                        PublishedUtc = published,
                        Score = r.TryGetProperty("score", out JsonElement sc) && sc.ValueKind == JsonValueKind.Number ? sc.GetDouble() : null,
                        Rank = hits.Count + 1,
                        Provider = Kind.ToString()
                    });
                    if (hits.Count >= request.MaxResults) break;
                }
            }

            return hits;
        }

        private static string Str(JsonElement e, string name)
        {
            return e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? String.Empty).Trim() : String.Empty;
        }
    }
}
