namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>Platform transport contracts: scoped paths, private playback, nullable billing state and explicit zero limits.</summary>
    public class PlatformWireTest : TestBase
    {
        /// <inheritdoc />
        public override string Name { get { return "Offline: platform catalog and billing wire contracts"; } }
        /// <inheritdoc />
        public override bool RequiresServer { get { return false; } }

        /// <inheritdoc />
        public override async Task RunAsync(TestContext context, TestResult result, CancellationToken token)
        {
            using (PlatformWireHandler handler = new PlatformWireHandler())
            using (HttpClient http = new HttpClient(handler))
            using (MyMorningNewsClient client = new MyMorningNewsClient("https://example.invalid", http))
            {
                client.Token = "member";
                handler.Reply = "{\"enabled\":false,\"plan\":{\"subscriptionRequired\":false,\"features\":{\"customTopics.max\":\"0\"}},\"offers\":[{\"key\":\"paid-5-year\",\"usdCents\":10000}],\"subscriptions\":[{\"endsUtc\":null}]}";
                BillingStatusResponse billing = await client.Billing.StatusAsync(token).ConfigureAwait(false);
                Helpers.True(!billing.Enabled && !billing.Plan.SubscriptionRequired, "non-billed policy survives deserialization");
                Helpers.Equal(10000, billing.Offers[0].UsdCents, "annual price");
                Helpers.True(billing.Subscriptions[0].EndsUtc == null, "unknown end is not a fake expiry");
                Helpers.Equal("Bearer member", handler.Authorization, "account-bound bearer");
                handler.Reply = "{\"items\":[{\"id\":\"c1\",\"eligible\":true}],\"totalCount\":1}";
                EnumerationResult<CatalogEntryResponse> catalog = await client.Catalog.ListAsync(new CatalogListOptions { Category = "AI & science", PageSize = 3 }, token).ConfigureAwait(false);
                Helpers.True(catalog.Items[0].Eligible, "public eligibility");
                Helpers.True(handler.Path.Contains("category=AI%20%26%20science", StringComparison.Ordinal), "encoded category");
                handler.Reply = "{\"positionSeconds\":12.5,\"completed\":false}";
                BriefingProgress progress = await client.Catalog.SaveProgressAsync("cat/one", "issue?one", new BriefingProgressRequest { PositionSeconds = 12.5, Completed = false }, token).ConfigureAwait(false);
                Helpers.Equal(12.5, progress.PositionSeconds, "fractional private progress");
                Helpers.Equal("/v1.0/api/catalog/cat%2Fone/issues/issue%3Fone/progress", handler.Path, "segments cannot escape their route");
                handler.Reply = "{}";
                await client.AdminEntitlements.SetAsync("t1", "u1", new EntitlementOverrideRequest { FeatureKey = "audio.dailySeconds.max", Value = "0", Reason = "Account restriction" }, token).ConfigureAwait(false);
                Helpers.True(handler.Body.Contains("\"value\":\"0\"", StringComparison.Ordinal), "zero override preserved");
                await client.AdminCatalog.SaveResearchAsync("c1", new CatalogResearchRequest { Topic = new TopicUpsertRequest { HistoryMode = HistoryModeEnum.Off, AudioEnabled = false } }, token).ConfigureAwait(false);
                Helpers.True(handler.Body.Contains("\"historyMode\":\"Off\"", StringComparison.Ordinal), "standalone comparison writes enum name");
                Helpers.True(typeof(ProviderConnection).GetProperty("SecretEncrypted") == null, "write-only encrypted secret is absent from SDK model");
                handler.Status = HttpStatusCode.NoContent;
                await client.Catalog.UnsubscribeAsync("c1", token).ConfigureAwait(false);
            }
        }
    }
}
