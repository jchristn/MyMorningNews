namespace MyMorningNews.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;

    /// <summary>
    /// Data access methods for EditionPublication records. Every method is tenant scoped.
    /// Thread safety: implementations are safe for concurrent use.
    /// </summary>
    public interface IEditionPublicationMethods
    {
        /// <summary>
        /// Enumerate links in edition order (sortOrder, then id). TenantId on the filter is required.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>One page of results.</returns>
        /// <exception cref="System.ArgumentNullException">filter or filter.TenantId is null.</exception>
        Task<EnumerationResult<EditionPublication>> EnumerateAsync(EditionPublicationFilter filter, CancellationToken token = default);

        /// <summary>
        /// Replace an edition's publications with this ordered list. Callers validate access first. Repeating the same
        /// list is idempotent.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="editionId">Edition.</param>
        /// <param name="publicationIds">Publication IDs in order, without duplicates. Empty removes every link.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The links after the change, in order.</returns>
        /// <exception cref="System.ArgumentNullException">An argument is null or empty.</exception>
        Task<List<EditionPublication>> ReplaceAsync(string tenantId, string editionId, List<string> publicationIds, CancellationToken token = default);
    }
}
