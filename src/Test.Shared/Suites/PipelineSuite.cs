namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Pipeline;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Server;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// The research pipeline end to end against the stub model and web server, plus the ranking and evidence helpers.
    /// </summary>
    public static class PipelineSuite
    {
        private const string S = "Pipeline";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Research pipeline", new[]
            {
                Cases.Case(S, "QuoteVerifier", "Quotes are verified verbatim with offsets, ignoring case, spacing, and typography", QuoteAsync, "fast"),
                Cases.Case(S, "Ranker", "Recency, novelty, source, composite, and tiers follow the plan", RankerAsync, "fast"),
                Cases.Case(S, "FullRun", "A run searches, filters, fetches, extracts verified findings, scores, synthesizes, predicts, suggests, and reviews", FullRunAsync),
                Cases.Case(S, "SecondRunDedupe", "A later run skips seen pages and marks a repeated finding as a duplicate", SecondRunAsync),
                Cases.Case(S, "Failures", "A critical failure fails the run; an optional failure leaves it partially succeeded", FailuresAsync),
                Cases.Case(S, "ResearchApi", "Findings, evidence, predictions, suggestions, and run details are served, private, and deleted with their topic", ApiAsync),
                Cases.Case(S, "UserDelete", "Deleting a user removes their topics with all research, editions with schedules, briefings, and feedback", UserDeleteAsync)
            });
        }

        private static Task QuoteAsync(CancellationToken token)
        {
            string source = "Intro.\n\nThe state  announced “new rebates” for heat pumps on Tuesday.\n\nApplications open in **January**.";
            VerifiedQuote v = QuoteVerifier.Verify(source, "the state announced \"new rebates\" for heat pumps on tuesday.");
            Assert.True(v.Found && source.Substring(v.Start, v.End - v.Start) == v.Text && v.Text.StartsWith("The state", StringComparison.Ordinal) && v.Context.Contains("rebates", StringComparison.Ordinal), "found with offsets: " + v.Text);
            Assert.True(QuoteVerifier.Verify(source, "Applications open in January").Found, "markdown emphasis ignored");
            Assert.True(!QuoteVerifier.Verify(source, "The state announced new tax credits").Found, "different text not found");
            Assert.True(!QuoteVerifier.Verify(source, "Tuesday").Found, "too short to count");
            Assert.Equal(QuoteVerifier.Hash("New  Rebates for heat pumps"), QuoteVerifier.Hash("new rebates for heat pumps"), "hash normalized");
            return Task.CompletedTask;
        }

        private static Task RankerAsync(CancellationToken token)
        {
            DateTime now = new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
            Assert.Equal(100.0, Ranker.Recency(now, now, 72), "now is 100");
            Assert.Equal(50.0, Ranker.Recency(now.AddHours(-72), now, 72), "one half-life is 50");
            Assert.Equal(50.0, Ranker.Recency(null, now, 72), "unknown is neutral");
            Assert.Equal(10.0, Ranker.Novelty(0.9, 100), "novelty counts fully at preference 100");
            Assert.Equal(50.0, Ranker.Novelty(0.9, 0), "novelty ignored at preference 0");
            Assert.Equal(70.0, Ranker.Source(null, 0, true, false), "preferred domain bonus");
            Assert.Equal(0.0, Ranker.Source(90, 10, true, true), "blocked is zero");
            Assert.Equal(76.67, Ranker.Source(90, 10, false, false), "bayesian average toward 50");
            Assert.Equal(80.0, Ranker.Composite(80, 80, 80, 80, 80, 0.45, 0.2, 0.15, 0.1, 0.1), "uniform components");
            Assert.Equal(90.0, Ranker.Composite(90, 0, 0, 0, 0, 1, 0, 0, 0, 0), "single weight");
            Assert.True(Ranker.Tier(85, 40) == FindingTierEnum.MustRead && Ranker.Tier(65, 40) == FindingTierEnum.WorthALook && Ranker.Tier(45, 40) == FindingTierEnum.Fyi && Ranker.Tier(45, 50) == FindingTierEnum.BelowThreshold, "tiers");
            return Task.CompletedTask;
        }

        /// <summary>
        /// A tenant with a tenant administrator, the stub research model for every task, a stub search provider, and an active topic.
        /// </summary>
        /// <param name="brief">Topic brief.</param>
        /// <param name="searchPath">Stub search path (/pipeline or /pipeline2).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Fixture.</returns>
        internal static async Task<PipelineFixture> FixtureAsync(string brief, string searchPath, CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            server.Context.SettingsService.Current.Search.PerDomainDelayMs = 0;
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser ta = await TestFixtures.CreateUserAsync(admin, t.Id, true, token).ConfigureAwait(false);
            ModelEndpoint e = (await ta.Client.PostAsync("/v1.0/api/model-endpoints", new
            {
                name = "stub research", provider = "OpenAiCompatible", endpoint = StubModelServer.Shared.BaseUrl, model = "stub-research", supportsChat = true,
                supportsEmbeddings = true, embeddingModel = "stub-embed-8", inputCostPerMTok = 1.0, outputCostPerMTok = 2.0
            }, token).ConfigureAwait(false)).Expect(201, "endpoint").Json<ModelEndpoint>();
            foreach (ModelTaskEnum task in Enum.GetValues<ModelTaskEnum>())
                (await ta.Client.PutAsync("/v1.0/api/model-assignments", new { task = task.ToString(), modelEndpointId = e.Id }, token).ConfigureAwait(false)).Expect(200, "assign " + task);
            SearchProvider sp = (await ta.Client.PostAsync("/v1.0/api/search-providers", new { name = "pipeline searx", kind = "Searxng", endpoint = StubModelServer.Shared.BaseUrl + searchPath }, token).ConfigureAwait(false))
                .Expect(201, "provider").Json<SearchProvider>();
            Topic topic = (await ta.Client.PostAsync("/v1.0/api/topics", new { name = "Heat pumps " + Guid.NewGuid().ToString("N").Substring(0, 6), brief = brief, status = "Active", noveltyPreference = 50 }, token).ConfigureAwait(false))
                .Expect(201, "topic").Json<Topic>();
            await ta.Client.PostAsync("/v1.0/api/topics/" + topic.Id + "/dimensions", new { name = "Rebates", weight = "High" }, token).ConfigureAwait(false);
            await ta.Client.PostAsync("/v1.0/api/topics/" + topic.Id + "/constraints", new { kind = "BlockedDomain", value = "blocked.test" }, token).ConfigureAwait(false);
            (await ta.Client.PutAsync("/v1.0/api/topics/" + topic.Id + "/search-providers", new[] { new { searchProviderId = sp.Id, enabled = true } }, token).ConfigureAwait(false)).Expect(200, "providers");
            return new PipelineFixture { Server = server, User = ta, Topic = topic, Provider = sp };
        }

        /// <summary>
        /// Run the fixture topic now and execute the queue until the run finishes.
        /// </summary>
        /// <param name="f">Fixture.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Finished run.</returns>
        internal static async Task<Run> RunAsync(PipelineFixture f, CancellationToken token)
        {
            Run queued = (await f.User.Client.PostAsync("/v1.0/api/topics/" + f.Topic.Id + "/run", null, token).ConfigureAwait(false)).Expect(202, "run now").Json<Run>();
            return await RunWaiter.FinishAsync(f.Server, queued, token).ConfigureAwait(false);
        }

        private static async Task FullRunAsync(CancellationToken token)
        {
            PipelineFixture f = await FixtureAsync("Track residential heat pump rebates and the rules that govern them.", "/pipeline", token).ConfigureAwait(false);
            Prediction open = await f.Server.Context.Database.Predictions.CreateAsync(new Prediction
            {
                TenantId = f.Topic.TenantId, RunId = "run_seed", TopicId = f.Topic.Id, UserId = f.Topic.UserId, Statement = "The state will announce new heat pump rebates this year.", Confidence = 0.8, HorizonUtc = DateTime.UtcNow.AddDays(30)
            }, token).ConfigureAwait(false);

            Run run = await RunAsync(f, token).ConfigureAwait(false);
            Assert.True(run.Status == RunStatusEnum.Completed, "completed: " + run.Status + " " + run.ErrorMessage + " " + run.Summary);
            List<RunStage> stages = (await f.User.Client.GetAsync("/v1.0/api/runs/" + run.Id + "/stages", token).ConfigureAwait(false)).Expect(200, "stages").Json<List<RunStage>>();
            Assert.True(stages.Count == 15 && stages.All(s => s.Status == StageStatusEnum.Succeeded), "all stages succeeded: " + String.Join(", ", stages.Select(s => s.Stage + "=" + s.Status + (s.ErrorMessage == null ? String.Empty : "(" + s.ErrorMessage + ")"))));
            Assert.True(stages.Single(s => s.Stage == "Extract").ErrorMessage!.Contains("could not be verified", StringComparison.Ordinal), "unverified quotes noted");

            List<SearchResult> results = (await f.User.Client.GetAsync("/v1.0/api/runs/" + run.Id + "/results", token).ConfigureAwait(false)).Json<List<SearchResult>>();
            Assert.True(results.Count(r => r.Disposition == SearchResultDispositionEnum.Fetched) == 2 && results.Count(r => r.Disposition == SearchResultDispositionEnum.SkippedDuplicate) == 2 && !results.Any(r => r.Url.Contains("blocked.test", StringComparison.Ordinal)),
                "blocked domains excluded at search; repeats across queries skipped: " + String.Join(", ", results.Select(r => r.Disposition)));

            EnumerationResult<Finding> findings = (await f.User.Client.GetAsync("/v1.0/api/findings?topicId=" + f.Topic.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Finding>>();
            Assert.True(findings.Items.Count == 2, "two verified findings, unsupported claim discarded (" + findings.Items.Count + ")");
            Finding top = findings.Items[0];
            Assert.True(top.Title == "Heat pump rebates expand" && top.Rank == 1 && top.LlmScore == 92 && top.Tier != FindingTierEnum.BelowThreshold && top.DimensionId != null && top.RecencyScore > 90, "top finding ranked and scored");
            Assert.True(findings.Items[1].RelevanceScore < top.RelevanceScore, "ordered by relevance");
            Assert.True(run.NewFindingCount == 2 && run.SourceCount == 2 && run.InputTokens > 0 && run.EstimatedCostUsd > 0 && run.ContextSnapshotHash != null, "run totals");
            Assert.True(run.Summary!.StartsWith("Rebates expanded", StringComparison.Ordinal) && run.Summary.Contains("Incentives", StringComparison.Ordinal), "synthesized summary");

            FindingDetailResponse detail = (await f.User.Client.GetAsync("/v1.0/api/findings/" + top.Id, token).ConfigureAwait(false)).Expect(200, "detail").Json<FindingDetailResponse>();
            Assert.True(detail.Evidence.Count == 1 && detail.Evidence[0].Evidence.Verified && detail.Evidence[0].Evidence.Quote.StartsWith("The state announced", StringComparison.Ordinal) && detail.Evidence[0].SourceUrl!.EndsWith("/news/a.html", StringComparison.Ordinal),
                "verified evidence with source");

            EnumerationResult<Prediction> preds = (await f.User.Client.GetAsync("/v1.0/api/predictions?topicId=" + f.Topic.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Prediction>>();
            Assert.True(preds.Items.Any(p => p.Statement.StartsWith("California will open", StringComparison.Ordinal) && p.Status == PredictionStatusEnum.Open && p.HorizonUtc.Year == 2027), "new prediction");
            PredictionDetailResponse reviewed = (await f.User.Client.GetAsync("/v1.0/api/predictions/" + open.Id, token).ConfigureAwait(false)).Json<PredictionDetailResponse>();
            Assert.True(reviewed.Prediction.Status == PredictionStatusEnum.ProposedResolved && reviewed.Reviews.Count == 1 && reviewed.Reviews[0].ProposedStatus == PredictionStatusEnum.Accurate && reviewed.Reviews[0].FindingIds.Contains(top.Id), "review proposed with evidence");
            Prediction accepted = (await f.User.Client.PostAsync("/v1.0/api/prediction-reviews/" + reviewed.Reviews[0].Id + "/accept", null, token).ConfigureAwait(false)).Expect(200, "accept").Json<Prediction>();
            Assert.True(accepted.Status == PredictionStatusEnum.Accurate && accepted.ResolvedBy == "AgentConfirmed" && Math.Abs(accepted.BrierScore!.Value - 0.04) < 0.0001, "accepted with Brier score");

            EnumerationResult<Suggestion> sugs = (await f.User.Client.GetAsync("/v1.0/api/suggestions?topicId=" + f.Topic.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Suggestion>>();
            Assert.True(sugs.Items.Count == 1 && sugs.Items[0].Priority == SuggestionPriorityEnum.High && sugs.Items[0].FindingIds.Contains(top.Id), "suggestion linked");
            EnumerationResult<LlmCall> calls = (await f.User.Client.GetAsync("/v1.0/api/llm-calls?pageSize=100", token).ConfigureAwait(false)).Json<EnumerationResult<LlmCall>>();
            Assert.True(calls.Items.Where(c => c.RunId == run.Id).Select(c => c.PromptKey).Distinct().Count() >= 7, "calls attributed to the run");
        }

        private static async Task SecondRunAsync(CancellationToken token)
        {
            PipelineFixture f = await FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            Run first = await RunAsync(f, token).ConfigureAwait(false);
            Assert.True(first.Status == RunStatusEnum.Completed, "first run: " + first.ErrorMessage);
            Run second = await RunAsync(f, token).ConfigureAwait(false);
            Assert.True(second.Status == RunStatusEnum.Completed && second.NewFindingCount == 0 && second.Summary!.StartsWith("Nothing new", StringComparison.Ordinal), "seen pages skipped: " + second.Summary);
            List<SearchResult> results = (await f.User.Client.GetAsync("/v1.0/api/runs/" + second.Id + "/results", token).ConfigureAwait(false)).Json<List<SearchResult>>();
            Assert.True(results.Count(r => r.Disposition == SearchResultDispositionEnum.SkippedSeen) == 2, "skipped as seen");

            (await f.User.Client.PutAsync("/v1.0/api/search-providers/" + f.Provider.Id, new { endpoint = StubModelServer.Shared.BaseUrl + "/pipeline2" }, token).ConfigureAwait(false)).Expect(200, "switch provider");
            Run third = await RunAsync(f, token).ConfigureAwait(false);
            Assert.True(third.Status == RunStatusEnum.Completed && third.DuplicateCount == 1 && third.NewFindingCount == 0, "same story on a new page is a duplicate (dup " + third.DuplicateCount + ", new " + third.NewFindingCount + ")");
            EnumerationResult<Finding> withDup = (await f.User.Client.GetAsync("/v1.0/api/findings?runId=" + third.Id + "&includeDuplicates=true", token).ConfigureAwait(false)).Json<EnumerationResult<Finding>>();
            Assert.True(withDup.Items.Count == 1 && withDup.Items[0].IsDuplicate && withDup.Items[0].DuplicateOfFindingId != null, "duplicate saved and linked");
        }

        private static async Task FailuresAsync(CancellationToken token)
        {
            PipelineFixture dead = await FixtureAsync("Track heat pumps.", "/pipeline", token).ConfigureAwait(false);
            (await dead.User.Client.PutAsync("/v1.0/api/search-providers/" + dead.Provider.Id, new { endpoint = "http://127.0.0.1:1" }, token).ConfigureAwait(false)).Expect(200, "break provider");
            Run failed = await RunAsync(dead, token).ConfigureAwait(false);
            Assert.True(failed.Status == RunStatusEnum.Failed && failed.ErrorMessage!.Contains("Search failed", StringComparison.Ordinal), "critical failure: " + failed.ErrorMessage);
            List<RunStage> stages = (await dead.User.Client.GetAsync("/v1.0/api/runs/" + failed.Id + "/stages", token).ConfigureAwait(false)).Json<List<RunStage>>();
            Assert.True(stages.Last().Stage == "Search" && stages.Last().Status == StageStatusEnum.Failed, "stopped at search");

            PipelineFixture partial = await FixtureAsync("Track heat pump rebates. FAILPREDICT", "/pipeline", token).ConfigureAwait(false);
            Run run = await RunAsync(partial, token).ConfigureAwait(false);
            Assert.True(run.Status == RunStatusEnum.PartiallySucceeded && run.NewFindingCount == 2 && run.Summary!.Contains("Predict", StringComparison.Ordinal), "optional failure: " + run.Status + " " + run.Summary);
            List<RunStage> ps = (await partial.User.Client.GetAsync("/v1.0/api/runs/" + run.Id + "/stages", token).ConfigureAwait(false)).Json<List<RunStage>>();
            Assert.True(ps.Single(s => s.Stage == "Predict").Status == StageStatusEnum.Failed && ps.Single(s => s.Stage == "Suggest").Status == StageStatusEnum.Succeeded, "later stages still ran");
        }

        private static async Task ApiAsync(CancellationToken token)
        {
            PipelineFixture f = await FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            Run run = await RunAsync(f, token).ConfigureAwait(false);
            EnumerationResult<Finding> high = (await f.User.Client.GetAsync("/v1.0/api/findings?topicId=" + f.Topic.Id + "&minScore=70", token).ConfigureAwait(false)).Json<EnumerationResult<Finding>>();
            Assert.True(high.Items.All(x => x.RelevanceScore >= 70) && high.Items.Count >= 1 && high.TotalCount == high.Items.Count, "min score filtered in the query, total matches");
            EnumerationResult<SourceItem> sources = (await f.User.Client.GetAsync("/v1.0/api/sources?topicId=" + f.Topic.Id, token).ConfigureAwait(false)).Expect(200, "sources").Json<EnumerationResult<SourceItem>>();
            Assert.True(sources.TotalCount == 2 && sources.Items.All(x => x.ContentText == null && x.CanonicalUrl.Contains("/news/", StringComparison.Ordinal)), "sources listed without content");
            Assert.Equal(1L, (await f.User.Client.GetAsync("/v1.0/api/sources?topicId=" + f.Topic.Id + "&search=b.html", token).ConfigureAwait(false)).Json<EnumerationResult<SourceItem>>().TotalCount, "source search");
            Assert.Equal("a\tb\nc", MyMorningNews.Core.Pipeline.Stages.FetchStage.Clean("a\u0000\tb\nc\u0007"), "NUL and control characters removed");
            List<SearchQuery> queries = (await f.User.Client.GetAsync("/v1.0/api/runs/" + run.Id + "/queries", token).ConfigureAwait(false)).Expect(200, "queries").Json<List<SearchQuery>>();
            Assert.True(queries.Count == 2 && queries.All(q => q.Succeeded && q.Provider == "Searxng"), "queries recorded");
            EnumerationResult<Suggestion> sugs = (await f.User.Client.GetAsync("/v1.0/api/suggestions?topicId=" + f.Topic.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Suggestion>>();
            (await f.User.Client.PutAsync("/v1.0/api/suggestions/" + sugs.Items[0].Id + "/status", new { status = "Snoozed" }, token).ConfigureAwait(false)).Expect(400, "snooze needs a time");
            Suggestion snoozed = (await f.User.Client.PutAsync("/v1.0/api/suggestions/" + sugs.Items[0].Id + "/status", new { status = "Snoozed", snoozedUntilUtc = DateTime.UtcNow.AddDays(3) }, token).ConfigureAwait(false))
                .Expect(200, "snooze").Json<Suggestion>();
            Assert.True(snoozed.Status == SuggestionStatusEnum.Snoozed && snoozed.SnoozedUntilUtc.HasValue && snoozed.StatusChangedUtc.HasValue, "snoozed");
            EnumerationResult<Prediction> preds = (await f.User.Client.GetAsync("/v1.0/api/predictions?topicId=" + f.Topic.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Prediction>>();
            (await f.User.Client.PostAsync("/v1.0/api/predictions/" + preds.Items[0].Id + "/resolve", new { status = "Open" }, token).ConfigureAwait(false)).Expect(400, "open is not a resolution");
            Prediction resolved = (await f.User.Client.PostAsync("/v1.0/api/predictions/" + preds.Items[0].Id + "/resolve", new { status = "Inaccurate", note = "Did not happen." }, token).ConfigureAwait(false))
                .Expect(200, "resolve").Json<Prediction>();
            Assert.True(resolved.Status == PredictionStatusEnum.Inaccurate && resolved.ResolvedBy == "User" && Math.Abs(resolved.BrierScore!.Value - 0.49) < 0.0001, "resolved with Brier score");

            TestUser other = await TestFixtures.CreateUserAsync(await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false), f.Topic.TenantId, false, token).ConfigureAwait(false);
            EnumerationResult<Finding> theirs = (await other.Client.GetAsync("/v1.0/api/findings", token).ConfigureAwait(false)).Expect(200, "member list").Json<EnumerationResult<Finding>>();
            Assert.Equal(0, theirs.Items.Count, "another member sees none");
            Assert.Equal(0L, (await other.Client.GetAsync("/v1.0/api/sources", token).ConfigureAwait(false)).Json<EnumerationResult<SourceItem>>().TotalCount, "another member sees no sources");
            (await other.Client.GetAsync("/v1.0/api/findings/" + high.Items[0].Id, token).ConfigureAwait(false)).Expect(404, "finding private");
            (await other.Client.GetAsync("/v1.0/api/runs/" + run.Id + "/stages", token).ConfigureAwait(false)).Expect(404, "run private");
            (await other.Client.GetAsync("/v1.0/api/sources/" + sources.Items[0].Id, token).ConfigureAwait(false)).Expect(404, "source private to the member whose topics found it");
            (await f.User.Client.GetAsync("/v1.0/api/sources/" + sources.Items[0].Id, token).ConfigureAwait(false)).Expect(200, "owner reads the source");

            FindingDetailResponse detail = (await f.User.Client.GetAsync("/v1.0/api/findings/" + high.Items[0].Id, token).ConfigureAwait(false)).Json<FindingDetailResponse>();
            string evidenceId = detail.Evidence[0].Evidence.Id;
            EvidenceView ev = (await f.User.Client.GetAsync("/v1.0/api/evidence/" + evidenceId, token).ConfigureAwait(false)).Expect(200, "evidence").Json<EvidenceView>();
            Assert.True(ev.Evidence.Id == evidenceId && !String.IsNullOrEmpty(ev.SourceUrl), "evidence with its source");
            (await other.Client.GetAsync("/v1.0/api/evidence/" + evidenceId, token).ConfigureAwait(false)).Expect(404, "evidence private");
            EnumerationResult<LlmCall> runCalls = (await f.User.Client.GetAsync("/v1.0/api/runs/" + run.Id + "/llm-calls?pageSize=500", token).ConfigureAwait(false)).Expect(200, "run calls").Json<EnumerationResult<LlmCall>>();
            Assert.True(runCalls.Items.Count > 0 && runCalls.Items.All(c => c.RunId == run.Id) && runCalls.Items.SequenceEqual(runCalls.Items.OrderBy(c => c.CreatedUtc)), "run calls oldest first");
            (await other.Client.GetAsync("/v1.0/api/runs/" + run.Id + "/llm-calls", token).ConfigureAwait(false)).Expect(404, "run calls private");

            string fid = high.Items[0].Id;
            (await f.User.Client.PutAsync("/v1.0/api/findings/" + fid, new { }, token).ConfigureAwait(false)).Expect(400, "nothing to change");
            Finding pinned = (await f.User.Client.PutAsync("/v1.0/api/findings/" + fid, new { pinned = true }, token).ConfigureAwait(false)).Expect(200, "pin").Json<Finding>();
            Assert.True(pinned.Pinned && !pinned.Hidden, "pinned");
            Assert.Equal(1L, (await f.User.Client.GetAsync("/v1.0/api/findings?topicId=" + f.Topic.Id + "&pinned=true", token).ConfigureAwait(false)).Json<EnumerationResult<Finding>>().TotalCount, "pinned filter");
            long before = (await f.User.Client.GetAsync("/v1.0/api/findings?topicId=" + f.Topic.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Finding>>().TotalCount;
            (await f.User.Client.PutAsync("/v1.0/api/findings/" + fid, new { hidden = true }, token).ConfigureAwait(false)).Expect(200, "hide");
            Assert.Equal(before - 1, (await f.User.Client.GetAsync("/v1.0/api/findings?topicId=" + f.Topic.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Finding>>().TotalCount, "hidden left out");
            Assert.Equal(before, (await f.User.Client.GetAsync("/v1.0/api/findings?topicId=" + f.Topic.Id + "&includeHidden=true", token).ConfigureAwait(false)).Json<EnumerationResult<Finding>>().TotalCount, "includeHidden");
            (await other.Client.PutAsync("/v1.0/api/findings/" + fid, new { hidden = false }, token).ConfigureAwait(false)).Expect(404, "others cannot change it");
            (await other.Client.DeleteAsync("/v1.0/api/findings/" + fid, token).ConfigureAwait(false)).Expect(404, "others cannot delete it");
            string doomed = (await f.User.Client.GetAsync("/v1.0/api/findings?topicId=" + f.Topic.Id + "&includeBelowThreshold=true&includeHidden=true&pageSize=100", token).ConfigureAwait(false)).Json<EnumerationResult<Finding>>().Items.First(x => x.Id != fid).Id;
            (await f.User.Client.DeleteAsync("/v1.0/api/findings/" + doomed, token).ConfigureAwait(false)).Expect(204, "delete finding");
            (await f.User.Client.GetAsync("/v1.0/api/findings/" + doomed, token).ConfigureAwait(false)).Expect(404, "gone");
            Assert.Equal(0L, await f.Server.Context.Database.FindingEvidence.CountAsync(new FindingEvidenceFilter { TenantId = f.Topic.TenantId, FindingId = doomed }, token).ConfigureAwait(false), "evidence links removed");

            TestResponse wrongRating = (await f.User.Client.PostAsync("/v1.0/api/feedback", new { targetType = "Finding", targetId = fid, accuracy = "Accurate" }, token).ConfigureAwait(false)).Expect(400, "accuracy does not apply to a finding");
            Assert.True(!wrongRating.Body.Contains("(Parameter", StringComparison.Ordinal), "no parameter suffix in the message");
            (await f.User.Client.PostAsync("/v1.0/api/feedback", new { targetType = "Finding", targetId = fid, relevance = "Relevant", reason = "Kept for the cascade check" }, token).ConfigureAwait(false)).Expect(201, "rate before delete");
            (await f.User.Client.PostAsync("/v1.0/api/learned-preferences", new { topicId = f.Topic.Id, statement = "Prefer primary sources." }, token).ConfigureAwait(false)).Expect(201, "topic preference");
            Schedule sched = (await f.User.Client.PostAsync("/v1.0/api/schedules", new { ownerId = f.Topic.Id, intervalType = "EveryNMinutes", intervalValue = 30 }, token).ConfigureAwait(false)).Expect(201, "schedule").Json<Schedule>();
            (await f.User.Client.DeleteAsync("/v1.0/api/topics/" + f.Topic.Id, token).ConfigureAwait(false)).Expect(204, "delete topic");
            Assert.True(await f.Server.Context.Database.Schedules.ReadAsync(f.Topic.TenantId, sched.Id, token).ConfigureAwait(false) == null, "schedule deleted with the topic");
            Assert.True(await f.Server.Context.Database.Runs.ReadAsync(f.Topic.TenantId, run.Id, token).ConfigureAwait(false) == null, "runs deleted with the topic");
            Assert.True(await f.Server.Context.Database.Findings.ReadAsync(f.Topic.TenantId, high.Items[0].Id, token).ConfigureAwait(false) == null, "findings deleted with the topic");
            Assert.Equal(0L, await f.Server.Context.Database.Briefings.CountAsync(new MyMorningNews.Core.Requests.BriefingFilter { TenantId = f.Topic.TenantId, TopicId = f.Topic.Id }, token).ConfigureAwait(false), "briefings deleted with the topic");
            Assert.Equal(0L, await f.Server.Context.Database.SearchResults.CountAsync(new SearchResultFilter { TenantId = f.Topic.TenantId, RunId = run.Id }, token).ConfigureAwait(false), "search results deleted with the topic");
            Assert.Equal(0L, await f.Server.Context.Database.RunStages.CountAsync(new RunStageFilter { TenantId = f.Topic.TenantId, RunId = run.Id }, token).ConfigureAwait(false), "run stages deleted with the topic");
            Assert.Equal(0L, await f.Server.Context.Database.Feedback.CountAsync(new FeedbackFilter { TenantId = f.Topic.TenantId, TopicId = f.Topic.Id }, token).ConfigureAwait(false), "feedback deleted with the topic");
            Assert.Equal(0L, await f.Server.Context.Database.LearnedPreferences.CountAsync(new LearnedPreferenceFilter { TenantId = f.Topic.TenantId, TopicId = f.Topic.Id }, token).ConfigureAwait(false), "preferences deleted with the topic");
            EnumerationResult<LlmCall> kept = (await f.User.Client.GetAsync("/v1.0/api/llm-calls?topicId=" + f.Topic.Id, token).ConfigureAwait(false)).Json<EnumerationResult<LlmCall>>();
            Assert.True(kept.Items.Count > 0, "model call records kept for cost accounting");
        }

        private static async Task UserDeleteAsync(CancellationToken token)
        {
            PipelineFixture f = await FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            Run run = await RunAsync(f, token).ConfigureAwait(false);
            Finding finding = (await f.User.Client.GetAsync("/v1.0/api/findings?runId=" + run.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Finding>>().Items.First();
            (await f.User.Client.PostAsync("/v1.0/api/feedback", new { targetType = "Finding", targetId = finding.Id, relevance = "Relevant", reason = "Useful" }, token).ConfigureAwait(false)).Expect(201, "feedback");
            (await f.User.Client.PostAsync("/v1.0/api/editions", new { name = "Doomed edition" }, token).ConfigureAwait(false)).Expect(201, "edition");
            (await f.User.Client.PostAsync("/v1.0/api/feed-tokens", new { name = "doomed feed" }, token).ConfigureAwait(false)).Expect(201, "feed token");
            string tenantId = f.Topic.TenantId;
            string userId = f.User.User.Id;
            MyMorningNewsServer server = f.Server;
            Assert.True(await server.Context.Database.Editions.CountAsync(new EditionFilter { TenantId = tenantId, UserId = userId }, token).ConfigureAwait(false) >= 1, "editions before");

            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            (await admin.DeleteAsync("/v1.0/tenants/" + tenantId + "/users/" + userId, token).ConfigureAwait(false)).Expect(204, "delete user");
            Assert.True(await server.Context.Database.Users.ReadAsync(tenantId, userId, token).ConfigureAwait(false) == null, "user gone");
            Assert.Equal(0L, await server.Context.Database.Topics.CountAsync(new TopicFilter { TenantId = tenantId, UserId = userId }, token).ConfigureAwait(false), "topics gone");
            Assert.True(await server.Context.Database.Runs.ReadAsync(tenantId, run.Id, token).ConfigureAwait(false) == null, "runs gone, not orphaned");
            Assert.True(await server.Context.Database.Findings.ReadAsync(tenantId, finding.Id, token).ConfigureAwait(false) == null, "findings gone");
            Assert.Equal(0L, await server.Context.Database.Briefings.CountAsync(new BriefingFilter { TenantId = tenantId, UserId = userId }, token).ConfigureAwait(false), "briefings gone");
            Assert.Equal(0L, await server.Context.Database.Editions.CountAsync(new EditionFilter { TenantId = tenantId, UserId = userId }, token).ConfigureAwait(false), "editions gone");
            Assert.Equal(0L, await server.Context.Database.Schedules.CountAsync(new ScheduleFilter { TenantId = tenantId }, token).ConfigureAwait(false), "schedules gone");
            Assert.Equal(0L, await server.Context.Database.Feedback.CountAsync(new FeedbackFilter { TenantId = tenantId, UserId = userId }, token).ConfigureAwait(false), "feedback gone");
            Assert.Equal(0L, await server.Context.Database.FeedTokens.CountAsync(new FeedTokenFilter { TenantId = tenantId, UserId = userId }, token).ConfigureAwait(false), "feed tokens gone");
            Assert.Equal(0L, await server.Context.Database.Artifacts.CountAsync(new ArtifactFilter { TenantId = tenantId, UserId = userId }, token).ConfigureAwait(false), "stored files gone");
        }
    }
}
