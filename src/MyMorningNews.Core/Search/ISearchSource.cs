namespace MyMorningNews.Core.Search
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// A search backend for one provider kind.
    /// Thread safety: implementations are safe for concurrent use.
    /// </summary>
    public interface ISearchSource
    {
        /// <summary>
        /// Provider kind served.
        /// </summary>
        SearchProviderKindEnum Kind { get; }

        /// <summary>
        /// Search.
        /// </summary>
        /// <param name="provider">Provider configuration.</param>
        /// <param name="apiKey">API key from the vault, when the provider needs one.</param>
        /// <param name="request">Request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Hits in provider order (rank starting at 1).</returns>
        Task<List<SearchHit>> SearchAsync(SearchProvider provider, string? apiKey, ResearchSearchRequest request, CancellationToken token);
    }
}
