namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;

    /// <summary>Live platform contracts. Read-only; never opens checkout or sends provider credentials.</summary>
    public class PlatformLiveTest : TestBase
    {
        /// <inheritdoc />
        public override string Name => "Live catalog, billing, private providers and daily usage contracts";

        /// <inheritdoc />
        public override async Task RunAsync(TestContext context, TestResult result, CancellationToken token)
        {
            MyMorningNewsClient client = context.RequireAdmin();
            BillingStatusResponse billing = await client.Billing.StatusAsync(token).ConfigureAwait(false);
            Helpers.True(Guid.TryParse(billing.PurchaseAccountToken, out Guid ignored), "native binding is a UUID");
            Helpers.Equal(billing.PurchaseAccountToken, (await client.Billing.StatusAsync(token).ConfigureAwait(false)).PurchaseAccountToken, "binding remains stable");
            Helpers.True(billing.Offers.Count > 0, "configured packages have typed offers");
            ProviderConnectionsResponse providers = await client.Providers.ListAsync(token).ConfigureAwait(false);
            Helpers.NotEmpty(providers.Plan.Plan.Id, "provider response carries effective plan");
            EnumerationResult<CatalogEntryResponse> catalog = await client.Catalog.ListAsync(new CatalogListOptions { PageSize = 2 }, token).ConfigureAwait(false);
            Helpers.True(catalog.Objects.Count <= 2, "catalog pagination is honored");
            await client.Catalog.SubscriptionsAsync(null, token).ConfigureAwait(false);
            await client.Catalog.AllIssuesAsync(null, token).ConfigureAwait(false);
            UsageMeter usage = await client.Account.GetUsageAsync(token).ConfigureAwait(false);
            Helpers.True(usage.AudioGeneratedSecondsToday >= 0 && usage.SystemSubscriptions >= 0, "separate daily generation and catalog usage fields");
        }
    }
}
