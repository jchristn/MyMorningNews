namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Analytics;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Models.Results;
    using MyMorningNews.Core.Pipeline;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Learning from feedback and analytics (plan sections 17 to 20): feedback in ranking and prompts, reason tags,
    /// distilled preferences and their lifecycle, preference proposals by email, feedback edits and retraction, and
    /// the analytics reports.
    /// </summary>
    public static class LearningSuite
    {
        private const string S = "Learning";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Learning and analytics", new[]
            {
                Cases.Case(S, "RankerMath", "Feedback affinity and rating values follow the plan", RankerAsync, "fast"),
                Cases.Case(S, "FeedbackRanking", "Ratings move the feedback and source scores and reach the next run's prompts", FeedbackRankingAsync),
                Cases.Case(S, "Distill", "A learning pass tags reasons and proposes preferences backed by three ratings, without repeats", DistillAsync),
                Cases.Case(S, "Preferences", "Preferences are written, edited, accepted, disabled, and kept private", PreferencesAsync),
                Cases.Case(S, "EmailProposals", "Proposals appear in the next digest and Apply from email makes them active", EmailProposalsAsync),
                Cases.Case(S, "FeedbackEdits", "Feedback reasons can be edited and a retraction restores the earlier rating", FeedbackEditsAsync),
                Cases.Case(S, "Analytics", "Relevance, accuracy, feedback, suggestions, reasons, quality, and ranking reports reflect the data", AnalyticsAsync)
            });
        }

        private static Task RankerAsync(CancellationToken token)
        {
            Assert.Equal(50.0, Ranker.Feedback(0, 1), "neutral");
            Assert.Equal(100.0, Ranker.Feedback(1, 1), "liked");
            Assert.Equal(0.0, Ranker.Feedback(-1, 1), "disliked");
            Assert.Equal(75.0, Ranker.Feedback(1, 0.5), "half the evidence");
            Assert.True(Ranker.RatingValue(RelevanceRatingEnum.Relevant) == 1 && Ranker.RatingValue(RelevanceRatingEnum.SomewhatRelevant) == 0.3 && Ranker.RatingValue(RelevanceRatingEnum.NotRelevant) == -1, "rating values");
            Assert.True(Ranker.ReputationPoints(RelevanceRatingEnum.NotRelevant) == 0 && Ranker.ReputationPoints(RelevanceRatingEnum.Relevant) == 100, "reputation points");
            return Task.CompletedTask;
        }

        private static async Task<List<Finding>> FindingsAsync(PipelineFixture f, Run run, CancellationToken token)
        {
            return (await f.User.Client.GetAsync("/v1.0/api/findings?runId=" + run.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Finding>>().Items;
        }

        private static async Task<Feedback> RateAsync(TestUser u, string type, string id, string action, string? reason, CancellationToken token)
        {
            return (await u.Client.PostAsync("/v1.0/api/feedback", new { targetType = type, targetId = id, action = action, reason = reason }, token).ConfigureAwait(false)).Expect(201, "rate " + action).Json<Feedback>();
        }

        private static async Task FeedbackRankingAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            Run first = await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            List<Finding> found = await FindingsAsync(f, first, token).ConfigureAwait(false);
            Finding rebates = found.Single(x => x.Title.StartsWith("Heat pump rebates", StringComparison.Ordinal));
            Finding standards = found.Single(x => x.Title.StartsWith("Efficiency standards", StringComparison.Ordinal));
            Assert.True(rebates.FeedbackScore == 50 && standards.FeedbackScore == 50, "neutral before any feedback");
            await RateAsync(f.User, "Finding", rebates.Id, "not-relevant", "Too basic for me", token).ConfigureAwait(false);
            await RateAsync(f.User, "Finding", standards.Id, "relevant", "Exactly the regulatory detail I want", token).ConfigureAwait(false);
            (await f.User.Client.PostAsync("/v1.0/api/learned-preferences", new { statement = "Prefer primary sources such as regulators", kind = "PreferSource", topicId = f.Topic.Id }, token).ConfigureAwait(false)).Expect(201, "preference");

            RunContext ctx = new RunContext { Run = first, Topic = f.Topic };
            PipelineServices services = new PipelineServices(f.Server.Context.Database, f.Server.Context.Models, f.Server.Context.Prompts, f.Server.Context.Search, f.Server.Context.Fetch, f.Server.Context.Settings, f.Server.Context.Logging);
            FeedbackProfile profile = await FeedbackProfile.LoadAsync(services, ctx, token).ConfigureAwait(false);
            Assert.True(profile.RatedFindings.Count == 2 && profile.DomainRatings.Values.Sum(v => v.Count) == 2, "profile loaded");
            double disliked = await profile.ScoreAsync(services, ctx, new Candidate { Extracted = new ExtractedFinding { Title = rebates.Title } }, token).ConfigureAwait(false);
            double liked = await profile.ScoreAsync(services, ctx, new Candidate { Extracted = new ExtractedFinding { Title = standards.Title } }, token).ConfigureAwait(false);
            double unrelated = await profile.ScoreAsync(services, ctx, new Candidate { Extracted = new ExtractedFinding { Title = "Quarterly earnings for a shoe company" } }, token).ConfigureAwait(false);
            Assert.True(disliked < 30 && liked > 70 && unrelated == 50, "feedback score: disliked " + disliked + ", liked " + liked + ", unrelated " + unrelated);

            await RateAsync(f.User, "Domain", "127.0.0.1", "relevant", null, token).ConfigureAwait(false);
            await RateAsync(f.User, "Domain", "127.0.0.1", "relevant", null, token).ConfigureAwait(false);
            FeedbackProfile again = await FeedbackProfile.LoadAsync(services, ctx, token).ConfigureAwait(false);
            Assert.True(again.SourceScore("127.0.0.1", false, false) > 50 && again.SourceScore("unknown.test", false, false) == 50 && again.SourceScore("127.0.0.1", false, true) == 0, "domain reputation: " + again.SourceScore("127.0.0.1", false, false));

            Run second = await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            LlmCall plan = (await f.Server.Context.Database.LlmCalls.EnumerateAsync(new LlmCallFilter { TenantId = f.Topic.TenantId, RunId = second.Id, PromptKey = "research.queries", PageSize = 1 }, token).ConfigureAwait(false)).Items.Single();
            Assert.True(plan.PromptText!.Contains("Prefer source: Prefer primary sources such as regulators", StringComparison.Ordinal), "active preference in the next run's prompt");
            LlmCall score = (await f.Server.Context.Database.LlmCalls.EnumerateAsync(new LlmCallFilter { TenantId = f.Topic.TenantId, RunId = first.Id, PromptKey = "research.score", PageSize = 1 }, token).ConfigureAwait(false)).Items.First();
            Run third = await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            Assert.True(third.Status == RunStatusEnum.Completed, "later run completes");
            AnalyticsScope scope = new AnalyticsScope { TenantId = f.Topic.TenantId, UserId = f.Topic.UserId, TopicId = f.Topic.Id };
            List<string> examples = await f.Server.Context.Learning.ExemplarsAsync(f.Topic.TenantId, f.Topic.UserId, f.Topic.Id, token).ConfigureAwait(false);
            Assert.True(examples.Any(e => e.StartsWith("Not relevant: “Heat pump rebates", StringComparison.Ordinal) && e.EndsWith("because Too basic for me", StringComparison.Ordinal))
                && examples.Any(e => e.StartsWith("Relevant: ", StringComparison.Ordinal)) && score.PromptText != null, "exemplars with reasons");
        }

        private static async Task<PipelineFixture> RatedFixtureAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            Run run = await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            List<Finding> found = await FindingsAsync(f, run, token).ConfigureAwait(false);
            await RateAsync(f.User, "Finding", found[0].Id, "not-relevant", "Too basic, I know how they work", token).ConfigureAwait(false);
            await RateAsync(f.User, "Finding", found[1].Id, "somewhat", "Basic but has a date", token).ConfigureAwait(false);
            Prediction p = (await f.User.Client.GetAsync("/v1.0/api/predictions?topicId=" + f.Topic.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Prediction>>().Items.First();
            await RateAsync(f.User, "Prediction", p.Id, "not-relevant", "Explainer level prediction", token).ConfigureAwait(false);
            Suggestion s = (await f.User.Client.GetAsync("/v1.0/api/suggestions?topicId=" + f.Topic.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Suggestion>>().Items.First();
            await RateAsync(f.User, "Suggestion", s.Id, "not-useful", null, token).ConfigureAwait(false);
            return f;
        }

        private static async Task DistillAsync(CancellationToken token)
        {
            PipelineFixture f = await RatedFixtureAsync(token).ConfigureAwait(false);
            TestResponse learned = (await f.User.Client.PostAsync("/v1.0/api/topics/" + f.Topic.Id + "/learn", null, token).ConfigureAwait(false)).Expect(200, "learn now");
            Assert.True(learned.Body.Contains("\"proposed\":1", StringComparison.Ordinal), "one proposal (the unsupported one is dropped): " + learned.Body);
            List<FeedbackReasonTag> tags = (await f.Server.Context.Database.FeedbackReasonTags.EnumerateAsync(new FeedbackReasonTagFilter { TenantId = f.Topic.TenantId, TopicId = f.Topic.Id, PageSize = 50 }, token).ConfigureAwait(false)).Items;
            Assert.True(tags.Count == 3 && tags.Any(t => t.Tag == "too basic"), "reasons tagged (the rating without a reason is not): " + tags.Count);
            EnumerationResult<LearnedPreference> prefs = (await f.User.Client.GetAsync("/v1.0/api/learned-preferences?topicId=" + f.Topic.Id, token).ConfigureAwait(false)).Json<EnumerationResult<LearnedPreference>>();
            LearnedPreference lp = prefs.Items.Single();
            Assert.True(lp.Status == PreferenceStatusEnum.Proposed && lp.Origin == PreferenceOriginEnum.Distilled && lp.Kind == PreferenceKindEnum.Avoid && lp.SupportCount == 3 && lp.Confidence == 0.8, "proposed with support");
            PreferenceDetailResponse d = (await f.User.Client.GetAsync("/v1.0/api/learned-preferences/" + lp.Id, token).ConfigureAwait(false)).Expect(200, "detail").Json<PreferenceDetailResponse>();
            Assert.Equal(3, d.Support.Count, "supporting feedback listed");
            Assert.Equal(0, await f.Server.Context.Learning.LearnAsync(f.Topic.TenantId, f.Topic.UserId, f.Topic.Id, DateTime.UtcNow, token).ConfigureAwait(false), "the same preference is not proposed twice");
            Assert.True((await f.Server.Context.Database.Feedback.LearningCandidatesAsync(1, 500, token).ConfigureAwait(false)).All(c => c.TopicId != f.Topic.Id), "checkpoint moved past the feedback");
            (await f.User.Client.PostAsync("/v1.0/api/learned-preferences/" + lp.Id + "/accept", null, token).ConfigureAwait(false)).Expect(200, "accept");
            List<string> active = await f.Server.Context.Learning.ActivePreferencesAsync(f.Topic.TenantId, f.Topic.UserId, f.Topic.Id, token).ConfigureAwait(false);
            Assert.True(active.Contains("Avoid: Avoid basic explainers about how heat pumps work"), "active after accepting");
        }

        private static async Task PreferencesAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            LearnedPreference lp = (await f.User.Client.PostAsync("/v1.0/api/learned-preferences", new { statement = "Write for an engineer", kind = "StyleNote" }, token).ConfigureAwait(false)).Expect(201, "create").Json<LearnedPreference>();
            Assert.True(lp.Status == PreferenceStatusEnum.Active && lp.Origin == PreferenceOriginEnum.User && lp.TopicId == null, "user preference active for all topics");
            (await f.User.Client.PostAsync("/v1.0/api/learned-preferences", new { statement = " " }, token).ConfigureAwait(false)).Expect(400, "statement required");
            (await f.User.Client.PostAsync("/v1.0/api/learned-preferences", new { statement = "X", topicId = "top_missing" }, token).ConfigureAwait(false)).Expect(400, "topic must be mine");
            LearnedPreference edited = (await f.User.Client.PutAsync("/v1.0/api/learned-preferences/" + lp.Id, new { statement = "Write for an HVAC engineer", topicId = f.Topic.Id }, token).ConfigureAwait(false)).Expect(200, "edit").Json<LearnedPreference>();
            Assert.True(edited.Statement == "Write for an HVAC engineer" && edited.TopicId == f.Topic.Id, "edited");
            (await f.User.Client.PostAsync("/v1.0/api/learned-preferences/" + lp.Id + "/accept", null, token).ConfigureAwait(false)).Expect(409, "only proposals are accepted");
            Assert.Equal(PreferenceStatusEnum.Disabled, (await f.User.Client.PostAsync("/v1.0/api/learned-preferences/" + lp.Id + "/disable", null, token).ConfigureAwait(false)).Expect(200, "disable").Json<LearnedPreference>().Status, "disabled");
            Assert.Equal(0, (await f.Server.Context.Learning.ActivePreferencesAsync(f.Topic.TenantId, f.Topic.UserId, f.Topic.Id, token).ConfigureAwait(false)).Count, "disabled preferences are not used");
            Assert.Equal(PreferenceStatusEnum.Active, (await f.User.Client.PostAsync("/v1.0/api/learned-preferences/" + lp.Id + "/enable", null, token).ConfigureAwait(false)).Expect(200, "enable").Json<LearnedPreference>().Status, "enabled");
            TestUser other = await TestFixtures.CreateUserAsync(await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false), f.Topic.TenantId, false, token).ConfigureAwait(false);
            (await other.Client.GetAsync("/v1.0/api/learned-preferences/" + lp.Id, token).ConfigureAwait(false)).Expect(404, "private");
            Assert.Equal(0, (await other.Client.GetAsync("/v1.0/api/learned-preferences", token).ConfigureAwait(false)).Json<EnumerationResult<LearnedPreference>>().Items.Count, "other member lists none");
            (await f.User.Client.DeleteAsync("/v1.0/api/learned-preferences/" + lp.Id, token).ConfigureAwait(false)).Expect(204, "delete");
        }

        private static async Task EmailProposalsAsync(CancellationToken token)
        {
            PipelineFixture f = await RatedFixtureAsync(token).ConfigureAwait(false);
            await f.Server.Context.Learning.LearnAsync(f.Topic.TenantId, f.Topic.UserId, f.Topic.Id, DateTime.UtcNow, token).ConfigureAwait(false);
            LearnedPreference lp = (await f.User.Client.GetAsync("/v1.0/api/learned-preferences?status=Proposed&topicId=" + f.Topic.Id, token).ConfigureAwait(false)).Json<EnumerationResult<LearnedPreference>>().Items.Single();
            Run run = await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            EmailMessage m = (await f.Server.Context.Database.EmailMessages.EnumerateAsync(new EmailMessageFilter { TenantId = run.TenantId, RunId = run.Id, PageSize = 5 }, token).ConfigureAwait(false)).Items.Single();
            Assert.True(m.RenderedHtml!.Contains("What we learned from your feedback", StringComparison.Ordinal) && m.RenderedHtml.Contains(lp.Statement, StringComparison.Ordinal), "proposal in the digest");
            Match apply = Regex.Match(m.RenderedText!, @"Apply: (https?://\S+/v1\.0/public/feedback/[A-Za-z0-9_\-]+)\?a=apply");
            Assert.True(apply.Success, "apply link");
            TestApiClient anon = new TestApiClient(await SharedTestServer.GetBaseUrlAsync(token).ConfigureAwait(false));
            TestResponse page = (await anon.GetAsync(new Uri(apply.Groups[1].Value).AbsolutePath + "?a=apply", token).ConfigureAwait(false)).Expect(200, "page");
            Assert.True(page.Body.Contains(lp.Statement, StringComparison.Ordinal) && page.Body.Contains("value=\"apply\" checked", StringComparison.Ordinal), "landing page for the proposal");
            (await anon.SendAsync(HttpMethod.Post, new Uri(apply.Groups[1].Value).AbsolutePath, "a=apply", new Dictionary<string, string>(), token).ConfigureAwait(false)).Expect(200, "apply");
            Assert.Equal(PreferenceStatusEnum.Active, (await f.Server.Context.Database.LearnedPreferences.ReadAsync(lp.TenantId, lp.Id, token).ConfigureAwait(false))!.Status, "applied from email");
            (await f.User.Client.PostAsync("/v1.0/api/feedback", new { targetType = "Finding", targetId = lp.Id, action = "apply" }, token).ConfigureAwait(false)).Expect(400, "apply is only for preferences");
        }

        private static async Task FeedbackEditsAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            Run run = await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            Finding x = (await FindingsAsync(f, run, token).ConfigureAwait(false)).First();
            Feedback first = await RateAsync(f.User, "Finding", x.Id, "relevant", null, token).ConfigureAwait(false);
            Feedback second = await RateAsync(f.User, "Finding", x.Id, "not-relevant", "Changed my mind", token).ConfigureAwait(false);
            Feedback edited = (await f.User.Client.PutAsync("/v1.0/api/feedback/" + second.Id, new { reason = "Changed my mind after reading" }, token).ConfigureAwait(false)).Expect(200, "edit").Json<Feedback>();
            Assert.Equal("Changed my mind after reading", edited.Reason, "reason edited");
            (await f.User.Client.DeleteAsync("/v1.0/api/feedback/" + second.Id, token).ConfigureAwait(false)).Expect(204, "retract");
            EnumerationResult<Feedback> current = (await f.User.Client.GetAsync("/v1.0/api/feedback?targetType=Finding&targetId=" + x.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Feedback>>();
            Assert.True(current.Items.Count == 1 && current.Items[0].Id == first.Id && current.Items[0].Relevance == RelevanceRatingEnum.Relevant, "earlier rating is current again");
            TestUser other = await TestFixtures.CreateUserAsync(await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false), f.Topic.TenantId, false, token).ConfigureAwait(false);
            (await other.Client.DeleteAsync("/v1.0/api/feedback/" + first.Id, token).ConfigureAwait(false)).Expect(404, "cannot retract someone else's");
        }

        private static async Task AnalyticsAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            Run run = await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            List<Finding> found = await FindingsAsync(f, run, token).ConfigureAwait(false);
            await RateAsync(f.User, "Finding", found[0].Id, "relevant", "Great detail", token).ConfigureAwait(false);
            await RateAsync(f.User, "Finding", found[1].Id, "not-relevant", "Too basic", token).ConfigureAwait(false);
            string q = "?topicId=" + f.Topic.Id;
            RelevanceReport rel = (await f.User.Client.GetAsync("/v1.0/api/analytics/relevance" + q, token).ConfigureAwait(false)).Expect(200, "relevance").Json<RelevanceReport>();
            Assert.True(rel.Shown == 2 && rel.Rated == 2 && rel.Precision == 0.5 && rel.Coverage == 1 && rel.ByTier.Sum(t => t.Count) == 2 && rel.ByTopic.Single().Label == f.Topic.Name && rel.ByDomain.Single().Key == "127.0.0.1",
                "relevance precision and breakdowns");

            DateTime past = DateTime.UtcNow.AddDays(-1);
            double[] confidences = { 0.9, 0.9, 0.1 };
            PredictionStatusEnum[] outcomes = { PredictionStatusEnum.Accurate, PredictionStatusEnum.Inaccurate, PredictionStatusEnum.Inaccurate };
            for (int i = 0; i < 3; i++)
            {
                Prediction p = await f.Server.Context.Database.Predictions.CreateAsync(new Prediction { TenantId = f.Topic.TenantId, RunId = run.Id, TopicId = f.Topic.Id, UserId = f.Topic.UserId, Statement = "Seed " + i, Confidence = confidences[i], HorizonUtc = past }, token).ConfigureAwait(false);
                (await f.User.Client.PostAsync("/v1.0/api/predictions/" + p.Id + "/resolve", new { status = outcomes[i].ToString() }, token).ConfigureAwait(false)).Expect(200, "resolve " + i);
            }

            AccuracyReport acc = (await f.User.Client.GetAsync("/v1.0/api/analytics/accuracy" + q, token).ConfigureAwait(false)).Expect(200, "accuracy").Json<AccuracyReport>();
            Assert.True(acc.Resolved == 3 && Math.Abs(acc.AccuracyRate!.Value - 0.3333) < 0.001 && Math.Abs(acc.MeanBrier!.Value - (0.01 + 0.81 + 0.01) / 3) < 0.001, "accuracy and Brier: " + acc.AccuracyRate + " " + acc.MeanBrier);
            CalibrationBucket high = acc.Calibration.Single(b => b.Lower == 0.8);
            CalibrationBucket low = acc.Calibration.Single(b => b.Lower == 0.0);
            Assert.True(acc.Calibration.Count == 5 && high.Count == 2 && high.Observed == 0.5 && low.Count == 1 && low.Observed == 0, "calibration buckets");
            Assert.True(acc.Open >= 1 && acc.ByTopic.Single().Count == 3, "open and by topic");

            FeedbackReport fb = (await f.User.Client.GetAsync("/v1.0/api/analytics/feedback" + q, token).ConfigureAwait(false)).Expect(200, "feedback").Json<FeedbackReport>();
            Assert.True(fb.Total == 2 && fb.WithReason == 2 && fb.ByChannel.Single().Key == "Dashboard" && fb.ByTargetType.Single().Key == "Finding", "feedback volume");
            Suggestion s = (await f.User.Client.GetAsync("/v1.0/api/suggestions" + q, token).ConfigureAwait(false)).Json<EnumerationResult<Suggestion>>().Items.First();
            await f.User.Client.PutAsync("/v1.0/api/suggestions/" + s.Id + "/status", new { status = "Done" }, token).ConfigureAwait(false);
            SuggestionReport sug = (await f.User.Client.GetAsync("/v1.0/api/analytics/suggestions" + q, token).ConfigureAwait(false)).Expect(200, "suggestions").Json<SuggestionReport>();
            Assert.True(sug.Total == 1 && sug.Done == 1 && sug.MeanDaysToDecision.HasValue && sug.ByPriority.Single().Rate == 1, "suggestion outcomes");

            await f.Server.Context.Learning.LearnAsync(f.Topic.TenantId, f.Topic.UserId, f.Topic.Id, DateTime.UtcNow, token).ConfigureAwait(false);
            ReasonReport reasons = (await f.User.Client.GetAsync("/v1.0/api/analytics/reasons" + q, token).ConfigureAwait(false)).Expect(200, "reasons").Json<ReasonReport>();
            Assert.True(reasons.Tags.Sum(t => t.Count) == 2 && reasons.ByWeek.Count >= 1, "reason tags");
            QualityReport quality = (await f.User.Client.GetAsync("/v1.0/api/analytics/quality" + q, token).ConfigureAwait(false)).Expect(200, "quality").Json<QualityReport>();
            Assert.True(quality.Quotes == 4 && quality.Verified == 2 && quality.FailureRate == 0.5 && quality.ByModel.Single().Key == "stub-research", "quote verification by model: " + quality.Quotes + " " + quality.Verified);
            RankingRecommendation rr = (await f.User.Client.GetAsync("/v1.0/api/topics/" + f.Topic.Id + "/ranking-recommendation", token).ConfigureAwait(false)).Expect(200, "ranking").Json<RankingRecommendation>();
            Assert.True(!rr.Enough && rr.RatedFindings == 2 && rr.Current["llm"] == 0.45 && rr.Recommended["llm"] == 0.45, "not enough ratings yet");
            Topic weighted = (await f.User.Client.PutAsync("/v1.0/api/topics/" + f.Topic.Id, new { weightLlm = 0.6, weightRecency = 2.0, weightNovelty = -1.0 }, token).ConfigureAwait(false)).Expect(200, "set weights").Json<Topic>();
            Assert.True(weighted.WeightLlm == 0.6 && weighted.WeightRecency == 1.0 && weighted.WeightNovelty == null, "weights set, clamped, and cleared");
            Assert.Equal(0.6, (await f.User.Client.GetAsync("/v1.0/api/topics/" + f.Topic.Id + "/ranking-recommendation", token).ConfigureAwait(false)).Json<RankingRecommendation>().Current["llm"], "recommendation reads the override");

            TestUser other = await TestFixtures.CreateUserAsync(await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false), f.Topic.TenantId, false, token).ConfigureAwait(false);
            Assert.Equal(0, (await other.Client.GetAsync("/v1.0/api/analytics/relevance", token).ConfigureAwait(false)).Json<RelevanceReport>().Rated, "another member sees only their own");
            (await other.Client.GetAsync("/v1.0/api/analytics/relevance" + q, token).ConfigureAwait(false)).Expect(404, "not someone else's topic");
        }
    }
}
