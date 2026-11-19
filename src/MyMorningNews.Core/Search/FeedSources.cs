namespace MyMorningNews.Core.Search
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Settings;

    /// <summary>
    /// RSS and Atom feeds from the topic's FeedUrl constraints, and arXiv through its Atom API. Feed items are matched
    /// against the query terms; arXiv searches its own index.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class FeedSources : HttpSearchSourceBase, ISearchSource
    {
        /// <inheritdoc />
        public SearchProviderKindEnum Kind { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="kind">Rss or Arxiv.</param>
        /// <param name="http">Shared HTTP client.</param>
        /// <param name="settings">Search settings.</param>
        /// <exception cref="ArgumentException">kind is not Rss or Arxiv.</exception>
        public FeedSources(SearchProviderKindEnum kind, HttpClient http, SearchSettings settings) : base(http, settings)
        {
            if (kind != SearchProviderKindEnum.Rss && kind != SearchProviderKindEnum.Arxiv) throw new ArgumentException("Only Rss and Arxiv are feed sources.", nameof(kind));
            Kind = kind;
        }

        /// <inheritdoc />
        public async Task<List<SearchHit>> SearchAsync(SearchProvider provider, string? apiKey, ResearchSearchRequest request, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(request);
            if (Kind == SearchProviderKindEnum.Arxiv)
            {
                string baseUrl = String.IsNullOrWhiteSpace(provider.Endpoint) ? Settings.ArxivUrl : provider.Endpoint;
                string body = await GetTextAsync(provider, Combine(baseUrl, "query?sortBy=submittedDate&sortOrder=descending&max_results=" + request.MaxResults + "&search_query=all:" + WebUtility.UrlEncode(request.Query)), token).ConfigureAwait(false);
                return FeedParser.Parse(body, Kind.ToString()).Take(request.MaxResults).ToList();
            }

            List<SearchHit> all = new List<SearchHit>();
            foreach (string feed in request.FeedUrls.Distinct(StringComparer.OrdinalIgnoreCase).Take(30))
            {
                try
                {
                    all.AddRange(FeedParser.Parse(await GetTextAsync(provider, feed, token).ConfigureAwait(false), Kind.ToString()));
                }
                catch (Exception ex) when (ex is HttpRequestException || (ex is OperationCanceledException && !token.IsCancellationRequested))
                {
                    continue;
                }
            }

            string[] terms = request.Query.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length > 2).Select(t => t.ToLowerInvariant()).ToArray();
            IEnumerable<SearchHit> matched = all.Where(h => request.SinceUtc == null || h.PublishedUtc == null || h.PublishedUtc >= request.SinceUtc);
            if (terms.Length > 0)
                matched = matched.Select(h => (hit: h, score: terms.Count(t => (h.Title + " " + h.Snippet).ToLowerInvariant().Contains(t, StringComparison.Ordinal))))
                    .Where(x => x.score > 0).OrderByDescending(x => x.score).ThenByDescending(x => x.hit.PublishedUtc).Select(x => x.hit);
            List<SearchHit> result = matched.Take(request.MaxResults).ToList();
            for (int i = 0; i < result.Count; i++) result[i].Rank = i + 1;
            return result;
        }
    }
}
