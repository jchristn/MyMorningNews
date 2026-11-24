namespace Test.Shared.Suites
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Briefings;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Server;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>Historical context ownership, selection, bounded snapshots and off semantics.</summary>
    public static class HistorySuite
    {
        /// <summary>Build comparison tests.</summary>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor("History", "Historical briefing comparison", new[]
            {
                Cases.Case("History", "Snapshots", "Baselines are scoped, exclude previews, bounded, and immutable across retries", SnapshotsAsync),
                Cases.Case("History", "Standalone", "Comparison off does not suppress current information on repeated runs", StandaloneAsync),
                Cases.Case("History", "Validation", "Invalid history controls never leave a draft behind", ValidationAsync)
            });
        }

        private static async Task SnapshotsAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            Tenant tenant = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser member = await TestFixtures.CreateUserAsync(admin, tenant.Id, false, token).ConfigureAwait(false);
            Topic topic = await server.Context.Refinement.CreateTopicAsync(tenant.Id, member.User.Id, "Comparison", null, false, token).ConfigureAwait(false);
            HistoryContextService history = new HistoryContextService(server.Context.Database);
            Briefing issue = await server.Context.Database.Briefings.CreateAsync(new Briefing { TenantId = tenant.Id, UserId = member.User.Id, TopicId = topic.Id, Title = "Baseline", Markdown = String.Concat(Enumerable.Repeat("Evidence 🌞 ", 300)), Status = BriefingStatusEnum.Ready }, token).ConfigureAwait(false);
            await server.Context.Database.Briefings.CreateAsync(new Briefing { TenantId = tenant.Id, UserId = member.User.Id, TopicId = topic.Id, Title = "Preview", Markdown = "Must not become a baseline", Status = BriefingStatusEnum.Ready, IsDryRun = true }, token).ConfigureAwait(false);
            Run offRun = await server.Context.Database.Runs.CreateAsync(new Run { TenantId = tenant.Id, UserId = member.User.Id, TopicId = topic.Id }, token).ConfigureAwait(false);
            RunHistorySnapshot off = await history.ResolveAsync(topic, offRun, token).ConfigureAwait(false);
            Assert.True(off.Mode == "Off" && off.BaselineIds.Count == 0 && off.ContextText.Length == 0, "new topics are standalone");
            topic.HistoryMode = HistoryModeEnum.PreviousIssue;
            topic.HistoryTokenBudget = 256;
            Run compareRun = await server.Context.Database.Runs.CreateAsync(new Run { TenantId = tenant.Id, UserId = member.User.Id, TopicId = topic.Id }, token).ConfigureAwait(false);
            RunHistorySnapshot snapshot = await history.ResolveAsync(topic, compareRun, token).ConfigureAwait(false);
            Assert.True(snapshot.BaselineIds.SequenceEqual(new[] { issue.Id }) && snapshot.Truncated && Encoding.UTF8.GetByteCount(snapshot.ContextText) <= 256, "latest ready issue bounded without preview");
            Assert.True(!snapshot.ContextText.Contains('\uFFFD'), "unicode scalar preserved");
            string original = snapshot.ContextText;
            issue.Markdown = "Changed later";
            await server.Context.Database.Briefings.UpdateAsync(issue, token).ConfigureAwait(false);
            topic.HistoryMode = HistoryModeEnum.Off;
            RunHistorySnapshot retry = await history.ResolveAsync(topic, compareRun, token).ConfigureAwait(false);
            Assert.True(retry.Id == snapshot.Id && retry.ContextText == original && retry.ContextHash == snapshot.ContextHash, "retry uses the frozen original");
            Assert.True(HistoryContextService.Prompt(retry).Contains("untrusted", StringComparison.Ordinal), "baseline treated as untrusted data");
            topic.HistoryMode = HistoryModeEnum.SinceDate;
            topic.HistorySinceUtc = DateTime.UtcNow;
            Run first = await server.Context.Database.Runs.CreateAsync(new Run { TenantId = tenant.Id, UserId = member.User.Id, TopicId = topic.Id }, token).ConfigureAwait(false);
            RunHistorySnapshot empty = await history.ResolveAsync(topic, first, token).ConfigureAwait(false);
            Assert.True(empty.BaselineIds.Count == 0 && empty.Note != null, "no baseline yields explained standalone output");
        }

        private static async Task StandaloneAsync(CancellationToken token)
        {
            PipelineFixture fixture = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            Run first = await PipelineSuite.RunAsync(fixture, token).ConfigureAwait(false);
            Run second = await PipelineSuite.RunAsync(fixture, token).ConfigureAwait(false);
            Assert.True(first.NewFindingCount > 0 && second.NewFindingCount > 0 && second.DuplicateCount == 0, "standalone keeps current evidence on repeat");
        }

        private static async Task ValidationAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            Tenant tenant = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser member = await TestFixtures.CreateUserAsync(admin, tenant.Id, false, token).ConfigureAwait(false);
            (await member.Client.PostAsync("/v1.0/api/topics", new { name = "Invalid", historyMode = "SinceDate", historySinceUtc = DateTime.UtcNow.AddDays(2) }, token).ConfigureAwait(false)).Expect(400, "future baseline rejected");
            (await member.Client.PostAsync("/v1.0/api/topics", new { name = "Invalid", historyMode = "SpecificIssue", historyBriefingId = "brf_nonexistent" }, token).ConfigureAwait(false)).Expect(400, "unreadable baseline rejected");
            Assert.Equal(0L, await server.Context.Database.Topics.CountAsync(new TopicFilter { TenantId = tenant.Id, UserId = member.User.Id }, token).ConfigureAwait(false), "failed validation creates no rows");
        }
    }
}
