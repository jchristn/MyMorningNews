namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Entitlements;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>Regression checks for the shared collection retrieval contract over real HTTP and PostgreSQL.</summary>
    public static class EnumerationSuite
    {
        /// <summary>Build the suite.</summary>
        /// <returns>Enumeration regression suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor("Enumeration", "Collection retrieval contracts", new[]
            {
                Cases.Case("Enumeration", "Pages", "Collection GETs and POST twins return exact pages, bounds, totals, and no legacy arrays", PagesAsync),
                Cases.Case("Enumeration", "BeyondMaximum", "Collections beyond 1000 records remain reachable with exact totals and stable ordering", BeyondMaximumAsync),
                Cases.Case("Enumeration", "CrossTenantRedemptions", "Global redemptions page across tenants, retain exact totals, and deny members", RedemptionsAsync)
            });
        }

        private static async Task PagesAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant tenant = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser owner = await TestFixtures.CreateUserAsync(admin, tenant.Id, true, token).ConfigureAwait(false);
            TestUser other = await TestFixtures.CreateUserAsync(admin, tenant.Id, false, token).ConfigureAwait(false);
            DatabaseDriverBase db = (await SharedTestServer.GetServerAsync(token).ConfigureAwait(false)).Context.Database;
            Topic topic = await db.Topics.CreateAsync(new Topic { TenantId = tenant.Id, UserId = owner.User.Id, Name = "Enumeration", Slug = "enumeration" }, token).ConfigureAwait(false);
            Run run = await db.Runs.CreateAsync(new Run { TenantId = tenant.Id, UserId = owner.User.Id, TopicId = topic.Id }, token).ConfigureAwait(false);
            for (int i = 0; i < 3; i++)
            {
                await db.TopicDimensions.CreateAsync(new TopicDimension { TenantId = tenant.Id, TopicId = topic.Id, Name = "Dimension " + i, SortOrder = i }, token).ConfigureAwait(false);
                await db.TopicConstraints.CreateAsync(new TopicConstraint { TenantId = tenant.Id, TopicId = topic.Id, Value = "Constraint " + i }, token).ConfigureAwait(false);
                await db.TopicRecipients.CreateAsync(new TopicRecipient { TenantId = tenant.Id, TopicId = topic.Id, Email = "recipient" + i + "@example.test" }, token).ConfigureAwait(false);
                await db.RunStages.CreateAsync(new RunStage { TenantId = tenant.Id, RunId = run.Id, Stage = "Stage " + i, SortOrder = i }, token).ConfigureAwait(false);
                SearchQuery query = await db.SearchQueries.CreateAsync(new SearchQuery { TenantId = tenant.Id, TopicId = topic.Id, RunId = run.Id, QueryText = "Query " + i }, token).ConfigureAwait(false);
                await db.SearchResults.CreateAsync(new SearchResult { TenantId = tenant.Id, TopicId = topic.Id, RunId = run.Id, SearchQueryId = query.Id, Url = "https://example.test/" + i, CanonicalUrlHash = "hash" + i, Title = "Result " + i }, token).ConfigureAwait(false);
                await db.RunToolCalls.CreateAsync(new RunToolCall { TenantId = tenant.Id, TopicId = topic.Id, RunId = run.Id, Tool = "tool" + i, SortOrder = i }, token).ConfigureAwait(false);
                await db.Devices.CreateAsync(new Device { TenantId = tenant.Id, UserId = owner.User.Id, ExpoPushToken = "ExponentPushToken[enumeration-" + i + "]", Platform = "ios" }, token).ConfigureAwait(false);
                await db.FeedTokens.CreateAsync(new FeedToken { TenantId = tenant.Id, UserId = owner.User.Id, Name = "Feed " + i, TokenHash = Guid.NewGuid().ToString("N"), TokenPrefix = "test" }, token).ConfigureAwait(false);
            }
            string topicPath = "/v1.0/api/topics/" + topic.Id;
            string runPath = "/v1.0/api/runs/" + run.Id;
            foreach (string path in new[] { topicPath + "/dimensions", topicPath + "/constraints", topicPath + "/recipients", runPath + "/stages", runPath + "/queries", runPath + "/results", runPath + "/tool-calls", "/v1.0/api/devices", "/v1.0/api/feed-tokens" })
                await VerifyPagesAsync(owner.Client, path, 3, token).ConfigureAwait(false);
            (await other.Client.GetAsync(topicPath + "/dimensions", token).ConfigureAwait(false)).Expect(404, "children are owner scoped");
            (await other.Client.PostAsync(runPath + "/stages/enumerate", new { pageSize = 1 }, token).ConfigureAwait(false)).Expect(404, "POST has the same authorization");
            await VerifyPagesAsync(other.Client, "/v1.0/api/devices", 0, token).ConfigureAwait(false);
            EnumerationResult<TopicDimension> filtered = (await owner.Client.GetAsync(topicPath + "/dimensions?search=Dimension%201&pageSize=1", token).ConfigureAwait(false)).Expect(200, "filtered page").Json<EnumerationResult<TopicDimension>>();
            Assert.Equal(1L, filtered.TotalRecords, "count applies the same search filter");
            Assert.Equal("Dimension 1", filtered.Objects.Single().Name, "matching row");
        }

        private static async Task VerifyPagesAsync(TestApiClient client, string path, int count, CancellationToken token)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int number = 1; number <= count + 1; number++)
            {
                TestResponse response = (await client.GetAsync(path + "?pageSize=1&pageNumber=" + number, token).ConfigureAwait(false)).Expect(200, path);
                using (JsonDocument json = JsonDocument.Parse(response.Body))
                {
                    JsonElement root = json.RootElement;
                    Assert.Equal(JsonValueKind.Object, root.ValueKind, "never a bare array");
                    Assert.True(root.GetProperty("success").GetBoolean(), "success flag");
                    Assert.True(!root.TryGetProperty("items", out _) && !root.TryGetProperty("totalCount", out _) && !root.TryGetProperty("durationMs", out _), "only Armada envelope fields");
                    Assert.True(root.GetProperty("totalMs").GetDouble() >= 0, "query timing");
                }
                EnumerationResult<JsonElement> page = response.Json<EnumerationResult<JsonElement>>();
                Assert.Equal(number, page.PageNumber, "requested page");
                Assert.Equal(1, page.PageSize, "requested size");
                Assert.Equal((long)count, page.TotalRecords, "exact total");
                Assert.Equal(count, page.TotalPages, "exact pages");
                Assert.Equal(number <= count ? 1 : 0, page.Objects.Count, "bounded or empty page");
                foreach (JsonElement item in page.Objects) Assert.True(ids.Add(item.GetProperty("id").GetString()!), "no duplicates across pages");
                EnumerationResult<JsonElement> post = (await client.PostAsync(path + "/enumerate?pageSize=1", new { pageNumber = number, pageSize = 3 }, token).ConfigureAwait(false)).Expect(200, "POST twin").Json<EnumerationResult<JsonElement>>();
                Assert.Equal(page.Objects.FirstOrDefault().ToString(), post.Objects.FirstOrDefault().ToString(), "GET and POST match, querystring overrides body");
                Assert.Equal(1, post.PageSize, "querystring size wins");
            }
            EnumerationResult<JsonElement> maximum = (await client.GetAsync(path + "?pageSize=2147483647&pageNumber=-1", token).ConfigureAwait(false)).Expect(200, "bounds").Json<EnumerationResult<JsonElement>>();
            Assert.Equal(1000, maximum.PageSize, "hard maximum");
            Assert.Equal(1, maximum.PageNumber, "minimum page");
            EnumerationResult<JsonElement> minimum = (await client.GetAsync(path + "?pageSize=0", token).ConfigureAwait(false)).Expect(200, "minimum").Json<EnumerationResult<JsonElement>>();
            Assert.Equal(1, minimum.PageSize, "minimum size");
            (await client.PostAsync(path + "/enumerate", "{broken", token).ConfigureAwait(false)).Expect(400, "malformed enumeration JSON");
            (await client.PostAsync(path + "/enumerate", new[] { 1, 2 }, token).ConfigureAwait(false)).Expect(400, "array query rejected");
        }

        private static async Task BeyondMaximumAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant tenant = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser owner = await TestFixtures.CreateUserAsync(admin, tenant.Id, true, token).ConfigureAwait(false);
            DatabaseDriverBase db = (await SharedTestServer.GetServerAsync(token).ConfigureAwait(false)).Context.Database;
            Topic topic = await db.Topics.CreateAsync(new Topic { TenantId = tenant.Id, UserId = owner.User.Id, Name = "Large enumeration", Slug = "large-enumeration" }, token).ConfigureAwait(false);
            DateTime sameTime = DateTime.UtcNow;
            for (int i = 0; i < 1001; i++)
                await db.TopicDimensions.CreateAsync(new TopicDimension { TenantId = tenant.Id, TopicId = topic.Id, Name = "Dimension " + i, CreatedUtc = sameTime }, token).ConfigureAwait(false);
            string path = "/v1.0/api/topics/" + topic.Id + "/dimensions";
            EnumerationResult<TopicDimension> first = (await owner.Client.GetAsync(path + "?pageSize=1000", token).ConfigureAwait(false)).Expect(200, "first large page").Json<EnumerationResult<TopicDimension>>();
            EnumerationResult<TopicDimension> second = (await owner.Client.GetAsync(path + "?pageSize=1000&pageNumber=2", token).ConfigureAwait(false)).Expect(200, "second large page").Json<EnumerationResult<TopicDimension>>();
            Assert.Equal(1001L, first.TotalRecords, "count is not capped at 1000");
            Assert.Equal(2, first.TotalPages, "second page advertised");
            Assert.Equal(1000, first.Objects.Count, "maximum bounded response");
            Assert.Equal(1, second.Objects.Count, "last record is reachable");
            Assert.True(!first.Objects.Any(x => x.Id == second.Objects[0].Id), "equal sort values use a stable ID tie-break");
            EnumerationResult<TopicDimension> defaults = (await owner.Client.GetAsync(path, token).ConfigureAwait(false)).Expect(200, "default page").Json<EnumerationResult<TopicDimension>>();
            Assert.Equal(10, defaults.PageSize, "Armada default size");
            Assert.Equal(10, defaults.Objects.Count, "default remains bounded");
            (await owner.Client.GetAsync(path + "?pageSize=garbage", token).ConfigureAwait(false)).Expect(400, "malformed size");
            (await owner.Client.PostAsync(path + "/enumerate", new { pageSize = "garbage" }, token).ConfigureAwait(false)).Expect(400, "malformed body size");
            EnumerationResult<TopicDimension> huge = (await owner.Client.GetAsync(path + "?pageSize=1000&pageNumber=2147483647", token).ConfigureAwait(false)).Expect(200, "overflow-safe offset").Json<EnumerationResult<TopicDimension>>();
            Assert.Equal(0, huge.Objects.Count, "huge page is empty");
            Assert.Equal(1001L, huge.TotalRecords, "huge page retains exact total");
        }

        private static async Task RedemptionsAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            DatabaseDriverBase db = (await SharedTestServer.GetServerAsync(token).ConfigureAwait(false)).Context.Database;
            Plan plan = (await db.Plans.EnumerateAsync(new PlanFilter { PageSize = 1 }, token).ConfigureAwait(false)).Objects.Single();
            PromoCode code = await db.PromoCodes.CreateAsync(new PromoCode { Code = "PAGE" + Guid.NewGuid().ToString("N"), PlanId = plan.Id }, token).ConfigureAwait(false);
            TestUser? member = null;
            for (int i = 0; i < 3; i++)
            {
                Tenant tenant = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
                member = await TestFixtures.CreateUserAsync(admin, tenant.Id, false, token).ConfigureAwait(false);
                await db.PromoRedemptions.CreateAsync(new PromoRedemption { TenantId = tenant.Id, UserId = member.User.Id, PromoCodeId = code.Id, Code = code.Code, Email = member.User.Email }, token).ConfigureAwait(false);
            }
            string path = "/v1.0/api/admin/promo-codes/" + code.Id + "/redemptions";
            await VerifyPagesAsync(admin, path, 3, token).ConfigureAwait(false);
            (await member!.Client.GetAsync(path, token).ConfigureAwait(false)).Expect(403, "global administrators only");
            (await member.Client.PostAsync(path + "/enumerate", new { allTenants = true }, token).ConfigureAwait(false)).Expect(403, "body cannot escalate scope");
        }
    }
}
