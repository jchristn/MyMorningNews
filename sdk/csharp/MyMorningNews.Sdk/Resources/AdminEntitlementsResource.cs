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
    public class AdminEntitlementsResource : ResourceBase
    {
        /// <summary>Bind the resource to a client.</summary>
        public AdminEntitlementsResource(MyMorningNewsClient client) : base(client) { }

        /// <summary>System administrator: inspect effective user allowances and provenance.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<EffectivePlan> GetAsync(string tenantId, string userId, CancellationToken token = default)
        {
            return Client.SendAsync<EffectivePlan>(HttpMethod.Get, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/users/" + MyMorningNewsClient.Segment(userId, nameof(userId)) + "/entitlements", null, null, token);
        }

        /// <summary>System administrator: override one limit with an audit reason and optional expiry.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<EffectivePlan> SetAsync(string tenantId, string userId, EntitlementOverrideRequest body, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(body);
            return Client.SendAsync<EffectivePlan>(HttpMethod.Put, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/users/" + MyMorningNewsClient.Segment(userId, nameof(userId)) + "/entitlements", null, body, token);
        }

        /// <summary>System administrator: remove an override and restore plan inheritance.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task ClearAsync(string tenantId, string userId, string featureKey, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/users/" + MyMorningNewsClient.Segment(userId, nameof(userId)) + "/entitlements/" + MyMorningNewsClient.Segment(featureKey, nameof(featureKey)), null, null, token);
        }
    }
}
