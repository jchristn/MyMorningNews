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
    /// Hacker News stories through the Algolia search API (keyless).
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class HackerNewsSource : HttpSearchSourceBase, ISearchSource
    {
        /// <inheritdoc />
        public SearchProviderKindEnum Kind
        {
            get { return SearchProviderKindEnum.HackerNews; }
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="http">Shared HTTP client.</param>
        /// <param name="settings">Search settings.</param>
        public HackerNewsSource(HttpClient http, SearchSettings settings) : base(http, settings)
        {
        }

        /// <inheritdoc />
        public async Task<List<SearchHit>> SearchAsync(SearchProvider provider, string? apiKey, ResearchSearchRequest request, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(request);
            string baseUrl = String.IsNullOrWhiteSpace(provider.Endpoint) ? Settings.HackerNewsUrl : provider.Endpoint;
            string path = "search_by_date?tags=story&hitsPerPage=" + request.MaxResults + "&query=" + WebUtility.UrlEncode(request.Query);
            if (request.SinceUtc.HasValue) path += "&numericFilters=created_at_i>" + new DateTimeOffset(DateTime.SpecifyKind(request.SinceUtc.Value, DateTimeKind.Utc)).ToUnixTimeSeconds();
            string body = await GetTextAsync(provider, Combine(baseUrl, path), token).ConfigureAwait(false);
            List<SearchHit> hits = new List<SearchHit>();
            using (JsonDocument doc = JsonDocument.Parse(body))
            {
                if (!doc.RootElement.TryGetProperty("hits", out JsonElement arr) || arr.ValueKind != JsonValueKind.Array) return hits;
                foreach (JsonElement h in arr.EnumerateArray())
                {
                    string id = h.TryGetProperty("objectID", out JsonElement o) ? o.GetString() ?? String.Empty : String.Empty;
                    string url = h.TryGetProperty("url", out JsonElement u) && u.ValueKind == JsonValueKind.String ? u.GetString() ?? String.Empty : String.Empty;
                    if (url.Length == 0 && id.Length > 0) url = "https://news.ycombinator.com/item?id=" + id;
                    if (url.Length == 0) continue;
                    DateTime? created = h.TryGetProperty("created_at", out JsonElement c) && c.ValueKind == JsonValueKind.String &&
                        DateTime.TryParse(c.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime d) ? d : null;
                    int points = h.TryGetProperty("points", out JsonElement p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : 0;
                    hits.Add(new SearchHit
                    {
                        Url = url,
                        Title = h.TryGetProperty("title", out JsonElement t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? url : url,
                        Snippet = points > 0 ? points + " points on Hacker News" : null,
                        PublishedUtc = created,
                        Score = points,
                        Rank = hits.Count + 1,
                        Provider = Kind.ToString()
                    });
                }
            }

            return hits;
        }
    }
}
