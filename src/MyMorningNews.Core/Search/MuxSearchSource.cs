namespace MyMorningNews.Core.Search
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using Mux.Search.Models;
    using Mux.Search.Providers.Tavily;
    using Mux.Search.Providers.You;

    /// <summary>
    /// Tavily and You.com through the Mux.Search clients (one client per call, because the clients configure their own
    /// HttpClient). Both need an API key from the vault.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class MuxSearchSource : ISearchSource
    {
        /// <inheritdoc />
        public SearchProviderKindEnum Kind { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="kind">Tavily or YouCom.</param>
        /// <exception cref="ArgumentException">kind is not Tavily or YouCom.</exception>
        public MuxSearchSource(SearchProviderKindEnum kind)
        {
            if (kind != SearchProviderKindEnum.Tavily && kind != SearchProviderKindEnum.YouCom) throw new ArgumentException("Only Tavily and YouCom are served by Mux.Search.", nameof(kind));
            Kind = kind;
        }

        /// <inheritdoc />
        public async Task<List<SearchHit>> SearchAsync(SearchProvider provider, string? apiKey, ResearchSearchRequest request, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(request);
            if (String.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException(Kind + " needs an API key. Add one to the vault and select it on the search provider.");
            SearchProviderOptions options = new SearchProviderOptions
            {
                ApiKey = apiKey,
                Timeout = TimeSpan.FromMilliseconds(provider.TimeoutMs),
                Endpoint = !String.IsNullOrWhiteSpace(provider.Endpoint) ? provider.Endpoint : (Kind == SearchProviderKindEnum.Tavily ? TavilySearchClient.DefaultEndpoint : YouSearchClient.DefaultEndpoint)
            };

            IReadOnlyList<SearchResultItem> items;
            if (Kind == SearchProviderKindEnum.Tavily)
            {
                using (TavilySearchClient client = new TavilySearchClient(options))
                {
                    TavilySearchQuery query = new TavilySearchQuery { Query = request.Query, MaxResults = request.MaxResults };
                    if (request.IncludeDomains.Any()) query.IncludeDomains = request.IncludeDomains;
                    if (request.ExcludeDomains.Any()) query.ExcludeDomains = request.ExcludeDomains;
                    items = (await client.SearchAsync(query, token).ConfigureAwait(false)).Results;
                }
            }
            else
            {
                using (YouSearchClient client = new YouSearchClient(options))
                {
                    YouSearchQuery query = new YouSearchQuery { Query = request.Query, MaxResults = request.MaxResults };
                    if (request.IncludeDomains.Any()) query.IncludeDomains = request.IncludeDomains;
                    if (request.ExcludeDomains.Any()) query.ExcludeDomains = request.ExcludeDomains;
                    items = (await client.SearchAsync(query, token).ConfigureAwait(false)).Results;
                }
            }

            List<SearchHit> hits = new List<SearchHit>();
            foreach (SearchResultItem i in items.Where(i => !String.IsNullOrWhiteSpace(i.Url)).Take(request.MaxResults))
            {
                string? snippet = !String.IsNullOrWhiteSpace(i.Description) ? i.Description : i.Snippets.FirstOrDefault();
                hits.Add(new SearchHit
                {
                    Url = i.Url,
                    Title = String.IsNullOrWhiteSpace(i.Title) ? i.Url : i.Title,
                    Snippet = snippet,
                    PublishedUtc = i.PublishedAt?.UtcDateTime,
                    Score = i.Score,
                    Rank = hits.Count + 1,
                    Provider = Kind.ToString()
                });
            }

            return hits;
        }
    }
}
