namespace MyMorningNews.Sdk.Resources
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// The caller's plan and promotional codes.
    /// </summary>
    public class AccountResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public AccountResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// The caller's plan and its limits.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The EffectivePlan.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EffectivePlan> GetPlanAsync(CancellationToken token = default)
        {
            return Client.SendAsync<EffectivePlan>(HttpMethod.Get, "/v1.0/api/account/plan", null, null, token);
        }

        /// <summary>
        /// Redeem a promotional code (409 PromoCodeInvalid with details.reason).
        /// </summary>
        /// <param name="code">Promotional code.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The EffectivePlan.</returns>
        /// <exception cref="ArgumentNullException">code is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EffectivePlan> RedeemAsync(string code, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(code)) throw new ArgumentNullException(nameof(code));
            return Client.SendAsync<EffectivePlan>(HttpMethod.Post, "/v1.0/api/promo-codes/redeem", null, new PromoRedeemRequest { Code = code }, token);
        }

        /// <summary>
        /// Check a promotional code without redeeming it (public).
        /// </summary>
        /// <param name="code">Promotional code.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The PromoCheck.</returns>
        /// <exception cref="ArgumentNullException">code is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<PromoCheck> CheckPromoCodeAsync(string code, CancellationToken token = default)
        {
            return Client.SendAsync<PromoCheck>(HttpMethod.Get, "/v1.0/public/promo-codes/" + MyMorningNewsClient.Segment(code, nameof(code)), null, null, token);
        }
    }
}
