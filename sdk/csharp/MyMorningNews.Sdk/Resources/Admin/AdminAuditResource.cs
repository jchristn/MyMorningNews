namespace MyMorningNews.Sdk.Resources.Admin
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Resources;

    /// <summary>
    /// Audit records.
    /// </summary>
    public class AdminAuditResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public AdminAuditResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List audit records.
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of AuditRecord items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<AuditRecord>> ListAsync(AuditListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<AuditRecord>>(HttpMethod.Get, "/v1.0/api/audit", (options ?? new AuditListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read an audit record.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The AuditRecord.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<AuditRecord> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<AuditRecord>(HttpMethod.Get, "/v1.0/api/audit/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Delete an audit record.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/audit/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Delete audit records created before a cutoff.
        /// </summary>
        /// <param name="toUtc">Cutoff.</param>
        /// <param name="tenantId">Global administrators: only this tenant, or null for every tenant.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The DeleteManyResponse.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<DeleteManyResponse> DeleteOlderThanAsync(DateTime toUtc, string? tenantId = null, CancellationToken token = default)
        {
            return Client.SendAsync<DeleteManyResponse>(HttpMethod.Delete, "/v1.0/api/audit", Query("toUtc", toUtc, "tenantId", tenantId), null, token);
        }
    }
}
