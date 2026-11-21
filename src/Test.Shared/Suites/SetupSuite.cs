namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Responses;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Workspace setup: required model keys and web search are collected and verified before people can use the product.
    /// </summary>
    public static class SetupSuite
    {
        private const string S = "Setup";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Setup checks for model keys and web search", new[]
            {
                Cases.Case(S, "Missing", "A new workspace reports its missing model key, members cannot configure it, and the wizard and requests are refused", MissingAsync),
                Cases.Case(S, "KeyFailed", "A stored key is kept in the vault, rotated in place, and a key that fails its live check blocks the wizard", KeyFailedAsync),
                Cases.Case(S, "Ready", "Working models and search verify with live calls, a successful call counts as proof, and edits clear it", ReadyAsync)
            });
        }

        private static async Task<TestUser[]> WorkspaceAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser ta = await TestFixtures.CreateUserAsync(admin, t.Id, true, token).ConfigureAwait(false);
            TestUser member = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            return new[] { ta, member };
        }

        private static async Task<SetupStatusResponse> StatusAsync(TestUser u, CancellationToken token)
        {
            return (await u.Client.GetAsync("/v1.0/api/setup", token).ConfigureAwait(false)).Expect(200, "setup status").Json<SetupStatusResponse>();
        }

        private static async Task MissingAsync(CancellationToken token)
        {
            TestUser[] users = await WorkspaceAsync(token).ConfigureAwait(false);
            TestUser ta = users[0];
            TestUser member = users[1];
            SetupStatusResponse s = await StatusAsync(ta, token).ConfigureAwait(false);
            Assert.True(!s.Ready && s.CanConfigure, "not ready, and the tenant admin can configure it");
            SetupCheck model = s.Checks.First(c => c.Kind == SetupCheckKindEnum.Model);
            Assert.True(model.Required && model.State == SetupCheckStateEnum.Missing && model.NeedsApiKey && !model.HasApiKey && !model.UsesPlatformKey, "the seeded Anthropic endpoint needs a key: " + model.State);
            Assert.True(model.Tasks.Contains(ModelTaskEnum.Refinement) && !model.Tasks.Contains(ModelTaskEnum.Embedding), "the model check lists the chat tasks it serves");
            Assert.True(s.Checks.Any(c => c.Kind == SetupCheckKindEnum.Search && c.Required), "search is required");
            Assert.True(s.Checks.Where(c => c.Kind == SetupCheckKindEnum.Embeddings || c.Kind == SetupCheckKindEnum.Email).All(c => !c.Required)
                && s.Checks.Count(c => c.Kind == SetupCheckKindEnum.Embeddings || c.Kind == SetupCheckKindEnum.Email) == 2, "embeddings and email are optional checks for admins");

            SetupStatusResponse m = await StatusAsync(member, token).ConfigureAwait(false);
            Assert.True(!m.Ready && !m.CanConfigure, "a member sees the same readiness and cannot configure");
            Assert.True(!m.Checks.Any(c => c.Kind == SetupCheckKindEnum.Email || c.Kind == SetupCheckKindEnum.Embeddings), "members do not see server details");
            (await member.Client.PostAsync("/v1.0/api/setup/verify", null, token).ConfigureAwait(false)).Expect(403, "member verify");
            (await member.Client.PostAsync("/v1.0/api/setup/model-key", new { modelEndpointId = model.ResourceId, apiKey = "sk-ant-x" }, token).ConfigureAwait(false)).Expect(403, "member key");

            ErrorResponse wizard = (await ta.Client.PostAsync("/v1.0/api/refinement-sessions", new { seedText = "Heat pump rebates" }, token).ConfigureAwait(false))
                .Expect(409, "wizard refused").Json<ErrorResponse>();
            Assert.True(wizard.Error == "SetupRequired" && wizard.Message.Contains("needs an API key", StringComparison.Ordinal) && wizard.Message.Contains("Setup page", StringComparison.Ordinal), "wizard: " + wizard.Message);
            ErrorResponse request = (await ta.Client.PostAsync("/v1.0/api/requests", new { requestText = "A podcast about heat pumps", deliverables = new[] { "Podcast" } }, token).ConfigureAwait(false))
                .Expect(409, "request refused").Json<ErrorResponse>();
            Assert.Equal("SetupRequired", request.Error, "request error code");
            long topics = (await ta.Client.GetAsync("/v1.0/api/topics", token).ConfigureAwait(false)).Expect(200, "topics").Json<EnumerationResult<Topic>>().TotalCount;
            Assert.Equal(0L, topics, "no draft topic is left behind");
        }

        private static async Task KeyFailedAsync(CancellationToken token)
        {
            TestUser ta = (await WorkspaceAsync(token).ConfigureAwait(false))[0];
            SetupCheck model = (await StatusAsync(ta, token).ConfigureAwait(false)).Checks.First(c => c.Kind == SetupCheckKindEnum.Model);
            (await ta.Client.PostAsync("/v1.0/api/setup/model-key", new { modelEndpointId = model.ResourceId }, token).ConfigureAwait(false)).Expect(400, "key required");
            (await ta.Client.PostAsync("/v1.0/api/setup/model-key", new { modelEndpointId = "mdl_missing", apiKey = "k" }, token).ConfigureAwait(false)).Expect(404, "unknown endpoint");

            // Point the Anthropic endpoint at the stub, which does not speak the Anthropic protocol: the live check fails.
            (await ta.Client.PutAsync("/v1.0/api/model-endpoints/" + model.ResourceId, new { endpoint = StubModelServer.Shared.BaseUrl + "/anthropic" }, token).ConfigureAwait(false)).Expect(200, "move endpoint");
            SetupStatusResponse after = (await ta.Client.PostAsync("/v1.0/api/setup/model-key", new { modelEndpointId = model.ResourceId, apiKey = "sk-ant-test-0001" }, token).ConfigureAwait(false))
                .Expect(200, "store key").Json<SetupStatusResponse>();
            SetupCheck failed = after.Checks.First(c => c.Key == model.Key);
            Assert.True(failed.HasApiKey && failed.State == SetupCheckStateEnum.Failed && !String.IsNullOrWhiteSpace(failed.Message) && !failed.Message!.Contains("(Parameter", StringComparison.Ordinal),
                "a stored key that fails is reported: " + failed.State + " " + failed.Message);
            Assert.True(!after.Ready, "still not ready");

            List<VaultCredentialResponse> creds = (await ta.Client.GetAsync("/v1.0/api/vault/credentials", token).ConfigureAwait(false)).Expect(200, "credentials").Json<EnumerationResult<VaultCredentialResponse>>().Items;
            Assert.True(creds.Count == 1 && creds[0].Name == "Anthropic API key" && creds[0].SecretPreview == "****0001", "one credential, secret hidden but for its last four");
            (await ta.Client.PostAsync("/v1.0/api/setup/model-key", new { modelEndpointId = model.ResourceId, apiKey = "sk-ant-test-0002" }, token).ConfigureAwait(false)).Expect(200, "rotate key");
            creds = (await ta.Client.GetAsync("/v1.0/api/vault/credentials", token).ConfigureAwait(false)).Expect(200, "credentials").Json<EnumerationResult<VaultCredentialResponse>>().Items;
            Assert.True(creds.Count == 1 && creds[0].SecretPreview == "****0002", "a second key rotates the same credential");

            ErrorResponse wizard = (await ta.Client.PostAsync("/v1.0/api/refinement-sessions", new { seedText = "Heat pump rebates" }, token).ConfigureAwait(false))
                .Expect(409, "wizard refused after a failed check").Json<ErrorResponse>();
            Assert.True(wizard.Message.Contains("failed its last check", StringComparison.Ordinal), "failed: " + wizard.Message);
        }

        private static async Task ReadyAsync(CancellationToken token)
        {
            TestUser[] users = await WorkspaceAsync(token).ConfigureAwait(false);
            TestUser ta = users[0];
            TestUser member = users[1];
            ModelEndpoint e = (await ta.Client.PostAsync("/v1.0/api/model-endpoints", new
            {
                name = "stub setup", provider = "OpenAiCompatible", endpoint = StubModelServer.Shared.BaseUrl, model = "stub-chat", supportsChat = true,
                supportsEmbeddings = true, embeddingModel = "stub-embed-8"
            }, token).ConfigureAwait(false)).Expect(201, "endpoint").Json<ModelEndpoint>();
            foreach (ModelTaskEnum task in Enum.GetValues<ModelTaskEnum>())
                (await ta.Client.PutAsync("/v1.0/api/model-assignments", new { task = task.ToString(), modelEndpointId = e.Id }, token).ConfigureAwait(false)).Expect(200, "assign " + task);

            SetupStatusResponse s = await StatusAsync(ta, token).ConfigureAwait(false);
            SetupCheck model = s.Checks.Single(c => c.Kind == SetupCheckKindEnum.Model);
            Assert.True(model.ResourceId == e.Id && model.State == SetupCheckStateEnum.Unverified && !model.NeedsApiKey, "a keyless endpoint starts unverified: " + model.State);
            (await ta.Client.PostAsync("/v1.0/api/refinement-sessions", new { seedText = "Heat pump rebates" }, token).ConfigureAwait(false)).Expect(201, "an unverified endpoint with what it needs is allowed");

            (await ta.Client.PostAsync("/v1.0/api/model-endpoints/" + e.Id + "/test", new { }, token).ConfigureAwait(false)).Expect(200, "sample prompt");
            model = (await StatusAsync(ta, token).ConfigureAwait(false)).Checks.Single(c => c.Kind == SetupCheckKindEnum.Model);
            Assert.Equal(SetupCheckStateEnum.Ready, model.State, "a successful call proves the endpoint works");

            List<SearchProvider> providers = (await ta.Client.GetAsync("/v1.0/api/search-providers", token).ConfigureAwait(false)).Expect(200, "providers").Json<EnumerationResult<SearchProvider>>().Items;
            SearchProvider searx = providers.First(p => p.Kind == SearchProviderKindEnum.Searxng);
            (await ta.Client.PutAsync("/v1.0/api/search-providers/" + searx.Id, new { endpoint = StubModelServer.Shared.BaseUrl + "/setup" }, token).ConfigureAwait(false)).Expect(200, "point SearXNG at the stub");
            SetupStatusResponse verified = (await ta.Client.PostAsync("/v1.0/api/setup/verify", null, token).ConfigureAwait(false)).Expect(200, "verify").Json<SetupStatusResponse>();
            Assert.True(verified.Ready, "ready after verification: " + String.Join("; ", verified.Checks.Select(c => c.Key + "=" + c.State + " " + c.Message)));
            Assert.Equal(SetupCheckStateEnum.Ready, verified.Checks.Single(c => c.Kind == SetupCheckKindEnum.Embeddings).State, "embeddings verified");
            Assert.True((await StatusAsync(member, token).ConfigureAwait(false)).Ready, "members see the workspace ready");

            (await ta.Client.PutAsync("/v1.0/api/model-endpoints/" + e.Id, new { model = "stub-research" }, token).ConfigureAwait(false)).Expect(200, "change model");
            SetupStatusResponse changed = await StatusAsync(ta, token).ConfigureAwait(false);
            Assert.True(!changed.Ready && changed.Checks.Single(c => c.Kind == SetupCheckKindEnum.Model).State == SetupCheckStateEnum.Unverified, "changing the model needs a new check");
        }
    }
}
