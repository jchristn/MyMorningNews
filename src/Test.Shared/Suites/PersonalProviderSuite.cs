namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Audio;
    using MyMorningNews.Core.Entitlements;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Server;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>Private provider ownership, encrypted credentials, rotation races, BYOK funding and real HTTP probes.</summary>
    public static class PersonalProviderSuite
    {
        /// <summary>Register core isolation tests and an optional local Postman provider integration.</summary>
        public static TestSuiteDescriptor Create()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Cases.Case("PersonalProviders", "Isolation", "Private keys are write-only and account-isolated; rotation clears validation and prevents sending a new key to an old URL", IsolationAsync)
            };
            if (!String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MYMORNINGNEWS_MODEL_SIM_URL")))
                cases.Add(Cases.Case("PersonalProviders", "HttpRouting", "BYOK probes and research use the account key; deleting it fails closed and unavailable local speech never uses a platform key", HttpRoutingAsync));
            return new TestSuiteDescriptor("PersonalProviders", "Private provider funding", cases);
        }

        private static async Task IsolationAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant tenant = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser owner = await TestFixtures.CreateUserAsync(admin, tenant.Id, false, token).ConfigureAwait(false);
            TestUser peer = await TestFixtures.CreateUserAsync(admin, tenant.Id, true, token).ConfigureAwait(false);
            const string key = "sk_test_private_1234";
            TestResponse saved = (await owner.Client.PutAsync("/v1.0/api/account/providers", new { kind = "Chat", provider = "OpenAi", model = "test-model", apiKey = key }, token).ConfigureAwait(false)).Expect(200, "save key");
            Assert.True(!saved.Body.Contains(key, StringComparison.Ordinal) && !saved.Body.Contains("secretEncrypted", StringComparison.OrdinalIgnoreCase), "write response excludes plaintext and ciphertext");
            ProviderConnection connection = saved.Json<ProviderConnection>();
            ProviderConnection stored = (await server.Context.Database.ProviderConnections.ReadAsync(tenant.Id, connection.Id, token).ConfigureAwait(false))!;
            Assert.True(stored.SecretEncrypted != null && !stored.SecretEncrypted.Contains(key, StringComparison.Ordinal) && stored.SecretLast4 == "1234", "encrypted at rest");
            TestResponse peers = (await peer.Client.GetAsync("/v1.0/api/account/providers", token).ConfigureAwait(false)).Expect(200, "peer list");
            Assert.True(!peers.Body.Contains(connection.Id, StringComparison.Ordinal), "same-tenant administrator cannot list another user's key");
            (await peer.Client.PostAsync("/v1.0/api/account/providers/" + connection.Id + "/test", null, token).ConfigureAwait(false)).Expect(404, "peer probe denied");
            (await peer.Client.DeleteAsync("/v1.0/api/account/providers/" + connection.Id, token).ConfigureAwait(false)).Expect(404, "peer deletion denied");
            (await owner.Client.PutAsync("/v1.0/api/account/funding", new { mode = "Personal" }, token).ConfigureAwait(false)).Expect(409, "unverified key cannot activate BYOK");
            ModelCallRequest request = new ModelCallRequest { TenantId = tenant.Id, UserId = owner.User.Id, Task = ModelTaskEnum.QueryPlanning, ModelEndpointId = connection.Id, IsDryRun = true };
            ResolvedModel resolved = (await server.Context.PersonalProviders.ResolveModelAsync(request, token).ConfigureAwait(false))!;
            (await owner.Client.PutAsync("/v1.0/api/account/providers", new { kind = "Chat", provider = "OpenAi", model = "test-model", endpoint = "https://example.com/v1" }, token).ConfigureAwait(false)).Expect(400, "changing key destination requires reentry");
            (await owner.Client.PutAsync("/v1.0/api/account/providers", new { kind = "Chat", provider = "Gemini", model = "test-model", apiKey = "new_private_5678" }, token).ConfigureAwait(false)).Expect(200, "rotate provider and key");
            Assert.True(!await server.Context.Database.ProviderConnections.MarkValidatedAsync(stored, token).ConfigureAwait(false), "old probe cannot validate changed key");
            bool blocked = false;
            try { await server.Context.PersonalProviders.ModelSecretAsync(resolved, request, token).ConfigureAwait(false); }
            catch (ModelCallException) { blocked = true; }
            Assert.True(blocked, "rotation cannot send a new provider key to a previous provider URL");
        }

        private static async Task HttpRoutingAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant tenant = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser owner = await TestFixtures.CreateUserAsync(admin, tenant.Id, false, token).ConfigureAwait(false);
            string url = Environment.GetEnvironmentVariable("MYMORNINGNEWS_MODEL_SIM_URL")!;
            ProviderConnection connection = (await owner.Client.PutAsync("/v1.0/api/account/providers", new { kind = "Chat", provider = "OpenAiCompatible", model = "private-test", endpoint = url, apiKey = "sk_test_private_1234" }, token).ConfigureAwait(false)).Expect(200, "connect private model").Json<ProviderConnection>();
            (await owner.Client.PostAsync("/v1.0/api/account/providers/" + connection.Id + "/test", null, token).ConfigureAwait(false)).Expect(200, "real HTTP probe uses private credential");
            ProviderConnection privateSpeech = (await owner.Client.PutAsync("/v1.0/api/account/providers", new { kind = "Speech", provider = "OpenAiCompatible", model = "speech-test", voice = "alloy", endpoint = url, apiKey = "sk_test_private_1234" }, token).ConfigureAwait(false)).Expect(200, "connect private speech before switching funding").Json<ProviderConnection>();
            (await owner.Client.PostAsync("/v1.0/api/account/providers/" + privateSpeech.Id + "/test", null, token).ConfigureAwait(false)).Expect(200, "speech connection probe works before BYOK activation");
            Assert.True((await server.Context.Database.ProviderConnections.ReadAsync(tenant.Id, privateSpeech.Id, token).ConfigureAwait(false))!.ValidatedUtc.HasValue, "speech marked verified after valid WAV");
            await owner.Client.DeleteAsync("/v1.0/api/account/providers/" + privateSpeech.Id, token).ConfigureAwait(false);
            EffectivePlan plan = (await owner.Client.PutAsync("/v1.0/api/account/funding", new { mode = "Personal" }, token).ConfigureAwait(false)).Expect(200, "activate BYOK").Json<EffectivePlan>();
            Assert.True(plan.Plan.Key == "byok" && plan.FundingMode == "Personal" && plan.Features["customTopics.max"] == "unlimited", "BYOK package and funding selected together");
            ModelCallRequest request = new ModelCallRequest { TenantId = tenant.Id, UserId = owner.User.Id, Task = ModelTaskEnum.QueryPlanning, UserPrompt = "Say ready", MaxTokens = 64 };
            ModelCallResult result = await server.Context.Models.ChatAsync(request, token).ConfigureAwait(false);
            Assert.True(result.Text.Contains("ready", StringComparison.Ordinal), "normal research uses private model");
            LlmCall recorded = (await server.Context.Database.LlmCalls.EnumerateAsync(new LlmCallFilter { TenantId = tenant.Id, UserId = owner.User.Id, PageSize = 10 }, token).ConfigureAwait(false)).Objects.First(c => !c.IsDryRun);
            Assert.True(recorded.ProviderConnectionId == connection.Id && recorded.ModelEndpointId == null && recorded.Model == "private-test", "accounting retains private connection attribution without a shared endpoint foreign key");
            bool originalLocal = server.Context.Settings.Tts.AllowLocalForByok;
            try
            {
                server.Context.Settings.Tts.AllowLocalForByok = false;
                bool refused = false;
                try { await server.Context.PersonalProviders.SpeechAsync(tenant.Id, owner.User.Id, "voice", "test", token).ConfigureAwait(false); }
                catch (InvalidOperationException) { refused = true; }
                Assert.True(refused, "no platform speech fallback");
                await server.Context.PersonalProviders.SaveAsync(tenant.Id, owner.User.Id, new ProviderConnectionRequest { Kind = "Speech", Provider = "OpenAi", Model = "speech-test", Voice = "alloy", ApiKey = "speech_private_key" }, token).ConfigureAwait(false);
                TtsRequest speech = (await server.Context.PersonalProviders.SpeechAsync(tenant.Id, owner.User.Id, "other", "test", token).ConfigureAwait(false))!;
                Assert.True(speech.ApiKey == "speech_private_key" && speech.Model == "speech-test" && speech.Voice == "alloy", "speech uses separately owned credential");
            }
            finally { server.Context.Settings.Tts.AllowLocalForByok = originalLocal; }
            (await owner.Client.DeleteAsync("/v1.0/api/account/providers/" + connection.Id, token).ConfigureAwait(false)).Expect(200, "remove chat key");
            bool missing = false;
            try { await server.Context.Models.ChatAsync(request, token).ConfigureAwait(false); }
            catch (ModelCallException) { missing = true; }
            Assert.True(missing, "deleted BYOK key cannot use tenant or platform model");
            plan = await server.Context.Entitlements.GetEffectiveAsync(tenant.Id, owner.User.Id, token).ConfigureAwait(false);
            Assert.Equal("Personal", plan.FundingMode, "deletion does not silently change funding");
        }
    }
}
