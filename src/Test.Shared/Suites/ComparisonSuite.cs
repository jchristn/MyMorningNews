namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Briefings;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Server;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Comparison provenance, baseline previews, edition comparison, system briefings inside custom editions, and the
    /// downgrade keep order (platform plan sections 5.2, 6 and 8.4).
    /// </summary>
    public static class ComparisonSuite
    {
        private const string S = "Comparison";

        /// <summary>Build the suite.</summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Comparison provenance, editions, and topic slots", new[]
            {
                Cases.Case(S, "Preview", "Baseline previews show the selected issues and unsaved settings without saving anything", PreviewAsync),
                Cases.Case(S, "Deltas", "Changes link current evidence to baseline items: new, changed, corrected, resolved, unchanged, unavailable", DeltasAsync),
                Cases.Case(S, "Edition", "Editions compare with their own earlier issues once per briefing and say so in the text", EditionAsync),
                Cases.Case(S, "Publications", "Custom editions include subscribed system issues without starting system research", PublicationsAsync),
                Cases.Case(S, "Slots", "The keep order decides which topics run after a downgrade and never deletes anything", SlotsAsync)
            });
        }

        private static async Task PreviewAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            Tenant tenant = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser member = await TestFixtures.CreateUserAsync(admin, tenant.Id, false, token).ConfigureAwait(false);
            TestUser other = await TestFixtures.CreateUserAsync(admin, tenant.Id, false, token).ConfigureAwait(false);
            Topic topic = await server.Context.Refinement.CreateTopicAsync(tenant.Id, member.User.Id, "Preview topic", null, false, token).ConfigureAwait(false);
            string path = "/v1.0/api/topics/" + topic.Id + "/history/preview";
            HistoryPreviewResponse off = (await member.Client.PostAsync(path, null, token).ConfigureAwait(false)).Expect(200, "saved settings").Json<HistoryPreviewResponse>();
            Assert.True(off.Mode == "Off" && off.Baselines.Count == 0 && off.Note != null, "off is standalone and explained");
            Briefing older = await server.Context.Database.Briefings.CreateAsync(new Briefing { TenantId = tenant.Id, UserId = member.User.Id, TopicId = topic.Id, Title = "Older", Markdown = "Older issue", Status = BriefingStatusEnum.Ready, Kind = BriefingKindEnum.Run }, token).ConfigureAwait(false);
            Briefing newer = await server.Context.Database.Briefings.CreateAsync(new Briefing { TenantId = tenant.Id, UserId = member.User.Id, TopicId = topic.Id, Title = "Newer", Markdown = "Newer issue", Status = BriefingStatusEnum.Ready, Kind = BriefingKindEnum.Run }, token).ConfigureAwait(false);
            HistoryPreviewResponse recent = (await member.Client.PostAsync(path, new { historyMode = "RecentIssues", historyCount = 2 }, token).ConfigureAwait(false)).Expect(200, "unsaved recent").Json<HistoryPreviewResponse>();
            Assert.True(recent.Baselines.Select(b => b.Id).SequenceEqual(new[] { newer.Id, older.Id }) && recent.ContextBytes > 0 && !recent.Truncated && recent.Note == null, "newest first, sized");
            HistoryPreviewResponse pinned = (await member.Client.PostAsync(path, new { historyMode = "SpecificIssue", historyBriefingId = older.Id }, token).ConfigureAwait(false)).Expect(200, "pinned").Json<HistoryPreviewResponse>();
            Assert.True(pinned.Baselines.Count == 1 && pinned.Baselines[0].Id == older.Id, "pinned issue");
            (await member.Client.PostAsync(path, new { historyMode = "SpecificIssue", historyBriefingId = "brf_missing" }, token).ConfigureAwait(false)).Expect(400, "unknown pin rejected");
            (await member.Client.PostAsync(path, new { historyMode = "RecentIssues", historyCount = 99 }, token).ConfigureAwait(false)).Expect(400, "bounds enforced");
            (await other.Client.PostAsync(path, null, token).ConfigureAwait(false)).Expect(404, "another member cannot preview");
            Topic saved = Assert.NotNull(await server.Context.Database.Topics.ReadAsync(tenant.Id, topic.Id, token).ConfigureAwait(false), "topic");
            Assert.Equal(HistoryModeEnum.Off, saved.HistoryMode, "preview saves nothing");
        }

        private static async Task DeltasAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            Tenant tenant = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser member = await TestFixtures.CreateUserAsync(admin, tenant.Id, false, token).ConfigureAwait(false);
            TestUser other = await TestFixtures.CreateUserAsync(admin, tenant.Id, false, token).ConfigureAwait(false);
            string t = tenant.Id;
            string u = member.User.Id;
            Topic topic = await server.Context.Refinement.CreateTopicAsync(t, u, "Delta topic", null, false, token).ConfigureAwait(false);
            Run firstRun = await server.Context.Database.Runs.CreateAsync(new Run { TenantId = t, UserId = u, TopicId = topic.Id }, token).ConfigureAwait(false);
            Run secondRun = await server.Context.Database.Runs.CreateAsync(new Run { TenantId = t, UserId = u, TopicId = topic.Id }, token).ConfigureAwait(false);
            Finding priceOld = await Finding(server, firstRun, "Rebate is $2,000", null, false, false, null, token).ConfigureAwait(false);
            Finding dateOld = await Finding(server, firstRun, "Program opens in March", null, false, false, null, token).ConfigureAwait(false);
            Finding sameOld = await Finding(server, firstRun, "Utility runs the program", null, false, false, null, token).ConfigureAwait(false);
            Finding goneOld = await Finding(server, firstRun, "Withdrawn report", null, false, false, null, token).ConfigureAwait(false);
            Prediction forecast = await server.Context.Database.Predictions.CreateAsync(new Prediction { TenantId = t, RunId = firstRun.Id, TopicId = topic.Id, UserId = u, Statement = "Funding runs out by June", HorizonUtc = DateTime.UtcNow.AddDays(30) }, token).ConfigureAwait(false);
            Briefing baseline = await server.Context.Database.Briefings.CreateAsync(new Briefing { TenantId = t, UserId = u, TopicId = topic.Id, RunId = firstRun.Id, Kind = BriefingKindEnum.Run, Title = "Rebates", Markdown = "Baseline issue", Status = BriefingStatusEnum.Ready }, token).ConfigureAwait(false);
            BriefingSection section = await server.Context.Database.BriefingSections.CreateAsync(new BriefingSection { TenantId = t, BriefingId = baseline.Id, SectionKey = "top", Heading = "Top", Source = "findings" }, token).ConfigureAwait(false);
            int order = 0;
            foreach (Finding f in new[] { priceOld, dateOld, sameOld, goneOld })
                await server.Context.Database.BriefingItems.CreateAsync(new BriefingItem { TenantId = t, BriefingId = baseline.Id, SectionId = section.Id, ItemType = BriefingItemTypeEnum.Finding, ItemId = f.Id, SortOrder = order++ }, token).ConfigureAwait(false);
            await server.Context.Database.BriefingItems.CreateAsync(new BriefingItem { TenantId = t, BriefingId = baseline.Id, SectionId = section.Id, ItemType = BriefingItemTypeEnum.Prediction, ItemId = forecast.Id, SortOrder = order++ }, token).ConfigureAwait(false);
            goneOld.Active = false;
            await server.Context.Database.Findings.UpdateAsync(goneOld, token).ConfigureAwait(false);
            forecast.Status = PredictionStatusEnum.Accurate;
            forecast.ResolvedUtc = DateTime.UtcNow;
            await server.Context.Database.Predictions.UpdateAsync(forecast, token).ConfigureAwait(false);

            Finding changed = await Finding(server, secondRun, "Rebate rises to $3,000", priceOld.Id, false, false, null, token).ConfigureAwait(false);
            Finding corrected = await Finding(server, secondRun, "Program opens in April, not March", dateOld.Id, true, false, null, token).ConfigureAwait(false);
            Finding fresh = await Finding(server, secondRun, "New income limits announced", null, false, false, null, token).ConfigureAwait(false);
            await Finding(server, secondRun, "Utility runs the program", null, false, true, sameOld.Id, token).ConfigureAwait(false);
            RunHistorySnapshot snapshot = await server.Context.Database.RunHistorySnapshots.CreateAsync(new RunHistorySnapshot
            { TenantId = t, RunId = secondRun.Id, TopicId = topic.Id, Mode = "PreviousIssue", BaselineIds = new List<string> { baseline.Id }, ContextText = "Baseline issue", ContextHash = "h" }, token).ConfigureAwait(false);

            ComparisonService comparisons = new ComparisonService(server.Context.Database);
            ComparisonResult result = await comparisons.ComputeAsync(t, new[] { snapshot }, new[] { changed, corrected, fresh }, new[] { topic }, secondRun.Id, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddMinutes(1), false, "UTC", token).ConfigureAwait(false);
            Dictionary<BriefingDeltaKindEnum, int> counts = result.Deltas.GroupBy(d => d.Kind).ToDictionary(g => g.Key, g => g.Count());
            Assert.True(counts.GetValueOrDefault(BriefingDeltaKindEnum.ChangedFact) == 1 && counts.GetValueOrDefault(BriefingDeltaKindEnum.Correction) == 1 && counts.GetValueOrDefault(BriefingDeltaKindEnum.NewDevelopment) == 1, "changed, corrected, new: " + String.Join(",", result.Deltas.Select(d => d.Kind)));
            Assert.True(counts.GetValueOrDefault(BriefingDeltaKindEnum.Unchanged) == 1 && counts.GetValueOrDefault(BriefingDeltaKindEnum.Resolved) == 1 && counts.GetValueOrDefault(BriefingDeltaKindEnum.Unavailable) == 1, "unchanged, resolved, unavailable");
            BriefingDelta change = result.Deltas.Single(d => d.Kind == BriefingDeltaKindEnum.ChangedFact);
            Assert.True(change.FindingId == changed.Id && change.BaselineItemId == priceOld.Id && change.BaselineBriefingId == baseline.Id, "change links current and baseline evidence");
            Assert.Equal(BriefingDeltaKindEnum.Correction, result.Deltas[0].Kind, "corrections lead");
            Assert.True(result.Sentence != null && result.Sentence.StartsWith("Compared with Rebates of ", StringComparison.Ordinal) && result.Sentence.Contains("1 new", StringComparison.Ordinal) && result.Sentence.Contains("1 correction", StringComparison.Ordinal), "sentence: " + result.Sentence);
            Assert.True(!result.Sentence!.Contains((char)0x2014), "no em-dash");

            Briefing current = await server.Context.Database.Briefings.CreateAsync(new Briefing { TenantId = t, UserId = u, TopicId = topic.Id, RunId = secondRun.Id, Kind = BriefingKindEnum.Run, Title = "Rebates", Markdown = "Current", Status = BriefingStatusEnum.Ready }, token).ConfigureAwait(false);
            await comparisons.SaveAsync(current, result.Deltas, token).ConfigureAwait(false);
            await comparisons.SaveAsync(current, result.Deltas, token).ConfigureAwait(false);
            BriefingComparisonResponse described = (await member.Client.GetAsync("/v1.0/api/briefings/" + current.Id + "/comparison", token).ConfigureAwait(false)).Expect(200, "comparison").Json<BriefingComparisonResponse>();
            Assert.True(described.Compared && described.Mode == "PreviousIssue" && described.Baselines.Single().Id == baseline.Id && described.Deltas.Count == 6 && !described.NoMaterialChanges, "described from the run snapshot; saving twice replaces");
            Assert.True(described.Counts.Count == 6 && described.Counts["Unchanged"] == 1 && described.Summary != null, "every kind counted");
            (await other.Client.GetAsync("/v1.0/api/briefings/" + current.Id + "/comparison", token).ConfigureAwait(false)).Expect(404, "private to the owner");

            ComparisonResult quiet = await comparisons.ComputeAsync(t, new[] { snapshot }, Array.Empty<Finding>(), new[] { topic }, secondRun.Id, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddMinutes(1), false, "UTC", token).ConfigureAwait(false);
            Assert.True(quiet.Sentence != null && quiet.Sentence.Contains("1 resolved", StringComparison.Ordinal), "resolution is material");
            forecast.Status = PredictionStatusEnum.Open;
            forecast.ResolvedUtc = null;
            await server.Context.Database.Predictions.UpdateAsync(forecast, token).ConfigureAwait(false);
            ComparisonResult none = await comparisons.ComputeAsync(t, new[] { snapshot }, Array.Empty<Finding>(), new[] { topic }, secondRun.Id, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddMinutes(1), false, "UTC", token).ConfigureAwait(false);
            Assert.True(none.Sentence != null && none.Sentence.Contains("no material changes found", StringComparison.Ordinal), "explicit no-change result: " + none.Sentence);
            RunHistorySnapshot offSnapshot = new RunHistorySnapshot { TenantId = t, Mode = "Off", ContextHash = "h" };
            ComparisonResult standalone = await comparisons.ComputeAsync(t, new[] { offSnapshot }, new[] { fresh }, new[] { topic }, secondRun.Id, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, false, "UTC", token).ConfigureAwait(false);
            Assert.True(standalone.Sentence == null && standalone.Deltas.Count == 0, "off records nothing");
            Briefing plain = await server.Context.Database.Briefings.CreateAsync(new Briefing { TenantId = t, UserId = u, Title = "Plain", Markdown = "x", Status = BriefingStatusEnum.Ready, Kind = BriefingKindEnum.Edition }, token).ConfigureAwait(false);
            BriefingComparisonResponse notCompared = (await member.Client.GetAsync("/v1.0/api/briefings/" + plain.Id + "/comparison", token).ConfigureAwait(false)).Expect(200, "standalone").Json<BriefingComparisonResponse>();
            Assert.True(!notCompared.Compared && notCompared.Note != null && notCompared.Deltas.Count == 0, "standalone explained");
        }

        private static async Task EditionAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            (await f.User.Client.PutAsync("/v1.0/api/topics/" + f.Topic.Id, new { historyMode = "PreviousIssue" }, token).ConfigureAwait(false)).Expect(200, "topic compares");
            await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            EditionResponse e = (await f.User.Client.PostAsync("/v1.0/api/editions", new { name = "Compared edition", freshResearchBeforeCompose = false, lookbackHours = 48, historyMode = "PreviousIssue" }, token).ConfigureAwait(false))
                .Expect(201, "create edition").Json<EditionResponse>();
            Assert.Equal(HistoryModeEnum.PreviousIssue, e.Edition.HistoryMode, "edition history saved");
            (await f.User.Client.PutAsync("/v1.0/api/editions/" + e.Edition.Id + "/topics", new { topicIds = new[] { f.Topic.Id } }, token).ConfigureAwait(false)).Expect(200, "topics");
            (await f.User.Client.PutAsync("/v1.0/api/editions/" + e.Edition.Id, new { historyMode = "SpecificIssue", historyBriefingId = "brf_missing" }, token).ConfigureAwait(false)).Expect(400, "edition pin validated");
            HistoryPreviewResponse before = (await f.User.Client.PostAsync("/v1.0/api/editions/" + e.Edition.Id + "/history/preview", null, token).ConfigureAwait(false)).Expect(200, "preview").Json<HistoryPreviewResponse>();
            Assert.True(before.Baselines.Count == 0 && before.Note != null, "first edition is standalone");

            Briefing first = await ComposeAsync(f, e.Edition.Id, token).ConfigureAwait(false);
            Assert.Equal(BriefingStatusEnum.Ready, first.Status, "first edition ready: " + first.ErrorMessage);
            BriefingComparisonResponse firstCmp = (await f.User.Client.GetAsync("/v1.0/api/briefings/" + first.Id + "/comparison", token).ConfigureAwait(false)).Expect(200, "first comparison").Json<BriefingComparisonResponse>();
            Assert.True(!firstCmp.Compared && firstCmp.Mode == "Edition:PreviousIssue" && firstCmp.Note != null, "edition snapshot recorded without a baseline");
            Assert.True(!(first.Markdown ?? String.Empty).Contains("Compared with", StringComparison.Ordinal), "no comparison claim without a baseline");

            await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            HistoryPreviewResponse after = (await f.User.Client.PostAsync("/v1.0/api/editions/" + e.Edition.Id + "/history/preview", null, token).ConfigureAwait(false)).Expect(200, "preview again").Json<HistoryPreviewResponse>();
            Assert.True(after.Baselines.Count == 1 && after.Baselines[0].Id == first.Id, "previous edition previewed");
            Briefing second = await ComposeAsync(f, e.Edition.Id, token).ConfigureAwait(false);
            Assert.Equal(BriefingStatusEnum.Ready, second.Status, "second edition ready: " + second.ErrorMessage);
            BriefingComparisonResponse secondCmp = (await f.User.Client.GetAsync("/v1.0/api/briefings/" + second.Id + "/comparison", token).ConfigureAwait(false)).Expect(200, "second comparison").Json<BriefingComparisonResponse>();
            Assert.True(secondCmp.Compared && secondCmp.Baselines.Single().Id == first.Id && secondCmp.Summary != null, "edition compared with its previous issue");
            Assert.True((second.Markdown ?? String.Empty).Contains("_Compared with ", StringComparison.Ordinal), "the text, and so email and audio, carry the comparison");
            Assert.True(secondCmp.Deltas.All(d => d.BaselineBriefingId == null || d.BaselineBriefingId == first.Id), "deltas refer to the edition baseline only");

            HistoryContextService history = new HistoryContextService(f.Server.Context.Database);
            Edition edition = Assert.NotNull(await f.Server.Context.Database.Editions.ReadAsync(f.User.User.TenantId, e.Edition.Id, token).ConfigureAwait(false), "edition");
            RunHistorySnapshot again = await history.ResolveEditionAsync(edition, second, token).ConfigureAwait(false);
            List<RunHistorySnapshot> saved = (await f.Server.Context.Database.RunHistorySnapshots.EnumerateAsync(new RunHistorySnapshotFilter { TenantId = edition.TenantId, EditionId = edition.Id, PageSize = 10 }, token).ConfigureAwait(false)).Objects;
            Assert.True(saved.Count == 2 && saved.Any(s => s.Id == again.Id && s.BriefingId == second.Id), "one snapshot per edition briefing, reused on retry");
        }

        private static async Task PublicationsAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            CatalogPublication publication = (await admin.PostAsync("/v1.0/api/admin/catalog", new { title = "Grid news " + Guid.NewGuid().ToString("N"), description = "Public scope", freeEligible = true, status = "Published" }, token).ConfigureAwait(false))
                .Expect(201, "publish").Json<CatalogPublication>();
            CatalogPublication unsubscribed = (await admin.PostAsync("/v1.0/api/admin/catalog", new { title = "Other news " + Guid.NewGuid().ToString("N"), description = "Public scope", freeEligible = true, status = "Published" }, token).ConfigureAwait(false))
                .Expect(201, "publish other").Json<CatalogPublication>();
            User owner = await f.Server.Context.Database.CatalogPublications.EnsureOwnerAsync(token).ConfigureAwait(false);
            Briefing issue = await f.Server.Context.Database.Briefings.CreateAsync(new Briefing
            { TenantId = owner.TenantId, UserId = owner.Id, TopicId = publication.TopicId, Kind = BriefingKindEnum.Run, Title = "Grid issue", Markdown = "# Grid issue\n\nTransmission permits doubled this week.\n\n## Details\n\nMore.", Status = BriefingStatusEnum.Ready }, token).ConfigureAwait(false);
            EditionResponse e = (await f.User.Client.PostAsync("/v1.0/api/editions", new { name = "With catalog", freshResearchBeforeCompose = false, lookbackHours = 48 }, token).ConfigureAwait(false)).Expect(201, "edition").Json<EditionResponse>();
            string path = "/v1.0/api/editions/" + e.Edition.Id + "/publications";
            (await f.User.Client.PutAsync(path, new { publicationIds = new[] { publication.Id } }, token).ConfigureAwait(false)).Expect(400, "subscription required");
            (await f.User.Client.PutAsync("/v1.0/api/catalog/" + publication.Id + "/subscription", new { }, token).ConfigureAwait(false)).Expect(200, "subscribe");
            (await f.User.Client.PutAsync(path, new { publicationIds = new[] { publication.Id, publication.Id } }, token).ConfigureAwait(false)).Expect(400, "duplicates rejected");
            (await f.User.Client.PutAsync(path, new { publicationIds = new[] { publication.Id, unsubscribed.Id } }, token).ConfigureAwait(false)).Expect(400, "every publication checked");
            EditionResponse set = (await f.User.Client.PutAsync(path, new { publicationIds = new[] { publication.Id } }, token).ConfigureAwait(false)).Expect(200, "set").Json<EditionResponse>();
            Assert.True(set.Publications.Count == 1 && set.Publications[0].Available && set.Publications[0].Title == publication.Title, "listed and available");
            (await f.User.Client.PutAsync(path, new { publicationIds = new[] { publication.Id } }, token).ConfigureAwait(false)).Expect(200, "idempotent");
            EnumerationResult<EditionPublicationResponse> listed = (await f.User.Client.GetAsync(path, token).ConfigureAwait(false)).Expect(200, "list").Json<EnumerationResult<EditionPublicationResponse>>();
            Assert.True(listed.TotalRecords == 1 && listed.Objects[0].PublicationId == publication.Id, "paged list");
            (await f.User.Client.PostAsync(path + "/enumerate", new { pageSize = 5 }, token).ConfigureAwait(false)).Expect(200, "enumerate");

            long runsBefore = await f.Server.Context.Database.Runs.CountAsync(new RunFilter { TenantId = owner.TenantId, TopicId = publication.TopicId }, token).ConfigureAwait(false);
            Briefing composed = await ComposeAsync(f, e.Edition.Id, token).ConfigureAwait(false);
            Assert.Equal(BriefingStatusEnum.Ready, composed.Status, "composed: " + composed.ErrorMessage);
            string md = composed.Markdown ?? String.Empty;
            Assert.True(md.Contains("## From your system briefings", StringComparison.Ordinal) && md.Contains("Transmission permits doubled this week.", StringComparison.Ordinal)
                && md.Contains("/dashboard/catalog/" + publication.Id + "/issues/" + issue.Id, StringComparison.Ordinal), "system issue summarized and linked: " + md);
            Assert.True(!md.Contains("More.", StringComparison.Ordinal), "only the opening is quoted");
            Assert.Equal(runsBefore, await f.Server.Context.Database.Runs.CountAsync(new RunFilter { TenantId = owner.TenantId, TopicId = publication.TopicId }, token).ConfigureAwait(false), "no system research started");

            (await f.User.Client.DeleteAsync("/v1.0/api/catalog/" + publication.Id + "/subscription", token).ConfigureAwait(false)).Expect(204, "unsubscribe");
            EditionResponse after = (await f.User.Client.GetAsync("/v1.0/api/editions/" + e.Edition.Id, token).ConfigureAwait(false)).Expect(200, "read").Json<EditionResponse>();
            Assert.True(after.Publications.Count == 1 && !after.Publications[0].Available, "unsubscribed publication shown as unavailable");
            Briefing preview = (await f.User.Client.PostAsync("/v1.0/api/editions/" + e.Edition.Id + "/preview", null, token).ConfigureAwait(false)).Expect(200, "preview").Json<Briefing>();
            Assert.True(!(preview.Markdown ?? String.Empty).Contains("Transmission permits", StringComparison.Ordinal), "composition rechecks the subscription");

            string limits = "/v1.0/tenants/" + f.User.User.TenantId + "/users/" + f.User.User.Id + "/entitlements";
            (await f.User.Client.PutAsync("/v1.0/api/catalog/" + publication.Id + "/subscription", new { }, token).ConfigureAwait(false)).Expect(200, "subscribe again");
            (await admin.PutAsync(limits, new { featureKey = "customResearch.enabled", value = "false", reason = "Catalog-only fixture" }, token).ConfigureAwait(false)).Expect(200, "catalog only");
            TestResponse denied = await f.User.Client.PutAsync(path, new { publicationIds = new[] { publication.Id } }, token).ConfigureAwait(false);
            Assert.Equal(402, denied.StatusCode, "custom composition required: " + denied.Body);
            (await f.User.Client.PutAsync(path, new { publicationIds = Array.Empty<string>() }, token).ConfigureAwait(false)).Expect(200, "removing is always allowed");
        }

        private static async Task SlotsAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            Tenant tenant = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser member = await TestFixtures.CreateUserAsync(admin, tenant.Id, false, token).ConfigureAwait(false);
            TestUser other = await TestFixtures.CreateUserAsync(admin, tenant.Id, false, token).ConfigureAwait(false);
            List<Topic> topics = new List<Topic>();
            for (int i = 0; i < 3; i++)
                topics.Add((await member.Client.PostAsync("/v1.0/api/topics", new { name = "Slot topic " + i }, token).ConfigureAwait(false)).Expect(201, "topic " + i).Json<Topic>());
            Topic foreign = (await other.Client.PostAsync("/v1.0/api/topics", new { name = "Not mine" }, token).ConfigureAwait(false)).Expect(201, "other topic").Json<Topic>();
            TopicSlotsResponse open = (await member.Client.GetAsync("/v1.0/api/account/topic-slots", token).ConfigureAwait(false)).Expect(200, "slots").Json<TopicSlotsResponse>();
            Assert.True(open.Counted == 3 && open.OverLimit == 0 && open.Topics.All(x => x.WithinPlan) && open.Topics.Select(x => x.TopicId).SequenceEqual(topics.Select(x => x.Id)), "oldest first by default");

            TopicSlotsResponse chosen = (await member.Client.PutAsync("/v1.0/api/account/topic-slots", new { keepTopicIds = new[] { topics[2].Id, topics[1].Id } }, token).ConfigureAwait(false)).Expect(200, "choose ahead of a downgrade").Json<TopicSlotsResponse>();
            Assert.True(chosen.Customized && chosen.Topics.Select(x => x.TopicId).SequenceEqual(new[] { topics[2].Id, topics[1].Id, topics[0].Id }) && chosen.Topics[0].Chosen && !chosen.Topics[2].Chosen, "keep order first");

            string limits = "/v1.0/tenants/" + tenant.Id + "/users/" + member.User.Id + "/entitlements";
            (await admin.PutAsync(limits, new { featureKey = "topics.max", value = "2", reason = "Downgrade fixture" }, token).ConfigureAwait(false)).Expect(200, "downgrade to two");
            TopicSlotsResponse down = (await member.Client.GetAsync("/v1.0/api/account/topic-slots", token).ConfigureAwait(false)).Expect(200, "after downgrade").Json<TopicSlotsResponse>();
            Assert.True(down.Limit == 2 && down.OverLimit == 1 && down.Topics.Single(x => !x.WithinPlan).TopicId == topics[0].Id, "the unchosen oldest topic is outside the plan");
            Topic oldest = Assert.NotNull(await server.Context.Database.Topics.ReadAsync(tenant.Id, topics[0].Id, token).ConfigureAwait(false), "oldest");
            Topic newest = Assert.NotNull(await server.Context.Database.Topics.ReadAsync(tenant.Id, topics[2].Id, token).ConfigureAwait(false), "newest");
            await server.Context.Entitlements.CheckTopicResearchAsync(newest, member.User.Id, token).ConfigureAwait(false);
            bool refused = false;
            try
            {
                await server.Context.Entitlements.CheckTopicResearchAsync(oldest, member.User.Id, token).ConfigureAwait(false);
            }
            catch (MyMorningNews.Core.Exceptions.CapReachedException)
            {
                refused = true;
            }

            Assert.True(refused, "the run gate follows the keep order");
            (await member.Client.PostAsync("/v1.0/api/topics", new { name = "Fourth" }, token).ConfigureAwait(false)).Expect(402, "over the limit blocks creation");
            Assert.Equal(3L, await server.Context.Database.Topics.CountAsync(new TopicFilter { TenantId = tenant.Id, UserId = member.User.Id }, token).ConfigureAwait(false), "nothing deleted");

            (await member.Client.PutAsync("/v1.0/api/account/topic-slots", new { keepTopicIds = new[] { topics[0].Id, topics[0].Id } }, token).ConfigureAwait(false)).Expect(400, "duplicates rejected");
            (await member.Client.PutAsync("/v1.0/api/account/topic-slots", new { keepTopicIds = new[] { foreign.Id } }, token).ConfigureAwait(false)).Expect(400, "another member's topic rejected");
            TopicSlotsResponse swapped = (await member.Client.PutAsync("/v1.0/api/account/topic-slots", new { keepTopicIds = new[] { topics[0].Id } }, token).ConfigureAwait(false)).Expect(200, "change the choice").Json<TopicSlotsResponse>();
            Assert.True(swapped.Topics[0].TopicId == topics[0].Id && swapped.Topics.Single(x => !x.WithinPlan).TopicId == topics[2].Id, "chosen first, then oldest unchosen");
            (await member.Client.PostAsync("/v1.0/api/topics/" + topics[1].Id + "/archive", null, token).ConfigureAwait(false)).Expect(200, "archive one");
            TopicSlotsResponse fits = (await member.Client.GetAsync("/v1.0/api/account/topic-slots", token).ConfigureAwait(false)).Expect(200, "fits").Json<TopicSlotsResponse>();
            Assert.True(fits.Counted == 2 && fits.OverLimit == 0, "archiving frees the slot");
            TopicSlotsResponse cleared = (await member.Client.PutAsync("/v1.0/api/account/topic-slots", new { keepTopicIds = Array.Empty<string>() }, token).ConfigureAwait(false)).Expect(200, "clear").Json<TopicSlotsResponse>();
            Assert.True(!cleared.Customized, "cleared");
        }

        private static async Task<Briefing> ComposeAsync(PipelineFixture f, string editionId, CancellationToken token)
        {
            Briefing queued = (await f.User.Client.PostAsync("/v1.0/api/editions/" + editionId + "/compose", null, token).ConfigureAwait(false)).Expect(202, "compose").Json<Briefing>();
            return await BriefingWaiter.FinishAsync(f.Server, queued.TenantId, queued.Id, token).ConfigureAwait(false);
        }

        private static async Task<Finding> Finding(MyMorningNewsServer server, Run run, string title, string? updates, bool correction, bool duplicate, string? duplicateOf, CancellationToken token)
        {
            return await server.Context.Database.Findings.CreateAsync(new Finding
            {
                TenantId = run.TenantId, RunId = run.Id, TopicId = run.TopicId, UserId = run.UserId, Title = title, Summary = title, Tier = FindingTierEnum.MustRead,
                RelevanceScore = 80, IsUpdateOfFindingId = updates, IsCorrection = correction, IsDuplicate = duplicate, DuplicateOfFindingId = duplicateOf
            }, token).ConfigureAwait(false);
        }
    }
}
