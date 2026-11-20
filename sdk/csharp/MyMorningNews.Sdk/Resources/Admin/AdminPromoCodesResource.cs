namespace MyMorningNews.Sdk.Resources.Admin
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;
    using MyMorningNews.Sdk.Resources;

    /// <summary>
    /// Promotional codes (global administrators).
    /// </summary>
    public class AdminPromoCodesResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public AdminPromoCodesResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List promotional codes with usage.
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of PromoCodeStats items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<PromoCodeStats>> ListAsync(PromoCodeListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<PromoCodeStats>>(HttpMethod.Get, "/v1.0/api/admin/promo-codes", (options ?? new PromoCodeListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read a promotional code with usage.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The PromoCodeStats.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<PromoCodeStats> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<PromoCodeStats>(HttpMethod.Get, "/v1.0/api/admin/promo-codes/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Create a promotional code.
        /// </summary>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The PromoCodeStats.</returns>
        /// <exception cref="ArgumentNullException">request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<PromoCodeStats> CreateAsync(PromoCodeUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<PromoCodeStats>(HttpMethod.Post, "/v1.0/api/admin/promo-codes", null, request, token);
        }

        /// <summary>
        /// Generate promotional codes in bulk.
        /// </summary>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of PromoCode items.</returns>
        /// <exception cref="ArgumentNullException">request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<List<PromoCode>> GenerateAsync(PromoCodeGenerateRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<List<PromoCode>>(HttpMethod.Post, "/v1.0/api/admin/promo-codes/generate", null, request, token);
        }

        /// <summary>
        /// Update a promotional code (limits, dates, plan, on or off).
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The PromoCodeStats.</returns>
        /// <exception cref="ArgumentNullException">id or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<PromoCodeStats> UpdateAsync(string id, PromoCodeUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<PromoCodeStats>(HttpMethod.Put, "/v1.0/api/admin/promo-codes/" + MyMorningNewsClient.Segment(id, nameof(id)), null, request, token);
        }

        /// <summary>
        /// Delete an unused promotional code; with force, also revoke and remove its redemptions.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="force">Delete a used code too.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string id, bool force = false, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/admin/promo-codes/" + MyMorningNewsClient.Segment(id, nameof(id)), Query("force", force ? (object)true : null), null, token);
        }

        /// <summary>
        /// Who redeemed a code and when.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of PromoRedemption items.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<List<PromoRedemption>> ListRedemptionsAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<List<PromoRedemption>>(HttpMethod.Get, "/v1.0/api/admin/promo-codes/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/redemptions", null, null, token);
        }

        /// <summary>
        /// Revoke a redemption (ends its plan now).
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="redemptionId">Redemption identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The PromoRedemption.</returns>
        /// <exception cref="ArgumentNullException">id or redemptionId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<PromoRedemption> RevokeRedemptionAsync(string id, string redemptionId, CancellationToken token = default)
        {
            return Client.SendAsync<PromoRedemption>(HttpMethod.Post, "/v1.0/api/admin/promo-codes/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/redemptions/" + MyMorningNewsClient.Segment(redemptionId, nameof(redemptionId)) + "/revoke", null, null, token);
        }

        /// <summary>
        /// Redeem a code on behalf of a user.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The PromoRedemption.</returns>
        /// <exception cref="ArgumentNullException">id or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<PromoRedemption> ApplyAsync(string id, PromoApplyRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<PromoRedemption>(HttpMethod.Post, "/v1.0/api/admin/promo-codes/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/apply", null, request, token);
        }
    }
}
