namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Public share links for a briefing: the 1 to 14 day expiry rule, create (links returned once), list (hash
    /// redacted), revoke, purge, and 404 after purge.
    /// </summary>
    public class BriefingSharesTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Briefing shares create, list, revoke, purge";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public BriefingSharesTest()
        {
        }

        /// <summary>
        /// Run the case.
        /// </summary>
        /// <param name="context">Shared state.</param>
        /// <param name="result">Result.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public override async Task RunAsync(TestContext context, TestResult result, CancellationToken token)
        {
            MyMorningNewsClient c = context.RequireAdmin();
            await StepAsync(context, result, "GET shares of an unknown briefing (404)", () => Helpers.ExpectErrorAsync(() => c.Briefings.ListSharesAsync("brf_missing", null, token), 404)).ConfigureAwait(false);
            await StepAsync(context, result, "POST share of an unknown briefing (404)", () => Helpers.ExpectErrorAsync(() => c.Briefings.ShareAsync("brf_missing", null, token), 404)).ConfigureAwait(false);
            ClientConfigResponse config = await StepAsync(context, result, "GET client config", () => c.GetClientConfigAsync(token)).ConfigureAwait(false);
            EnumerationResult<Briefing> page = await StepAsync(context, result, "GET briefings (ready)",
                () => c.Briefings.ListAsync(new BriefingListOptions { Status = BriefingStatusEnum.Ready, PageSize = 1 }, token)).ConfigureAwait(false);
            Briefing? ready = page.Items.FirstOrDefault();
            if (ready == null || !config.SharingEnabled) return;

            int max = config.SharingMaxExpiryDays;
            await StepAsync(context, result, "POST share above the longest expiry (400)", () => Helpers.ExpectErrorAsync(() => c.Briefings.ShareAsync(ready.Id, new BriefingShareCreateRequest { ExpiresInDays = max + 1 }, token), 400)).ConfigureAwait(false);
            await StepAsync(context, result, "POST share with zero days (400)", () => Helpers.ExpectErrorAsync(() => c.Briefings.ShareAsync(ready.Id, new BriefingShareCreateRequest { ExpiresInDays = 0 }, token), 400)).ConfigureAwait(false);
            BriefingShareCreated created = await StepAsync(context, result, "POST share at the longest expiry", () => c.Briefings.ShareAsync(ready.Id, new BriefingShareCreateRequest { ExpiresInDays = max }, token)).ConfigureAwait(false);
            string id = created.Share.Id;
            context.Cleanup.Add("share:" + id, async ct =>
            {
                await c.Briefings.RevokeShareAsync(ready.Id, id, false, ct).ConfigureAwait(false);
                await c.Briefings.RevokeShareAsync(ready.Id, id, true, ct).ConfigureAwait(false);
            });
            Helpers.NotEmpty(created.Secret, "secret");
            Helpers.True(created.ReadUrl.Contains("/v1.0/public/shares/", System.StringComparison.Ordinal) && created.ListenUrl == created.ReadUrl + "/listen", "reading and listening links");
            Helpers.Equal("redacted", created.Share.TokenHash, "hash is redacted");
            Helpers.True(created.Share.ExpiresUtc > System.DateTime.UtcNow.AddDays(max - 1) && !created.Share.ShowOwnerName, "expiry stored, name hidden by default");

            EnumerationResult<BriefingShare> list = await StepAsync(context, result, "GET shares", () => c.Briefings.ListSharesAsync(ready.Id, null, token)).ConfigureAwait(false);
            BriefingShare? mine = list.Items.FirstOrDefault(s => s.Id == id);
            Helpers.True(mine != null && mine.TokenHash == "redacted" && mine.RevokedUtc == null && mine.ViewCount == 0, "the live share, hash redacted, not opened");

            await StepAsync(context, result, "DELETE share (revoke)", () => c.Briefings.RevokeShareAsync(ready.Id, id, false, token)).ConfigureAwait(false);
            EnumerationResult<BriefingShare> afterRevoke = await StepAsync(context, result, "GET shares after revoke", () => c.Briefings.ListSharesAsync(ready.Id, null, token)).ConfigureAwait(false);
            Helpers.True(afterRevoke.Items.Any(s => s.Id == id && s.RevokedUtc != null), "the share is revoked");
            await StepAsync(context, result, "DELETE share (purge)", () => c.Briefings.RevokeShareAsync(ready.Id, id, true, token)).ConfigureAwait(false);
            context.Cleanup.Remove("share:" + id);
            await StepAsync(context, result, "DELETE purged share (404)", () => Helpers.ExpectErrorAsync(() => c.Briefings.RevokeShareAsync(ready.Id, id, true, token), 404)).ConfigureAwait(false);
        }
    }
}
