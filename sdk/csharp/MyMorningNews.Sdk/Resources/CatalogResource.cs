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
    public class CatalogResource : ResourceBase
    {
        /// <summary>Bind the resource to a client.</summary>
        public CatalogResource(MyMorningNewsClient client) : base(client) { }

        /// <summary>Browse published system briefings and account eligibility. subscribed true returns only active subscriptions, false only the rest; sortBy accepts title.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<EnumerationResult<CatalogEntryResponse>> ListAsync(CatalogListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<CatalogEntryResponse>>(HttpMethod.Get, "/v1.0/api/catalog", (options ?? new CatalogListOptions()).ToQuery(), null, token);
        }

        /// <summary>List private subscriptions, including suspended subscriptions.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<EnumerationResult<CatalogSubscription>> SubscriptionsAsync(ListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<CatalogSubscription>>(HttpMethod.Get, "/v1.0/api/catalog/subscriptions", (options ?? new ListOptions()).ToQuery(), null, token);
        }

        /// <summary>Subscribe or update delivery preferences. Separate system subscription limits apply.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<CatalogSubscription> SubscribeAsync(string id, CatalogSubscriptionRequest body, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(body);
            return Client.SendAsync<CatalogSubscription>(HttpMethod.Put, "/v1.0/api/catalog/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/subscription", null, body, token);
        }

        /// <summary>Unsubscribe without deleting canonical content.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task UnsubscribeAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/catalog/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/subscription", null, null, token);
        }

        /// <summary>List issues readable through this subscription.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<EnumerationResult<CatalogIssueResponse>> IssuesAsync(string id, ListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<CatalogIssueResponse>>(HttpMethod.Get, "/v1.0/api/catalog/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/issues", (options ?? new ListOptions()).ToQuery(), null, token);
        }

        /// <summary>List shared issues across all current subscriptions.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<EnumerationResult<CatalogIssueResponse>> AllIssuesAsync(CatalogIssueListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<CatalogIssueResponse>>(HttpMethod.Get, "/v1.0/api/catalog/issues", (options ?? new CatalogIssueListOptions()).ToQuery(), null, token);
        }

        /// <summary>Read shared content without exposing canonical workspace internals.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<CatalogIssueResponse> IssueAsync(string id, string issueId, CancellationToken token = default)
        {
            return Client.SendAsync<CatalogIssueResponse>(HttpMethod.Get, "/v1.0/api/catalog/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/issues/" + MyMorningNewsClient.Segment(issueId, nameof(issueId)), null, null, token);
        }

        /// <summary>Download authenticated shared audio. Playback does not consume generation allowance.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<DownloadResult> AudioAsync(string id, string issueId, ByteRange? range = null, CancellationToken token = default)
        {
            return Client.DownloadAsync(HttpMethod.Get, "/v1.0/api/catalog/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/issues/" + MyMorningNewsClient.Segment(issueId, nameof(issueId)) + "/audio", null, null, range, null, token);
        }

        /// <summary>Read private listening progress.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<BriefingProgress> ProgressAsync(string id, string issueId, CancellationToken token = default)
        {
            return Client.SendAsync<BriefingProgress>(HttpMethod.Get, "/v1.0/api/catalog/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/issues/" + MyMorningNewsClient.Segment(issueId, nameof(issueId)) + "/progress", null, null, token);
        }

        /// <summary>Save private listening progress for a shared issue.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<BriefingProgress> SaveProgressAsync(string id, string issueId, BriefingProgressRequest body, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(body);
            return Client.SendAsync<BriefingProgress>(HttpMethod.Put, "/v1.0/api/catalog/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/issues/" + MyMorningNewsClient.Segment(issueId, nameof(issueId)) + "/progress", null, body, token);
        }
    }
}
