namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Podcast feed tokens: create (secret returned once), list (hash redacted), revoke, purge, and 404 after purge.
    /// </summary>
    public class FeedTokensTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Feed tokens create, list, revoke, purge";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public FeedTokensTest()
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
            await StepAsync(context, result, "POST feed token for an unknown edition (400)", () => Helpers.ExpectErrorAsync(() => c.FeedTokens.CreateAsync(new FeedTokenCreateRequest { EditionId = "edi_missing" }, token), 400)).ConfigureAwait(false);
            FeedTokenCreated created = await StepAsync(context, result, "POST feed token", () => c.FeedTokens.CreateAsync(new FeedTokenCreateRequest { Name = context.Name("feed") }, token)).ConfigureAwait(false);
            string id = created.Token.Id;
            context.Cleanup.Add("feedtoken:" + id, async ct =>
            {
                await c.FeedTokens.DeleteAsync(id, false, ct).ConfigureAwait(false);
                await c.FeedTokens.DeleteAsync(id, true, ct).ConfigureAwait(false);
            });
            Helpers.NotEmpty(created.Secret, "secret");
            Helpers.True(created.FeedUrl.Contains("/v1.0/public/feeds/", System.StringComparison.Ordinal), "feed URL");
            Helpers.Equal("redacted", created.Token.TokenHash, "hash is redacted");

            List<FeedToken> list = (await StepAsync(context, result, "GET feed tokens", () => c.FeedTokens.ListAsync(options: new MyMorningNews.Sdk.Options.EnumerationQuery { PageSize = 1000 }, token: token)).ConfigureAwait(false)).Objects;
            FeedToken? mine = list.FirstOrDefault(t => t.Id == id);
            Helpers.True(mine != null && mine.TokenHash == "redacted" && mine.RevokedUtc == null, "the active token, hash redacted");

            await StepAsync(context, result, "DELETE feed token (revoke)", () => c.FeedTokens.DeleteAsync(id, false, token)).ConfigureAwait(false);
            List<FeedToken> afterRevoke = (await StepAsync(context, result, "GET feed tokens after revoke", () => c.FeedTokens.ListAsync(options: new MyMorningNews.Sdk.Options.EnumerationQuery { PageSize = 1000 }, token: token)).ConfigureAwait(false)).Objects;
            Helpers.True(afterRevoke.Any(t => t.Id == id && t.RevokedUtc != null), "the token is revoked");
            await StepAsync(context, result, "DELETE feed token (purge)", () => c.FeedTokens.DeleteAsync(id, true, token)).ConfigureAwait(false);
            context.Cleanup.Remove("feedtoken:" + id);
            List<FeedToken> afterPurge = (await StepAsync(context, result, "GET feed tokens after purge", () => c.FeedTokens.ListAsync(options: new MyMorningNews.Sdk.Options.EnumerationQuery { PageSize = 1000 }, token: token)).ConfigureAwait(false)).Objects;
            Helpers.DoesNotContain(afterPurge, t => t.Id == id, "the purged token");
            await StepAsync(context, result, "DELETE purged feed token (404)", () => Helpers.ExpectErrorAsync(() => c.FeedTokens.DeleteAsync(id, true, token), 404)).ConfigureAwait(false);
        }
    }
}
