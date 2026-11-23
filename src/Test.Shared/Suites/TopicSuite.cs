namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Topics, the refinement wizard, and one-time requests (Phase 4), against the stub model server.
    /// </summary>
    public static class TopicSuite
    {
        private const string S = "Topics";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Topics, refinement, and one-time requests", new[]
            {
                Cases.Case(S, "Topics.Crud", "Topics, dimensions, constraints, and recipients are managed with validation and unique slugs", CrudAsync),
                Cases.Case(S, "Topics.Ownership", "Members see only their own topics; tenant administrators see all", OwnershipAsync),
                Cases.Case(S, "Topics.Duplicate", "Duplicating a topic copies its dimensions and constraints as a draft", DuplicateAsync),
                Cases.Case(S, "Refinement.Wizard", "The wizard proposes dimensions, asks two rounds of questions, drafts context, checks sources, and applies", WizardAsync),
                Cases.Case(S, "Refinement.Resume", "A session resumes from the database with everything persisted", ResumeAsync),
                Cases.Case(S, "Refinement.OutOfScope", "A task that is not a topic is declined with a reason", OutOfScopeAsync),
                Cases.Case(S, "Refinement.Failure", "A model failure returns 502 and is recorded on the session", FailureAsync),
                Cases.Case(S, "Refinement.Abandon", "Abandoning a new session deletes its draft topic", AbandonAsync),
                Cases.Case(S, "Requests.Flow", "A one-time podcast request is planned, clarified, confirmed for later, and made recurring", RequestFlowAsync),
                Cases.Case(S, "Requests.Variants", "Requests can skip questions, be declined, cancelled, and deleted", RequestVariantsAsync)
            });
        }

        private static async Task<TestUser> TenantWithRefineModelAsync(string model, CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser ta = await TestFixtures.CreateUserAsync(admin, t.Id, true, token).ConfigureAwait(false);
            ModelEndpoint e = (await ta.Client.PostAsync("/v1.0/api/model-endpoints", new
            {
                name = "stub " + model, provider = "OpenAiCompatible", endpoint = StubModelServer.Shared.BaseUrl, model = model, supportsChat = true
            }, token).ConfigureAwait(false)).Expect(201, "endpoint").Json<ModelEndpoint>();
            (await ta.Client.PutAsync("/v1.0/api/model-assignments", new { task = "Refinement", modelEndpointId = e.Id }, token).ConfigureAwait(false)).Expect(200, "assign refinement");
            return ta;
        }

        private static async Task CrudAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser u = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            (await u.Client.PostAsync("/v1.0/api/topics", new { description = "no name" }, token).ConfigureAwait(false)).Expect(400, "name required");
            Topic a = (await u.Client.PostAsync("/v1.0/api/topics", new { name = "Heat Pumps!", description = "Residential heat pumps" }, token).ConfigureAwait(false)).Expect(201, "create").Json<Topic>();
            Topic b = (await u.Client.PostAsync("/v1.0/api/topics", new { name = "Heat pumps" }, token).ConfigureAwait(false)).Expect(201, "create second").Json<Topic>();
            Assert.True(a.Slug == "heat-pumps" && b.Slug == "heat-pumps-2" && a.Status == TopicStatusEnum.Draft, "unique slugs and draft status: " + a.Slug + ", " + b.Slug);

            (await u.Client.PostAsync("/v1.0/api/topics/" + a.Id + "/resume", null, token).ConfigureAwait(false)).Expect(409, "cannot activate without a brief");
            Topic updated = (await u.Client.PutAsync("/v1.0/api/topics/" + a.Id, new { name = "Home heat pumps", brief = "Track rebates.", noveltyPreference = 80, maxEmailFindings = 500 }, token).ConfigureAwait(false))
                .Expect(200, "update").Json<Topic>();
            Assert.True(updated.Slug == "home-heat-pumps" && updated.NoveltyPreference == 80 && updated.MaxEmailFindings == 50, "slug follows the name and values clamp");
            (await u.Client.PostAsync("/v1.0/api/topics/" + a.Id + "/resume", null, token).ConfigureAwait(false)).Expect(200, "activate");
            Assert.Equal(TopicStatusEnum.Paused, (await u.Client.PostAsync("/v1.0/api/topics/" + a.Id + "/pause", null, token).ConfigureAwait(false)).Expect(200, "pause").Json<Topic>().Status, "paused");

            TopicDimension d = (await u.Client.PostAsync("/v1.0/api/topics/" + a.Id + "/dimensions", new { name = "Rebates", weight = "High", includeTerms = new[] { "rebate", " " } }, token).ConfigureAwait(false))
                .Expect(201, "dimension").Json<TopicDimension>();
            Assert.True(d.Origin == OriginEnum.User && d.IncludeTerms.Count == 1, "user dimension");
            (await u.Client.PutAsync("/v1.0/api/topics/" + a.Id + "/dimensions/" + d.Id, new { isLocked = true }, token).ConfigureAwait(false)).Expect(200, "lock");
            (await u.Client.PostAsync("/v1.0/api/topics/" + a.Id + "/constraints", new { kind = "FeedUrl", value = "not a url" }, token).ConfigureAwait(false)).Expect(400, "bad feed url");
            (await u.Client.PostAsync("/v1.0/api/topics/" + a.Id + "/constraints", new { kind = "IncludeKeyword", value = "rebate", dimensionId = "dim_nope" }, token).ConfigureAwait(false)).Expect(400, "foreign dimension");
            TopicConstraint c = (await u.Client.PostAsync("/v1.0/api/topics/" + a.Id + "/constraints", new { kind = "PreferredDomain", value = "energy.gov", dimensionId = d.Id }, token).ConfigureAwait(false))
                .Expect(201, "constraint").Json<TopicConstraint>();
            (await u.Client.PutAsync("/v1.0/api/topics/" + a.Id + "/constraints/" + c.Id, new { note = "official" }, token).ConfigureAwait(false)).Expect(200, "update constraint");
            (await u.Client.PostAsync("/v1.0/api/topics/" + a.Id + "/recipients", new { email = "bad" }, token).ConfigureAwait(false)).Expect(400, "bad recipient");
            TopicRecipient r = (await u.Client.PostAsync("/v1.0/api/topics/" + a.Id + "/recipients", new { email = "reader@example.test" }, token).ConfigureAwait(false)).Expect(201, "recipient").Json<TopicRecipient>();
            (await u.Client.PostAsync("/v1.0/api/topics/" + a.Id + "/recipients", new { email = "READER@example.test", active = false }, token).ConfigureAwait(false)).Expect(200, "same recipient updates");

            TopicDetailResponse detail = (await u.Client.GetAsync("/v1.0/api/topics/" + a.Id, token).ConfigureAwait(false)).Expect(200, "detail").Json<TopicDetailResponse>();
            Assert.True(detail.Dimensions.Count == 1 && detail.Constraints.Count == 1 && detail.Recipients.Count == 1 && !detail.Recipients[0].Active, "detail");
            List<TopicConstraint> feeds = (await u.Client.GetAsync("/v1.0/api/topics/" + a.Id + "/constraints?kind=FeedUrl", token).ConfigureAwait(false)).Json<List<TopicConstraint>>();
            Assert.Equal(0, feeds.Count, "kind filter");

            (await u.Client.DeleteAsync("/v1.0/api/topics/" + a.Id + "/recipients/" + r.Id, token).ConfigureAwait(false)).Expect(204, "remove recipient");
            (await u.Client.DeleteAsync("/v1.0/api/topics/" + a.Id + "/dimensions/" + d.Id, token).ConfigureAwait(false)).Expect(204, "delete dimension");
            (await u.Client.DeleteAsync("/v1.0/api/topics/" + a.Id, token).ConfigureAwait(false)).Expect(204, "delete topic");
            (await u.Client.GetAsync("/v1.0/api/topics/" + a.Id, token).ConfigureAwait(false)).Expect(404, "gone");
            (await u.Client.GetAsync("/v1.0/api/topics/" + b.Id + "/constraints", token).ConfigureAwait(false)).Expect(200, "other topic intact");
        }

        private static async Task OwnershipAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser ta = await TestFixtures.CreateUserAsync(admin, t.Id, true, token).ConfigureAwait(false);
            TestUser one = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            TestUser two = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            Topic mine = (await one.Client.PostAsync("/v1.0/api/topics", new { name = "Mine" }, token).ConfigureAwait(false)).Expect(201, "create").Json<Topic>();
            await two.Client.PostAsync("/v1.0/api/topics", new { name = "Theirs" }, token).ConfigureAwait(false);
            EnumerationResult<Topic> oneList = (await one.Client.GetAsync("/v1.0/api/topics", token).ConfigureAwait(false)).Expect(200, "list").Json<EnumerationResult<Topic>>();
            Assert.True(oneList.Items.Count == 1 && oneList.Items[0].Id == mine.Id, "member sees own");
            (await two.Client.GetAsync("/v1.0/api/topics/" + mine.Id, token).ConfigureAwait(false)).Expect(404, "other member cannot read");
            (await two.Client.DeleteAsync("/v1.0/api/topics/" + mine.Id, token).ConfigureAwait(false)).Expect(404, "other member cannot delete");
            EnumerationResult<Topic> all = (await ta.Client.GetAsync("/v1.0/api/topics", token).ConfigureAwait(false)).Expect(200, "admin list").Json<EnumerationResult<Topic>>();
            Assert.Equal(2, all.Items.Count, "tenant admin sees all");
            (await ta.Client.GetAsync("/v1.0/api/topics/" + mine.Id, token).ConfigureAwait(false)).Expect(200, "tenant admin reads");
        }

        private static async Task DuplicateAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser u = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            Topic src = (await u.Client.PostAsync("/v1.0/api/topics", new { name = "Source", brief = "Brief." }, token).ConfigureAwait(false)).Json<Topic>();
            TopicDimension d = (await u.Client.PostAsync("/v1.0/api/topics/" + src.Id + "/dimensions", new { name = "Facet" }, token).ConfigureAwait(false)).Json<TopicDimension>();
            await u.Client.PostAsync("/v1.0/api/topics/" + src.Id + "/constraints", new { kind = "IncludeKeyword", value = "x", dimensionId = d.Id }, token).ConfigureAwait(false);
            TopicDetailResponse copy = (await u.Client.PostAsync("/v1.0/api/topics/" + src.Id + "/duplicate", null, token).ConfigureAwait(false)).Expect(201, "duplicate").Json<TopicDetailResponse>();
            Assert.True(copy.Topic.Id != src.Id && copy.Topic.Name == "Source (copy)" && copy.Topic.Status == TopicStatusEnum.Draft && copy.Topic.Brief == "Brief.", "copied topic");
            Assert.True(copy.Dimensions.Count == 1 && copy.Constraints.Count == 1 && copy.Constraints[0].DimensionId == copy.Dimensions[0].Id, "children copied and remapped");
        }

        private static async Task WizardAsync(CancellationToken token)
        {
            TestUser ta = await TenantWithRefineModelAsync("stub-refine", token).ConfigureAwait(false);
            RefinementStateResponse s = (await ta.Client.PostAsync("/v1.0/api/refinement-sessions", new { seedText = "I want to learn about AI developer tools", goal = "Work decisions" }, token).ConfigureAwait(false))
                .Expect(201, "start").Json<RefinementStateResponse>();
            Assert.True(s.Topic.Status == TopicStatusEnum.Draft && s.Topic.Name == "AI developer tools" && s.Session.Step == RefinementStepEnum.Describe, "draft topic named from the seed: " + s.Topic.Name);
            string b = "/v1.0/api/refinement-sessions/" + s.Session.Id;

            s = (await ta.Client.PostAsync(b + "/dimensions", null, token).ConfigureAwait(false)).Expect(200, "dimensions").Json<RefinementStateResponse>();
            Assert.Equal(8, s.Dimensions.Count, "eight dimensions");
            TopicDimension locked = s.Dimensions[3];
            (await ta.Client.PutAsync("/v1.0/api/topics/" + s.Topic.Id + "/dimensions/" + locked.Id, new { isLocked = true, name = "Release cadence" }, token).ConfigureAwait(false)).Expect(200, "lock and rename");
            s = (await ta.Client.PostAsync(b + "/dimensions", new { guidance = "more on pricing" }, token).ConfigureAwait(false)).Expect(200, "regenerate").Json<RefinementStateResponse>();
            Assert.True(s.Dimensions.Any(d => d.Id == locked.Id && d.Name == "Release cadence" && d.IsLocked) && s.Dimensions.Count == 9, "locked dimension kept (count " + s.Dimensions.Count + ")");

            s = (await ta.Client.PostAsync(b + "/questions", null, token).ConfigureAwait(false)).Expect(200, "round 1").Json<RefinementStateResponse>();
            Assert.True(s.Session.Round == 1 && s.Questions.Count == 5 && !s.Session.SufficientContext, "five questions");
            RefinementQuestionResponse single = s.Questions.First(q => q.Question.Kind == QuestionKindEnum.SingleChoice);
            RefinementQuestionResponse multi = s.Questions.First(q => q.Question.Kind == QuestionKindEnum.MultiChoice);
            RefinementQuestionResponse free = s.Questions.First(q => q.Question.Kind == QuestionKindEnum.FreeText);
            RefinementQuestionResponse scale = s.Questions.First(q => q.Question.Kind == QuestionKindEnum.Scale);
            RefinementQuestionResponse yesNo = s.Questions.First(q => q.Question.Kind == QuestionKindEnum.YesNo);
            Assert.True(single.Options.Count == 3 && single.Question.DimensionId != null, "options and dimension link");
            string qb = b + "/questions/";
            (await ta.Client.PutAsync(qb + single.Question.Id, new { selectedOptionIds = new[] { single.Options[0].Id, single.Options[1].Id } }, token).ConfigureAwait(false)).Expect(400, "single choice takes one");
            (await ta.Client.PutAsync(qb + single.Question.Id, new { selectedOptionIds = new[] { multi.Options[0].Id } }, token).ConfigureAwait(false)).Expect(400, "option from another question");
            (await ta.Client.PutAsync(qb + single.Question.Id, new { selectedOptionIds = new[] { single.Options[0].Id } }, token).ConfigureAwait(false)).Expect(200, "single");
            (await ta.Client.PutAsync(qb + multi.Question.Id, new { selectedOptionIds = new[] { multi.Options[0].Id, multi.Options[2].Id } }, token).ConfigureAwait(false)).Expect(200, "multi");
            (await ta.Client.PutAsync(qb + scale.Question.Id, new { answerText = "9" }, token).ConfigureAwait(false)).Expect(400, "scale range");
            (await ta.Client.PutAsync(qb + scale.Question.Id, new { answerText = "4" }, token).ConfigureAwait(false)).Expect(200, "scale");
            (await ta.Client.PutAsync(qb + yesNo.Question.Id, new { answerText = "maybe" }, token).ConfigureAwait(false)).Expect(400, "yes or no");
            (await ta.Client.PutAsync(qb + yesNo.Question.Id, new { answerText = "no" }, token).ConfigureAwait(false)).Expect(200, "yes no");
            (await ta.Client.PutAsync(qb + free.Question.Id, new { skipped = true }, token).ConfigureAwait(false)).Expect(200, "skip");

            s = (await ta.Client.PostAsync(b + "/questions", new { guidance = "focus on vendors" }, token).ConfigureAwait(false)).Expect(200, "round 2").Json<RefinementStateResponse>();
            Assert.True(s.Session.Round == 2 && s.Questions.Count == 10 && s.Session.SufficientContext, "second round uses the follow-up prompt");

            s = (await ta.Client.PostAsync(b + "/context", null, token).ConfigureAwait(false)).Expect(200, "context").Json<RefinementStateResponse>();
            Assert.True(!String.IsNullOrEmpty(s.Topic.Brief) && s.Topic.AudienceLevel == AudienceLevelEnum.Expert && s.Topic.Horizon == HorizonEnum.Months && s.Topic.NoveltyPreference == 100 && s.Topic.SeedQueries.Count == 2,
                "brief, levels, clamped novelty, seed queries");
            Assert.True(s.Constraints.Any(c => c.Kind == ConstraintKindEnum.PreferredDomain && c.Value == "example.com") && s.Constraints.Any(c => c.Kind == ConstraintKindEnum.TrackedQuestion), "constraints with normalized domains");

            List<SourceCheck> sources = (await ta.Client.PostAsync(b + "/sources", null, token).ConfigureAwait(false)).Expect(200, "sources").Json<List<SourceCheck>>();
            Assert.True(sources.Count == 3, "invalid URL dropped (" + sources.Count + ")");
            Assert.True(sources.Single(x => x.Url.EndsWith("/feed.xml", StringComparison.Ordinal)).Reachable && sources.Single(x => x.Url.EndsWith("/missing.xml", StringComparison.Ordinal)).StatusCode == 404, "live checks");
            s = (await ta.Client.PostAsync(b + "/sources/accept", new { feedUrls = new[] { sources[0].Url }, seedUrls = new string[0] }, token).ConfigureAwait(false)).Expect(200, "accept").Json<RefinementStateResponse>();
            Assert.True(s.Constraints.Count(c => c.Kind == ConstraintKindEnum.FeedUrl) == 1, "feed saved");

            s = (await ta.Client.PostAsync(b + "/complete", null, token).ConfigureAwait(false)).Expect(200, "complete").Json<RefinementStateResponse>();
            Assert.True(s.Topic.Status == TopicStatusEnum.Active && s.Session.Status == RefinementStatusEnum.Completed && s.Topic.LastRefinedUtc.HasValue, "applied");
            (await ta.Client.PostAsync(b + "/questions", null, token).ConfigureAwait(false)).Expect(409, "completed session is closed");

            EnumerationResult<LlmCall> calls = (await ta.Client.GetAsync("/v1.0/api/llm-calls?pageSize=50", token).ConfigureAwait(false)).Expect(200, "calls").Json<EnumerationResult<LlmCall>>();
            List<LlmCall> mine = calls.Items.Where(c => c.RefinementSessionId == s.Session.Id).ToList();
            Assert.True(mine.Count == 6 && mine.All(c => c.UserId == ta.User.Id && c.TopicId == s.Topic.Id && c.PromptTemplateVersionId != null), "six attributed calls (" + mine.Count + ")");
            Assert.True(mine.Any(c => c.PromptKey == "refinement.followup") && mine.Any(c => c.PromptKey == "refinement.sources"), "prompt keys recorded");

            RefinementStateResponse again = (await ta.Client.PostAsync("/v1.0/api/topics/" + s.Topic.Id + "/refine", null, token).ConfigureAwait(false)).Expect(201, "refine again").Json<RefinementStateResponse>();
            Assert.True(again.Session.Id != s.Session.Id && again.Topic.Id == s.Topic.Id && again.Dimensions.Count == 9 && again.Session.Step == RefinementStepEnum.Dimensions, "refine again starts from the topic");
        }

        private static async Task ResumeAsync(CancellationToken token)
        {
            TestUser ta = await TenantWithRefineModelAsync("stub-refine", token).ConfigureAwait(false);
            RefinementStateResponse s = (await ta.Client.PostAsync("/v1.0/api/refinement-sessions", new { seedText = "Keep me updated on heat pump rebates" }, token).ConfigureAwait(false)).Json<RefinementStateResponse>();
            await ta.Client.PostAsync("/v1.0/api/refinement-sessions/" + s.Session.Id + "/dimensions", null, token).ConfigureAwait(false);
            await ta.Client.PostAsync("/v1.0/api/refinement-sessions/" + s.Session.Id + "/questions", null, token).ConfigureAwait(false);
            TestApiClient fresh = new TestApiClient(ta.Client.BaseUrl);
            await fresh.LoginAsync(s.Topic.TenantId, ta.User.Email, ta.Password, token).ConfigureAwait(false);
            RefinementStateResponse resumed = (await fresh.GetAsync("/v1.0/api/refinement-sessions/" + s.Session.Id, token).ConfigureAwait(false)).Expect(200, "resume").Json<RefinementStateResponse>();
            Assert.True(resumed.Dimensions.Count == 8 && resumed.Questions.Count == 5 && resumed.Messages.Count == 3 && resumed.Session.Step == RefinementStepEnum.Questions, "state persisted");
            EnumerationResult<RefinementSession> list = (await fresh.GetAsync("/v1.0/api/refinement-sessions?status=Active", token).ConfigureAwait(false)).Json<EnumerationResult<RefinementSession>>();
            Assert.True(list.Items.Any(x => x.Id == s.Session.Id), "listed as active");
        }

        private static async Task OutOfScopeAsync(CancellationToken token)
        {
            TestUser ta = await TenantWithRefineModelAsync("stub-refine", token).ConfigureAwait(false);
            RefinementStateResponse s = (await ta.Client.PostAsync("/v1.0/api/refinement-sessions", new { seedText = "Write me a Python web scraper for news sites" }, token).ConfigureAwait(false)).Json<RefinementStateResponse>();
            s = (await ta.Client.PostAsync("/v1.0/api/refinement-sessions/" + s.Session.Id + "/dimensions", null, token).ConfigureAwait(false)).Expect(200, "declined").Json<RefinementStateResponse>();
            Assert.True(s.Dimensions.Count == 0 && !String.IsNullOrEmpty(s.Session.OutOfScopeReason) && s.Session.OutOfScopeReason!.Contains("does not write code", StringComparison.Ordinal), "out of scope reason");
            (await ta.Client.PostAsync("/v1.0/api/refinement-sessions/" + s.Session.Id + "/questions", null, token).ConfigureAwait(false)).Expect(409, "no questions without dimensions");
        }

        private static async Task FailureAsync(CancellationToken token)
        {
            TestUser ta = await TenantWithRefineModelAsync("stub-error", token).ConfigureAwait(false);
            RefinementStateResponse s = (await ta.Client.PostAsync("/v1.0/api/refinement-sessions", new { seedText = "Solar batteries" }, token).ConfigureAwait(false)).Json<RefinementStateResponse>();
            TestResponse r = (await ta.Client.PostAsync("/v1.0/api/refinement-sessions/" + s.Session.Id + "/dimensions", null, token).ConfigureAwait(false)).Expect(502, "model error");
            Assert.True(r.Body.Contains("ModelError", StringComparison.Ordinal), "typed error");
            RefinementStateResponse after = (await ta.Client.GetAsync("/v1.0/api/refinement-sessions/" + s.Session.Id, token).ConfigureAwait(false)).Json<RefinementStateResponse>();
            Assert.True(!String.IsNullOrEmpty(after.Session.LastError), "last error stored for the failure panel");
            (await ta.Client.PostAsync("/v1.0/api/refinement-sessions", new { seedText = "" }, token).ConfigureAwait(false)).Expect(400, "seed required");
        }

        private static async Task AbandonAsync(CancellationToken token)
        {
            TestUser ta = await TenantWithRefineModelAsync("stub-refine", token).ConfigureAwait(false);
            RefinementStateResponse s = (await ta.Client.PostAsync("/v1.0/api/refinement-sessions", new { seedText = "Quantum networking" }, token).ConfigureAwait(false)).Json<RefinementStateResponse>();
            (await ta.Client.PostAsync("/v1.0/api/refinement-sessions/" + s.Session.Id + "/abandon", null, token).ConfigureAwait(false)).Expect(204, "abandon");
            (await ta.Client.GetAsync("/v1.0/api/topics/" + s.Topic.Id, token).ConfigureAwait(false)).Expect(404, "draft topic deleted");
            Topic existing = (await ta.Client.PostAsync("/v1.0/api/topics", new { name = "Existing draft " + Guid.NewGuid().ToString("N").Substring(0, 6) }, token).ConfigureAwait(false)).Expect(201, "existing draft").Json<Topic>();
            Assert.Equal(TopicStatusEnum.Draft, existing.Status, "created as a draft");
            RefinementStateResponse again = (await ta.Client.PostAsync("/v1.0/api/topics/" + existing.Id + "/refine", null, token).ConfigureAwait(false)).Json<RefinementStateResponse>();
            (await ta.Client.PostAsync("/v1.0/api/refinement-sessions/" + again.Session.Id + "/abandon", null, token).ConfigureAwait(false)).Expect(204, "abandon refining an existing topic");
            (await ta.Client.GetAsync("/v1.0/api/topics/" + existing.Id, token).ConfigureAwait(false)).Expect(200, "an existing topic survives an abandoned refinement");
        }

        private static async Task RequestFlowAsync(CancellationToken token)
        {
            TestUser ta = await TenantWithRefineModelAsync("stub-refine", token).ConfigureAwait(false);
            ResearchRequestResponse r = (await ta.Client.PostAsync("/v1.0/api/requests", new
            {
                requestText = "Build me a podcast on the history of the shipping container",
                deliverables = new[] { "podcast", "Email", "PODCAST" },
                length = "Short",
                publishToFeed = true
            }, token).ConfigureAwait(false)).Expect(201, "create").Json<ResearchRequestResponse>();
            Assert.True(r.Request.Status == ResearchRequestStatusEnum.Clarifying && r.Request.Title == "The shipping container revolution", "planned: " + r.Request.Title);
            Assert.True(r.Request.Deliverables.SequenceEqual(new[] { "Podcast", "Email" }) && r.Request.EmailRecipients.SequenceEqual(new[] { ta.User.Email }) && r.Request.PublishToFeed, "deliverables normalized, recipients default to me");
            Assert.True(r.Topic != null && r.Topic.IsOneTime && r.Topic.Brief!.Length > 0 && r.Dimensions.Count == 2 && r.Questions.Count == 1, "one-time topic with plan and one question");

            RefinementQuestionResponse q = r.Questions[0];
            (await ta.Client.PutAsync("/v1.0/api/requests/" + r.Request.Id + "/questions/" + q.Question.Id, new { selectedOptionIds = new[] { q.Options[0].Id } }, token).ConfigureAwait(false)).Expect(200, "answer");
            r = (await ta.Client.PostAsync("/v1.0/api/requests/" + r.Request.Id + "/replan", null, token).ConfigureAwait(false)).Expect(200, "replan").Json<ResearchRequestResponse>();
            Assert.True(r.Questions.Count == 1 && r.Questions[0].Question.Answered, "no new questions after answering");

            DateTime later = DateTime.UtcNow.AddDays(2);
            r = (await ta.Client.PostAsync("/v1.0/api/requests/" + r.Request.Id + "/confirm", new { runAtUtc = later }, token).ConfigureAwait(false)).Expect(200, "confirm").Json<ResearchRequestResponse>();
            Assert.True(r.Request.Status == ResearchRequestStatusEnum.Scheduled && r.Request.RunAtUtc.HasValue && Math.Abs((r.Request.RunAtUtc.Value - later).TotalSeconds) < 2 && r.Topic != null && r.Topic.Status == TopicStatusEnum.Active, "scheduled once");
            (await ta.Client.PostAsync("/v1.0/api/requests/" + r.Request.Id + "/confirm", null, token).ConfigureAwait(false)).Expect(409, "already confirmed");

            Topic converted = (await ta.Client.PostAsync("/v1.0/api/requests/" + r.Request.Id + "/convert", null, token).ConfigureAwait(false)).Expect(200, "convert").Json<Topic>();
            Assert.True(!converted.IsOneTime && converted.Status == TopicStatusEnum.Active && converted.Id == r.Request.TopicId, "now a recurring topic");
            Assert.True(converted.AudioEnabled, "a request that asked for a podcast keeps automatic audio as a topic");
            (await ta.Client.PostAsync("/v1.0/api/requests/" + r.Request.Id + "/convert", null, token).ConfigureAwait(false)).Expect(409, "already converted");
            (await ta.Client.DeleteAsync("/v1.0/api/requests/" + r.Request.Id, token).ConfigureAwait(false)).Expect(204, "delete request");
            (await ta.Client.GetAsync("/v1.0/api/topics/" + converted.Id, token).ConfigureAwait(false)).Expect(200, "converted topic survives deletion");

            EnumerationResult<LlmCall> calls = (await ta.Client.GetAsync("/v1.0/api/llm-calls?pageSize=50", token).ConfigureAwait(false)).Json<EnumerationResult<LlmCall>>();
            Assert.True(calls.Items.Count(c => c.PromptKey == "refinement.quick" && c.UserId == ta.User.Id) == 2, "quick planning calls attributed");
        }

        private static async Task RequestVariantsAsync(CancellationToken token)
        {
            TestUser ta = await TenantWithRefineModelAsync("stub-refine", token).ConfigureAwait(false);
            (await ta.Client.PostAsync("/v1.0/api/requests", new { requestText = "x", deliverables = new[] { "Video" } }, token).ConfigureAwait(false)).Expect(400, "unknown deliverable");
            (await ta.Client.PostAsync("/v1.0/api/requests", new { requestText = "  " }, token).ConfigureAwait(false)).Expect(400, "text required");

            ResearchRequestResponse quick = (await ta.Client.PostAsync("/v1.0/api/requests", new { requestText = "Report on container shipping rates", skipQuestions = true }, token).ConfigureAwait(false))
                .Expect(201, "skip questions").Json<ResearchRequestResponse>();
            Assert.True(quick.Questions.Count == 0 && quick.Request.Deliverables.SequenceEqual(new[] { "Report" }) && quick.Request.EmailRecipients.Count == 0, "no questions, report by default");
            ResearchRequestResponse now = (await ta.Client.PostAsync("/v1.0/api/requests/" + quick.Request.Id + "/confirm", null, token).ConfigureAwait(false)).Expect(200, "run now").Json<ResearchRequestResponse>();
            Assert.True(now.Request.RunAtUtc.HasValue && now.Request.RunAtUtc.Value <= DateTime.UtcNow.AddSeconds(5), "runs as soon as possible");
            ResearchRequestResponse cancelled = (await ta.Client.PostAsync("/v1.0/api/requests/" + quick.Request.Id + "/cancel", null, token).ConfigureAwait(false)).Expect(200, "cancel").Json<ResearchRequestResponse>();
            Assert.Equal(ResearchRequestStatusEnum.Cancelled, cancelled.Request.Status, "cancelled");
            (await ta.Client.PostAsync("/v1.0/api/requests/" + quick.Request.Id + "/cancel", null, token).ConfigureAwait(false)).Expect(409, "already cancelled");

            ResearchRequestResponse declined = (await ta.Client.PostAsync("/v1.0/api/requests", new { requestText = "Write code for a python web scraper" }, token).ConfigureAwait(false))
                .Expect(201, "declined").Json<ResearchRequestResponse>();
            Assert.True(declined.Request.Status == ResearchRequestStatusEnum.Declined && !String.IsNullOrEmpty(declined.Request.OutOfScopeReason), "declined with reason");
            (await ta.Client.PostAsync("/v1.0/api/requests/" + declined.Request.Id + "/confirm", null, token).ConfigureAwait(false)).Expect(409, "declined cannot run");
            string topicId = declined.Request.TopicId!;
            (await ta.Client.DeleteAsync("/v1.0/api/requests/" + declined.Request.Id, token).ConfigureAwait(false)).Expect(204, "delete");
            (await ta.Client.GetAsync("/v1.0/api/topics/" + topicId, token).ConfigureAwait(false)).Expect(404, "one-time topic deleted with the request");

            TestUser other = await TestFixtures.CreateUserAsync(await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false), quick.Request.TenantId, false, token).ConfigureAwait(false);
            (await other.Client.GetAsync("/v1.0/api/requests/" + quick.Request.Id, token).ConfigureAwait(false)).Expect(404, "private to the owner");
            EnumerationResult<ResearchRequest> list = (await ta.Client.GetAsync("/v1.0/api/requests?status=Cancelled", token).ConfigureAwait(false)).Json<EnumerationResult<ResearchRequest>>();
            Assert.True(list.Items.Count == 1 && list.Items[0].Id == quick.Request.Id, "status filter");
        }
    }
}
