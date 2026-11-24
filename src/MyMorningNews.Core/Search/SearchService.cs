namespace MyMorningNews.Core.Search
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Fetch;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Core.Telemetry;
    using SyslogLogging;

    /// <summary>
    /// Runs searches against a tenant's providers (plan section 15): resolves API keys from the vault, applies domain rules,
    /// de-duplicates by canonical URL, and records telemetry. Provider failures are reported in the outcome, never thrown.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class SearchService : IDisposable
    {
        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly VaultService _Vault;
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly HttpClient _Http;
        private readonly Dictionary<SearchProviderKindEnum, ISearchSource> _Sources;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="vault">Vault for provider API keys.</param>
        /// <param name="settings">Server settings (Search group).</param>
        /// <param name="logging">Logging.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public SearchService(DatabaseDriverBase database, VaultService vault, ServerSettings settings, LoggingModule logging)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Vault = vault ?? throw new ArgumentNullException(nameof(vault));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Http = new HttpClient(Security.NetworkGuard.CreateHandler()) { Timeout = Timeout.InfiniteTimeSpan };
            _Sources = new Dictionary<SearchProviderKindEnum, ISearchSource>
            {
                { SearchProviderKindEnum.Searxng, new SearxngSource(_Http, settings.Search) },
                { SearchProviderKindEnum.Tavily, new MuxSearchSource(SearchProviderKindEnum.Tavily) },
                { SearchProviderKindEnum.YouCom, new MuxSearchSource(SearchProviderKindEnum.YouCom) },
                { SearchProviderKindEnum.Rss, new FeedSources(SearchProviderKindEnum.Rss, _Http, settings.Search) },
                { SearchProviderKindEnum.Arxiv, new FeedSources(SearchProviderKindEnum.Arxiv, _Http, settings.Search) },
                { SearchProviderKindEnum.HackerNews, new HackerNewsSource(_Http, settings.Search) }
            };
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Search one provider.
        /// </summary>
        /// <param name="provider">Provider.</param>
        /// <param name="request">Request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Outcome with hits or an error.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public async Task<SearchOutcome> SearchAsync(SearchProvider provider, ResearchSearchRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(request);
            Stopwatch sw = Stopwatch.StartNew();
            SearchOutcome outcome = new SearchOutcome { ProviderId = provider.Id, Kind = provider.Kind.ToString() };
            request.MaxResults = Math.Min(request.MaxResults, provider.MaxResults);
            try
            {
                if (!_Sources.TryGetValue(provider.Kind, out ISearchSource? source)) throw new NotSupportedException(provider.Kind + " search is not available on this server.");
                string? key = String.IsNullOrEmpty(provider.CredentialId) ? null : await _Vault.ResolveAsync(provider.TenantId, provider.CredentialId, token).ConfigureAwait(false);
                List<SearchHit> hits = await source.SearchAsync(provider, key, request, token).ConfigureAwait(false);
                HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (SearchHit h in hits)
                {
                    string? hash = UrlCanonicalizer.Hash(h.Url);
                    if (hash == null || !seen.Add(hash)) continue;
                    if (request.ExcludeDomains.Any(d => UrlCanonicalizer.IsInDomain(h.Url, d))) continue;
                    h.CanonicalUrl = UrlCanonicalizer.Canonicalize(h.Url)!;
                    outcome.Hits.Add(h);
                }

                outcome.Success = true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException && token.IsCancellationRequested))
            {
                outcome.Success = false;
                outcome.Error = ex is OperationCanceledException ? "Timed out." : ex.Message;
                _Logging.Debug("[Search] " + provider.Kind + " failed: " + outcome.Error);
            }
            finally
            {
                outcome.DurationMs = Math.Round(sw.Elapsed.TotalMilliseconds, 1);
                MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.SearchRequests, "provider", provider.Kind.ToString(), "outcome", outcome.Success ? "success" : "error");
                MyMorningNewsTelemetry.Record(MyMorningNewsTelemetry.SearchDuration, sw.Elapsed.TotalSeconds, "provider", provider.Kind.ToString());
            }

            return outcome;
        }

        /// <summary>
        /// Providers a topic searches: its enabled providers when it chose any, otherwise the tenant defaults.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="topicId">Topic.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Providers with per-topic result limits applied.</returns>
        public async Task<List<SearchProvider>> ProvidersForTopicAsync(string tenantId, string topicId, CancellationToken token = default)
        {
            List<TopicSearchProvider> links = (await _Database.TopicSearchProviders.EnumerateAsync(new TopicSearchProviderFilter { TenantId = tenantId, TopicId = topicId, PageSize = 100 }, token).ConfigureAwait(false)).Objects;
            List<SearchProvider> all = (await _Database.SearchProviders.EnumerateAsync(new SearchProviderFilter { TenantId = tenantId, PageSize = 100 }, token).ConfigureAwait(false)).Objects;
            if (links.Count == 0) return all.Where(p => p.IsDefault && p.Active).ToList();
            List<SearchProvider> chosen = new List<SearchProvider>();
            foreach (TopicSearchProvider link in links.Where(l => l.Enabled))
            {
                SearchProvider? p = all.FirstOrDefault(x => x.Id == link.SearchProviderId && x.Active);
                if (p == null) continue;
                if (link.MaxResults.HasValue) p.MaxResults = Math.Clamp(link.MaxResults.Value, 1, 50);
                chosen.Add(p);
            }

            return chosen;
        }

        /// <summary>
        /// Seed a tenant's search providers when it has none: SearXNG and RSS feeds as defaults, Hacker News and arXiv
        /// available but off by default.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Providers created.</returns>
        public async Task<int> EnsureDefaultsAsync(string tenantId, CancellationToken token = default)
        {
            if (await _Database.SearchProviders.CountAsync(new SearchProviderFilter { TenantId = tenantId }, token).ConfigureAwait(false) > 0) return 0;
            SearchProvider[] defaults =
            {
                new SearchProvider { TenantId = tenantId, Name = "SearXNG", Kind = SearchProviderKindEnum.Searxng, IsDefault = true },
                new SearchProvider { TenantId = tenantId, Name = "RSS and Atom feeds", Kind = SearchProviderKindEnum.Rss, IsDefault = true, MaxResults = 20 },
                new SearchProvider { TenantId = tenantId, Name = "Hacker News", Kind = SearchProviderKindEnum.HackerNews },
                new SearchProvider { TenantId = tenantId, Name = "arXiv", Kind = SearchProviderKindEnum.Arxiv }
            };
            foreach (SearchProvider p in defaults) await _Database.SearchProviders.CreateAsync(p, token).ConfigureAwait(false);
            return defaults.Length;
        }

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        /// <param name="disposing">True when called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing) _Http.Dispose();
            _Disposed = true;
        }

        #endregion
    }
}
