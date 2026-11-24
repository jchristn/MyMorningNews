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
    public class ProvidersResource : ResourceBase
    {
        /// <summary>Bind the resource to a client.</summary>
        public ProvidersResource(MyMorningNewsClient client) : base(client) { }

        /// <summary>List only this account's masked provider connections.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<ProviderConnectionsResponse> ListAsync(CancellationToken token = default)
        {
            return Client.SendAsync<ProviderConnectionsResponse>(HttpMethod.Get, "/v1.0/api/account/providers", null, null, token);
        }

        /// <summary>Connect or rotate a write-only model or speech API key. Clears validation.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<ProviderConnection> SaveAsync(ProviderConnectionRequest body, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(body);
            return Client.SendAsync<ProviderConnection>(HttpMethod.Put, "/v1.0/api/account/providers", null, body, token);
        }

        /// <summary>Run a fixed small probe with this account's credential. Six probes per minute.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<JsonObject> TestAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<JsonObject>(HttpMethod.Post, "/v1.0/api/account/providers/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/test", null, null, token);
        }

        /// <summary>Remove a private key. BYOK calls fail closed, without platform fallback.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<JsonObject> RemoveAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<JsonObject>(HttpMethod.Delete, "/v1.0/api/account/providers/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>Choose Personal or Platform funding. A verified chat connection is required for BYOK.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<EffectivePlan> SetFundingAsync(FundingModeRequest body, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(body);
            return Client.SendAsync<EffectivePlan>(HttpMethod.Put, "/v1.0/api/account/funding", null, body, token);
        }
    }
}
