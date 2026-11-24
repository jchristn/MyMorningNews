namespace MyMorningNews.Sdk.Resources
{
    using System;
    using System.Net.Http;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>Platform account/catalog API. Stateless and safe to share; server authorization is authoritative.</summary>
    public class BillingResource : ResourceBase
    {
        /// <summary>Bind the resource to a client.</summary>
        public BillingResource(MyMorningNewsClient client) : base(client) { }

        /// <summary>Read offers, native account binding, verified subscriptions and effective access.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<BillingStatusResponse> StatusAsync(CancellationToken token = default)
        {
            return Client.SendAsync<BillingStatusResponse>(HttpMethod.Get, "/v1.0/api/account/billing", null, null, token);
        }

        /// <summary>Create or resume one account-bound Stripe checkout. A redirect never grants access.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<BillingRedirectResponse> CheckoutAsync(BillingCheckoutRequest body, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(body);
            return Client.SendAsync<BillingRedirectResponse>(HttpMethod.Post, "/v1.0/api/account/billing/checkout", null, body, token);
        }

        /// <summary>Open management for the verified Stripe customer.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<BillingRedirectResponse> PortalAsync(CancellationToken token = default)
        {
            return Client.SendAsync<BillingRedirectResponse>(HttpMethod.Post, "/v1.0/api/account/billing/portal", null, null, token);
        }

        /// <summary>Verify or restore an account-bound Apple or Google purchase.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<EffectivePlan> VerifyPurchaseAsync(BillingPurchaseRequest body, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(body);
            return Client.SendAsync<EffectivePlan>(HttpMethod.Post, "/v1.0/api/account/billing/purchases", null, body, token);
        }
    }
}
