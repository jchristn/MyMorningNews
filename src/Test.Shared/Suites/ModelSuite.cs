namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Models.Results;
    using MyMorningNews.Core.Prompts;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Server;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Credential vault, model endpoints and assignments, the model gateway (against a stub model server), and prompts.
    /// </summary>
    public static class ModelSuite
    {
        private const string S = "Models";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Vault, models, gateway, and prompts", new[]
            {
                Cases.Case(S, "Vault.Lifecycle", "Vault credentials are stored encrypted, previewed, rotated, and deleted", VaultLifecycleAsync),
                Cases.Case(S, "Vault.RevealAudited", "Reveal returns the secret with no-store, is audited, and is denied to members", RevealAsync),
                Cases.Case(S, "Defaults.Seeded", "New tenants get Anthropic and Ollama endpoints, role assignments, and prompts", DefaultsAsync),
                Cases.Case(S, "Endpoints.Validation", "Endpoint rules are enforced (Anthropic temperature cleared, compatible needs URL)", EndpointRulesAsync),
                Cases.Case(S, "Gateway.TextAndLog", "Text calls succeed and are logged with tokens", TextAsync),
                Cases.Case(S, "Gateway.ToolStructured", "Structured output uses a submit_result tool with tool_choice auto", ToolStructuredAsync),
                Cases.Case(S, "Gateway.Repair", "Invalid structured output triggers a repair turn", RepairAsync),
                Cases.Case(S, "Gateway.JsonFallback", "Endpoints without tool support use JSON mode", JsonFallbackAsync),
                Cases.Case(S, "Gateway.Retry429", "Rate-limited calls are retried", RetryAsync),
                Cases.Case(S, "Gateway.NonRetryable", "Client errors fail fast and are logged", NonRetryableAsync),
                Cases.Case(S, "Gateway.Refusal", "Refusals raise a typed exception", RefusalAsync),
                Cases.Case(S, "Gateway.Embeddings", "Embeddings return vectors with their dimensions", EmbeddingsAsync),
                Cases.Case(S, "Gateway.Assignments", "Topic assignments override tenant assignments", AssignmentsAsync),
                Cases.Case(S, "Endpoints.ValidateAndList", "Validation probes tools and embedding dimensions; models are listed", ValidateAsync),
                Cases.Case(S, "Prompts.Renderer", "The renderer substitutes values, expands lists, and flags unknown variables", RendererAsync, "fast"),
                Cases.Case(S, "Prompts.Defaults", "Every default prompt parses and declares its variables", PromptDefaultsAsync, "fast"),
                Cases.Case(S, "Prompts.Versioning", "Prompts save versions, roll back, and reset", VersioningAsync),
                Cases.Case(S, "Prompts.OverrideAndLab", "Topic overrides resolve first and the lab runs drafts", OverrideAndLabAsync),
                Cases.Case(S, "Prompts.DefaultUpgrade", "Uncustomized tenant prompts move to new defaults; customized prompts are kept", DefaultUpgradeAsync),
                Cases.Case(S, "Accounting.Streaming", "Streamed calls record time to first token, chunks, token types, tool calls, attribution, and split cost", StreamingAccountingAsync),
                Cases.Case(S, "Accounting.Guardrail", "Every model call carries the scope guardrail ahead of the prompt", GuardrailAsync),
                Cases.Case(S, "Usage.Reports", "Usage reports rank users by cost, time series are gap-filled, and access is restricted", UsageReportsAsync)
            });
        }

        private static async Task<TestUser> TenantAdminAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            return await TestFixtures.CreateUserAsync(admin, t.Id, true, token).ConfigureAwait(false);
        }

        private static async Task<ModelEndpoint> StubEndpointAsync(TestApiClient client, string model, bool? supportsTools = null, string? embeddingModel = null, CancellationToken token = default)
        {
            ModelEndpoint e = (await client.PostAsync("/v1.0/api/model-endpoints", new
            {
                name = "stub " + model + " " + Guid.NewGuid().ToString("N").Substring(0, 6),
                provider = "OpenAiCompatible",
                endpoint = StubModelServer.Shared.BaseUrl,
                model = model,
                supportsChat = true,
                supportsEmbeddings = embeddingModel != null,
                embeddingModel = embeddingModel,
                inputCostPerMTok = 1.0,
                outputCostPerMTok = 2.0
            }, token).ConfigureAwait(false)).Expect(201, "create stub endpoint").Json<ModelEndpoint>();
            if (supportsTools.HasValue)
            {
                MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
                e.SupportsTools = supportsTools;
                await server.Context.Database.ModelEndpoints.UpdateAsync(e, token).ConfigureAwait(false);
            }

            return e;
        }

        private static async Task<ModelGateway> GatewayAsync(CancellationToken token)
        {
            return (await SharedTestServer.GetServerAsync(token).ConfigureAwait(false)).Context.Models;
        }

        private static async Task VaultLifecycleAsync(CancellationToken token)
        {
            TestUser ta = await TenantAdminAsync(token).ConfigureAwait(false);
            VaultCredentialResponse created = (await ta.Client.PostAsync("/v1.0/api/vault/credentials", new { name = "Tavily", provider = "Tavily", secret = "tvly-secret-abcd" }, token).ConfigureAwait(false))
                .Expect(201, "create").Json<VaultCredentialResponse>();
            Assert.Equal("****abcd", created.SecretPreview, "preview");
            TestResponse list = (await ta.Client.GetAsync("/v1.0/api/vault/credentials", token).ConfigureAwait(false)).Expect(200, "list");
            Assert.True(!list.Body.Contains("tvly-secret", StringComparison.Ordinal), "list never returns the secret");
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            VaultCredential stored = (await server.Context.Database.VaultCredentials.ReadByIdAsync(created.Id, token).ConfigureAwait(false))!;
            Assert.True(!stored.SecretEncrypted.Contains("tvly", StringComparison.Ordinal), "stored encrypted");
            VaultCredentialResponse rotated = (await ta.Client.PutAsync("/v1.0/api/vault/credentials/" + created.Id, new { secret = "tvly-rotated-wxyz" }, token).ConfigureAwait(false))
                .Expect(200, "rotate").Json<VaultCredentialResponse>();
            Assert.Equal("****wxyz", rotated.SecretPreview, "rotated preview");
            (await ta.Client.PostAsync("/v1.0/api/vault/credentials", new { name = "no secret" }, token).ConfigureAwait(false)).Expect(400, "secret required");
            (await ta.Client.DeleteAsync("/v1.0/api/vault/credentials/" + created.Id, token).ConfigureAwait(false)).Expect(204, "delete");
            (await ta.Client.GetAsync("/v1.0/api/vault/credentials/" + created.Id, token).ConfigureAwait(false)).Expect(404, "deleted");
        }

        private static async Task RevealAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser ta = await TestFixtures.CreateUserAsync(admin, t.Id, true, token).ConfigureAwait(false);
            TestUser member = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            VaultCredentialResponse created = (await ta.Client.PostAsync("/v1.0/api/vault/credentials", new { name = "Key", secret = "sk-plain-1234" }, token).ConfigureAwait(false))
                .Expect(201, "create").Json<VaultCredentialResponse>();
            using (HttpClient http = new HttpClient())
            using (HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, ta.Client.BaseUrl + "/v1.0/api/vault/credentials/" + created.Id + "/reveal"))
            {
                req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + ta.Client.Token);
                using (HttpResponseMessage resp = await http.SendAsync(req, token).ConfigureAwait(false))
                {
                    Assert.Equal(200, (int)resp.StatusCode, "reveal status");
                    Assert.True(resp.Headers.CacheControl?.NoStore == true, "no-store");
                    VaultRevealResponse revealed = MyMorningNews.Core.Serialization.Serializer.Deserialize<VaultRevealResponse>(await resp.Content.ReadAsStringAsync(token).ConfigureAwait(false))!;
                    Assert.Equal("sk-plain-1234", revealed.Secret, "secret");
                }
            }

            EnumerationResult<AuditRecord> audit = (await ta.Client.GetAsync("/v1.0/api/audit?eventType=CredentialRevealed", token).ConfigureAwait(false)).Expect(200, "audit").Json<EnumerationResult<AuditRecord>>();
            Assert.True(audit.Items.Any(a => a.Description != null && a.Description.Contains(created.Id, StringComparison.Ordinal) || a.Reason != null && a.Reason.Contains(created.Id, StringComparison.Ordinal)), "reveal audited");
            (await member.Client.PostAsync("/v1.0/api/vault/credentials/" + created.Id + "/reveal", null, token).ConfigureAwait(false)).Expect(403, "member reveal");
            (await member.Client.GetAsync("/v1.0/api/vault/credentials", token).ConfigureAwait(false)).Expect(200, "member reads metadata");
        }

        private static async Task DefaultsAsync(CancellationToken token)
        {
            TestUser ta = await TenantAdminAsync(token).ConfigureAwait(false);
            EnumerationResult<ModelEndpoint> endpoints = (await ta.Client.GetAsync("/v1.0/api/model-endpoints", token).ConfigureAwait(false)).Expect(200, "endpoints").Json<EnumerationResult<ModelEndpoint>>();
            ModelEndpoint anthropic = endpoints.Items.First(e => e.Provider == ModelProviderEnum.Anthropic);
            Assert.Equal("claude-opus-5-5", anthropic.Model, "default chat model");
            Assert.True(anthropic.InputCostPerMTok == 4m && anthropic.OutputCostPerMTok == 20m && anthropic.CachedInputCostPerMTok == 0.20m && anthropic.CacheWriteCostPerMTok == 5m, "default pricing seeded");
            Assert.True(anthropic.Temperature == null, "no temperature for Anthropic");
            ModelEndpoint ollama = endpoints.Items.First(e => e.Provider == ModelProviderEnum.Ollama);
            Assert.True(ollama.SupportsEmbeddings && !ollama.SupportsChat && ollama.EmbeddingModel == "all-minilm" && ollama.EmbeddingDimensions == 384, "embedding endpoint");
            EnumerationResult<ModelAssignment> assignments = (await ta.Client.GetAsync("/v1.0/api/model-assignments", token).ConfigureAwait(false)).Expect(200, "assignments").Json<EnumerationResult<ModelAssignment>>();
            Assert.Equal(Enum.GetValues<ModelTaskEnum>().Length, assignments.Items.Count, "one assignment per role");
            Assert.True(assignments.Items.Single(a => a.Task == ModelTaskEnum.Embedding).ModelEndpointId == ollama.Id, "embedding on Ollama");
            Assert.True(assignments.Items.Single(a => a.Task == ModelTaskEnum.Synthesis).ReasoningEffort == ReasoningEffortEnum.High, "synthesis effort high");
            EnumerationResult<PromptTemplate> prompts = (await ta.Client.GetAsync("/v1.0/api/prompts?pageSize=100", token).ConfigureAwait(false)).Expect(200, "prompts").Json<EnumerationResult<PromptTemplate>>();
            Assert.Equal(DefaultPromptCatalog.All().Count, (int)prompts.TotalCount, "prompts seeded");
        }

        private static async Task EndpointRulesAsync(CancellationToken token)
        {
            TestUser ta = await TenantAdminAsync(token).ConfigureAwait(false);
            ModelEndpoint a = (await ta.Client.PostAsync("/v1.0/api/model-endpoints", new { name = "A2", provider = "Anthropic", model = "claude-sonnet-5-5", temperature = 0.7, apiKey = "sk-ant-test-0001" }, token).ConfigureAwait(false))
                .Expect(201, "anthropic").Json<ModelEndpoint>();
            Assert.True(a.Temperature == null, "temperature cleared for Anthropic");
            Assert.True(!String.IsNullOrEmpty(a.CredentialId), "api key stored in the vault");
            (await ta.Client.PostAsync("/v1.0/api/model-endpoints", new { name = "C", provider = "OpenAiCompatible", model = "m" }, token).ConfigureAwait(false)).Expect(400, "compatible needs url");
            (await ta.Client.PostAsync("/v1.0/api/model-endpoints", new { name = "D", provider = "Ollama", supportsChat = true }, token).ConfigureAwait(false)).Expect(400, "chat needs model");
            TestUser member = await TestFixtures.CreateUserAsync(await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false), a.TenantId, false, token).ConfigureAwait(false);
            (await member.Client.PostAsync("/v1.0/api/model-endpoints", new { name = "M", provider = "Ollama", model = "x" }, token).ConfigureAwait(false)).Expect(403, "member cannot add endpoints");
            (await member.Client.GetAsync("/v1.0/api/model-endpoints", token).ConfigureAwait(false)).Expect(200, "member reads endpoints");
        }

        private static async Task TextAsync(CancellationToken token)
        {
            TestUser ta = await TenantAdminAsync(token).ConfigureAwait(false);
            ModelEndpoint e = await StubEndpointAsync(ta.Client, "stub-chat", null, null, token).ConfigureAwait(false);
            ModelTestResponse r = (await ta.Client.PostAsync("/v1.0/api/model-endpoints/" + e.Id + "/test", new { prompt = "hello" }, token).ConfigureAwait(false)).Expect(200, "test").Json<ModelTestResponse>();
            Assert.True(r.Success && r.Text == "ready" && r.InputTokens == 12 && r.OutputTokens == 3, "test result: " + r.Error);
            EnumerationResult<LlmCall> calls = (await ta.Client.GetAsync("/v1.0/api/llm-calls?modelEndpointId=" + e.Id, token).ConfigureAwait(false)).Expect(200, "calls").Json<EnumerationResult<LlmCall>>();
            LlmCall call = calls.Items.First();
            Assert.True(call.Success && call.IsDryRun && call.InputTokens == 12 && call.PromptText == null, "logged without payload in list");
            Assert.True(call.EstimatedCostUsd.HasValue && call.EstimatedCostUsd.Value > 0, "cost estimated");
            LlmCall full = (await ta.Client.GetAsync("/v1.0/api/llm-calls/" + call.Id, token).ConfigureAwait(false)).Expect(200, "call").Json<LlmCall>();
            Assert.True(full.PromptText != null && full.PromptText.Contains("hello", StringComparison.Ordinal) && full.ResponseText == "ready", "payload captured");
        }

        private static async Task ToolStructuredAsync(CancellationToken token)
        {
            TestUser ta = await TenantAdminAsync(token).ConfigureAwait(false);
            ModelEndpoint e = await StubEndpointAsync(ta.Client, "stub-chat", null, null, token).ConfigureAwait(false);
            int before = StubModelServer.Shared.Requests.Count;
            StructuredCallResult<TextAnswerResult> r = await (await GatewayAsync(token).ConfigureAwait(false)).ChatStructuredAsync<TextAnswerResult>(new ModelCallRequest
            {
                TenantId = e.TenantId, Task = ModelTaskEnum.Scoring, ModelEndpointId = e.Id, SystemPrompt = "Answer.", UserPrompt = "Say ready."
            }, token).ConfigureAwait(false);
            Assert.Equal("ready", r.Value.Answer, "structured answer");
            Assert.Equal(0, r.Repairs, "no repairs");
            string sent = StubModelServer.Shared.Requests.Skip(before).Last(b => b.Contains("submit_result", StringComparison.Ordinal));
            Assert.True(sent.Contains("\"tool_choice\":\"auto\"", StringComparison.Ordinal), "tool choice auto, never forced: " + sent.Substring(0, Math.Min(300, sent.Length)));
        }

        private static async Task RepairAsync(CancellationToken token)
        {
            TestUser ta = await TenantAdminAsync(token).ConfigureAwait(false);
            ModelEndpoint e = await StubEndpointAsync(ta.Client, "stub-repair", null, null, token).ConfigureAwait(false);
            StructuredCallResult<TextAnswerResult> r = await (await GatewayAsync(token).ConfigureAwait(false)).ChatStructuredAsync<TextAnswerResult>(new ModelCallRequest
            {
                TenantId = e.TenantId, Task = ModelTaskEnum.Extraction, ModelEndpointId = e.Id, UserPrompt = "x"
            }, token).ConfigureAwait(false);
            Assert.True(r.Value.Answer == "ready" && r.Repairs == 1, "repaired once (repairs " + r.Repairs + ")");
        }

        private static async Task JsonFallbackAsync(CancellationToken token)
        {
            TestUser ta = await TenantAdminAsync(token).ConfigureAwait(false);
            ModelEndpoint e = await StubEndpointAsync(ta.Client, "stub-chat", false, null, token).ConfigureAwait(false);
            StructuredCallResult<TextAnswerResult> r = await (await GatewayAsync(token).ConfigureAwait(false)).ChatStructuredAsync<TextAnswerResult>(new ModelCallRequest
            {
                TenantId = e.TenantId, Task = ModelTaskEnum.Scoring, ModelEndpointId = e.Id, UserPrompt = "x"
            }, token).ConfigureAwait(false);
            Assert.Equal("ready", r.Value.Answer, "parsed from fenced JSON");
        }

        private static async Task RetryAsync(CancellationToken token)
        {
            TestUser ta = await TenantAdminAsync(token).ConfigureAwait(false);
            ModelEndpoint e = await StubEndpointAsync(ta.Client, "stub-429", null, null, token).ConfigureAwait(false);
            int before = StubModelServer.Shared.Count("stub-429");
            ModelCallResult r = await (await GatewayAsync(token).ConfigureAwait(false)).ChatAsync(new ModelCallRequest
            {
                TenantId = e.TenantId, Task = ModelTaskEnum.QueryPlanning, ModelEndpointId = e.Id, UserPrompt = "x"
            }, token).ConfigureAwait(false);
            Assert.Equal("ready", r.Text, "succeeded after retry");
            Assert.True(StubModelServer.Shared.Count("stub-429") >= Math.Max(2, before + 1), "retried");
        }

        private static async Task NonRetryableAsync(CancellationToken token)
        {
            TestUser ta = await TenantAdminAsync(token).ConfigureAwait(false);
            ModelEndpoint e = await StubEndpointAsync(ta.Client, "stub-error", null, null, token).ConfigureAwait(false);
            int before = StubModelServer.Shared.Count("stub-error");
            bool threw = false;
            try
            {
                await (await GatewayAsync(token).ConfigureAwait(false)).ChatAsync(new ModelCallRequest { TenantId = e.TenantId, Task = ModelTaskEnum.QueryPlanning, ModelEndpointId = e.Id, UserPrompt = "x" }, token).ConfigureAwait(false);
            }
            catch (ModelCallException ex) when (ex.StatusCode == 400 && !ex.Transient)
            {
                threw = true;
            }

            Assert.True(threw, "typed non-transient failure");
            Assert.Equal(before + 1, StubModelServer.Shared.Count("stub-error"), "not retried");
            EnumerationResult<LlmCall> calls = (await ta.Client.GetAsync("/v1.0/api/llm-calls?success=false", token).ConfigureAwait(false)).Expect(200, "calls").Json<EnumerationResult<LlmCall>>();
            Assert.True(calls.Items.Any(c => c.ModelEndpointId == e.Id && c.ErrorMessage != null), "failure logged");
        }

        private static async Task RefusalAsync(CancellationToken token)
        {
            TestUser ta = await TenantAdminAsync(token).ConfigureAwait(false);
            ModelEndpoint e = await StubEndpointAsync(ta.Client, "stub-refusal", null, null, token).ConfigureAwait(false);
            bool refused = false;
            try
            {
                await (await GatewayAsync(token).ConfigureAwait(false)).ChatAsync(new ModelCallRequest { TenantId = e.TenantId, Task = ModelTaskEnum.Synthesis, ModelEndpointId = e.Id, UserPrompt = "x" }, token).ConfigureAwait(false);
            }
            catch (ModelRefusalException)
            {
                refused = true;
            }

            Assert.True(refused, "refusal surfaced");
        }

        private static async Task EmbeddingsAsync(CancellationToken token)
        {
            TestUser ta = await TenantAdminAsync(token).ConfigureAwait(false);
            ModelEndpoint e = await StubEndpointAsync(ta.Client, "stub-chat", null, "stub-embed-8", token).ConfigureAwait(false);
            (await ta.Client.PutAsync("/v1.0/api/model-assignments", new { task = "Embedding", modelEndpointId = e.Id }, token).ConfigureAwait(false)).Expect(200, "assign embedding");
            EmbeddingBatch batch = await (await GatewayAsync(token).ConfigureAwait(false)).EmbedAsync(e.TenantId, null, new List<string> { "a", "b", "c" }, token).ConfigureAwait(false);
            Assert.True(batch.Vectors.Count == 3 && batch.Dimensions == 8 && batch.Model == "stub-embed-8", "vectors and dimensions");
        }

        private static async Task AssignmentsAsync(CancellationToken token)
        {
            TestUser ta = await TenantAdminAsync(token).ConfigureAwait(false);
            ModelEndpoint tenantDefault = await StubEndpointAsync(ta.Client, "stub-chat", null, null, token).ConfigureAwait(false);
            ModelEndpoint topicEndpoint = await StubEndpointAsync(ta.Client, "stub-chat", null, null, token).ConfigureAwait(false);
            (await ta.Client.PutAsync("/v1.0/api/model-assignments", new { task = "Scoring", modelEndpointId = tenantDefault.Id }, token).ConfigureAwait(false)).Expect(200, "tenant assignment");
            ModelAssignment topic = (await ta.Client.PutAsync("/v1.0/api/model-assignments", new { task = "Scoring", topicId = "top_test", modelEndpointId = topicEndpoint.Id, modelOverride = "stub-chat" }, token).ConfigureAwait(false))
                .Expect(201, "topic assignment").Json<ModelAssignment>();
            ModelGateway gateway = await GatewayAsync(token).ConfigureAwait(false);
            ResolvedModel tenantResolved = await gateway.ResolveAsync(tenantDefault.TenantId, ModelTaskEnum.Scoring, null, null, null, token).ConfigureAwait(false);
            ResolvedModel topicResolved = await gateway.ResolveAsync(tenantDefault.TenantId, ModelTaskEnum.Scoring, "top_test", null, null, token).ConfigureAwait(false);
            Assert.Equal(tenantDefault.Id, tenantResolved.Endpoint.Id, "tenant default");
            Assert.Equal(topicEndpoint.Id, topicResolved.Endpoint.Id, "topic override");
            (await ta.Client.DeleteAsync("/v1.0/api/model-assignments/" + topic.Id, token).ConfigureAwait(false)).Expect(204, "delete topic override");
            (await ta.Client.PutAsync("/v1.0/api/model-assignments", new { task = "Embedding", modelEndpointId = tenantDefault.Id }, token).ConfigureAwait(false)).Expect(400, "chat-only endpoint for embeddings");
        }

        private static async Task ValidateAsync(CancellationToken token)
        {
            TestUser ta = await TenantAdminAsync(token).ConfigureAwait(false);
            ModelEndpoint e = await StubEndpointAsync(ta.Client, "stub-chat", null, "stub-embed-12", token).ConfigureAwait(false);
            ModelValidationResponse v = (await ta.Client.PostAsync("/v1.0/api/model-endpoints/" + e.Id + "/validate", null, token).ConfigureAwait(false)).Expect(200, "validate").Json<ModelValidationResponse>();
            Assert.True(v.Reachable && v.SupportsTools == true && v.EmbeddingDimensions == 12, "validation: " + String.Join(" | ", v.Messages));
            ModelEndpoint after = (await ta.Client.GetAsync("/v1.0/api/model-endpoints/" + e.Id, token).ConfigureAwait(false)).Json<ModelEndpoint>();
            Assert.True(after.SupportsTools == true && after.EmbeddingDimensions == 12 && after.LastValidatedUtc.HasValue, "endpoint updated");
            List<ModelListItem> models = (await ta.Client.GetAsync("/v1.0/api/model-endpoints/" + e.Id + "/models", token).ConfigureAwait(false)).Expect(200, "models").Json<List<ModelListItem>>();
            Assert.True(models.Any(m => m.Name == "stub-chat"), "listed");
        }

        private static Task RendererAsync(CancellationToken token)
        {
            Dictionary<string, object?> values = new Dictionary<string, object?>
            {
                { "name", "Ada" },
                { "items", new List<string> { "one", "two" } },
                { "rows", new List<Dictionary<string, object?>> { new Dictionary<string, object?> { { "t", "x" } }, new Dictionary<string, object?> { { "t", "y" } } } }
            };
            Assert.Equal("Hi Ada.", PromptRenderer.Render("Hi {{name}}.", values), "substitution");
            Assert.Equal("Missing: (none)", PromptRenderer.Render("Missing: {{nope}}", values), "missing value");
            Assert.Equal("- one\n- two", PromptRenderer.Render("{{items}}", values), "list formatting");
            Assert.Equal("[x][y]", PromptRenderer.Render("{{#each rows}}[{{this.t}}]{{/each}}", values), "each with property");
            PromptDefinition def = PromptParser.Parse("---\nkey: k\nrole: Scoring\nvariables: [a]\n---\n## System\nS {{a}}\n## User\nU {{b}}");
            Assert.True(def.Role == ModelTaskEnum.Scoring && def.System == "S {{a}}" && def.User == "U {{b}}", "parsed sections");
            Assert.True(PromptRenderer.Unknown(def).SequenceEqual(new[] { "b" }), "unknown variable detected");
            return Task.CompletedTask;
        }

        private static Task PromptDefaultsAsync(CancellationToken token)
        {
            IReadOnlyDictionary<string, PromptDefinition> all = DefaultPromptCatalog.All();
            Assert.True(all.Count >= 19, "defaults loaded: " + all.Count);
            foreach (PromptDefinition def in all.Values)
            {
                Assert.True(!String.IsNullOrWhiteSpace(def.System) && !String.IsNullOrWhiteSpace(def.User), def.Key + " has both sections");
                List<string> unknown = PromptRenderer.Unknown(def);
                Assert.True(unknown.Count == 0, def.Key + " declares every variable (missing: " + String.Join(", ", unknown) + ")");
                Assert.True(!def.Content.Contains((char)0x2014), def.Key + " has no em-dash");
            }

            return Task.CompletedTask;
        }

        private static async Task VersioningAsync(CancellationToken token)
        {
            TestUser ta = await TenantAdminAsync(token).ConfigureAwait(false);
            EnumerationResult<PromptTemplate> list = (await ta.Client.GetAsync("/v1.0/api/prompts?key=research.score", token).ConfigureAwait(false)).Expect(200, "find").Json<EnumerationResult<PromptTemplate>>();
            PromptTemplate t = list.Items.Single();
            PromptTemplateResponse original = (await ta.Client.GetAsync("/v1.0/api/prompts/" + t.Id, token).ConfigureAwait(false)).Expect(200, "read").Json<PromptTemplateResponse>();
            Assert.True(original.Content == original.DefaultContent && !t.IsCustomized, "starts at default");
            string bad = original.Content.Replace("{{candidate.title}}", "{{candidate.nonsense}}", StringComparison.Ordinal);
            (await ta.Client.PutAsync("/v1.0/api/prompts/" + t.Id, new { content = bad }, token).ConfigureAwait(false)).Expect(400, "undeclared variable rejected");
            string good = original.Content.Replace("You rank research findings", "You carefully rank research findings", StringComparison.Ordinal);
            PromptTemplateResponse saved = (await ta.Client.PutAsync("/v1.0/api/prompts/" + t.Id, new { content = good, changeNote = "careful" }, token).ConfigureAwait(false)).Expect(200, "save").Json<PromptTemplateResponse>();
            Assert.True(saved.Template.CurrentVersion == 2 && saved.Template.IsCustomized, "version 2 customized");
            List<PromptTemplateVersion> versions = (await ta.Client.GetAsync("/v1.0/api/prompts/" + t.Id + "/versions", token).ConfigureAwait(false)).Expect(200, "versions").Json<List<PromptTemplateVersion>>();
            Assert.Equal(2, versions.Count, "two versions");
            PromptTemplateResponse rolled = (await ta.Client.PostAsync("/v1.0/api/prompts/" + t.Id + "/rollback?version=1", null, token).ConfigureAwait(false)).Expect(200, "rollback").Json<PromptTemplateResponse>();
            Assert.True(rolled.Template.CurrentVersion == 3 && !rolled.Template.IsCustomized && rolled.Content == original.Content, "rolled back as version 3");
            await ta.Client.PutAsync("/v1.0/api/prompts/" + t.Id, new { content = good }, token).ConfigureAwait(false);
            PromptTemplateResponse reset = (await ta.Client.PostAsync("/v1.0/api/prompts/" + t.Id + "/reset", null, token).ConfigureAwait(false)).Expect(200, "reset").Json<PromptTemplateResponse>();
            Assert.True(reset.Content == original.DefaultContent && reset.Template.CurrentVersion == 5, "reset to default as version 5");
            (await ta.Client.DeleteAsync("/v1.0/api/prompts/" + t.Id, token).ConfigureAwait(false)).Expect(409, "tenant prompt cannot be deleted");
        }

        private static async Task OverrideAndLabAsync(CancellationToken token)
        {
            TestUser ta = await TenantAdminAsync(token).ConfigureAwait(false);
            ModelEndpoint e = await StubEndpointAsync(ta.Client, "stub-chat", null, null, token).ConfigureAwait(false);
            PromptTemplateResponse over = (await ta.Client.PostAsync("/v1.0/api/prompts/overrides", new { key = "email.run-summary", topicId = "top_override" }, token).ConfigureAwait(false))
                .Expect(201, "override").Json<PromptTemplateResponse>();
            Assert.Equal("top_override", over.Template.TopicId, "override topic");
            (await ta.Client.PostAsync("/v1.0/api/prompts/overrides", new { key = "email.run-summary", topicId = "top_override" }, token).ConfigureAwait(false)).Expect(409, "duplicate override");
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            ResolvedPrompt resolved = await server.Context.Prompts.ResolveAsync(e.TenantId, "email.run-summary", "top_override", token).ConfigureAwait(false);
            Assert.Equal(over.Template.Id, resolved.Template.Id, "override resolves first");
            ResolvedPrompt fallback = await server.Context.Prompts.ResolveAsync(e.TenantId, "email.run-summary", "top_other", token).ConfigureAwait(false);
            Assert.True(fallback.Template.TopicId == null, "other topics use the tenant prompt");
            PromptLabResponse lab = (await ta.Client.PostAsync("/v1.0/api/prompts/lab", new
            {
                key = "email.run-summary",
                modelEndpointId = e.Id,
                variables = new Dictionary<string, string> { { "topic.name", "AI developer tools" }, { "run.summary", "Two releases." } }
            }, token).ConfigureAwait(false)).Expect(200, "lab").Json<PromptLabResponse>();
            Assert.True(lab.Success && lab.Text == "ready" && lab.RenderedUser.Contains("AI developer tools", StringComparison.Ordinal) && lab.RenderedUser.Contains("(none)", StringComparison.Ordinal), "lab rendered and ran: " + lab.Error);
            LlmCall call = (await ta.Client.GetAsync("/v1.0/api/llm-calls/" + lab.LlmCallId, token).ConfigureAwait(false)).Expect(200, "lab call").Json<LlmCall>();
            Assert.True(lab.EstimatedCostUsd.HasValue && lab.EstimatedCostUsd.Value > 0 && lab.CachedInputTokens == 4 && lab.ReasoningTokens == 1, "lab returns cost and token types");
            EnumerationResult<LlmCall> byTopic = (await ta.Client.GetAsync("/v1.0/api/llm-calls?userId=" + ta.User.Id + "&topicId=nope", token).ConfigureAwait(false)).Json<EnumerationResult<LlmCall>>();
            Assert.Equal(0, byTopic.Items.Count, "call log filters by user and topic");
            Assert.True(call.IsDryRun && call.PromptKey == "email.run-summary", "lab call logged as dry run");
            (await ta.Client.DeleteAsync("/v1.0/api/prompts/" + over.Template.Id, token).ConfigureAwait(false)).Expect(204, "delete override");
        }

        private static async Task StreamingAccountingAsync(CancellationToken token)
        {
            TestUser ta = await TenantAdminAsync(token).ConfigureAwait(false);
            ModelEndpoint e = await StubEndpointAsync(ta.Client, "stub-chat", null, null, token).ConfigureAwait(false);
            e = (await ta.Client.PutAsync("/v1.0/api/model-endpoints/" + e.Id, new { cachedInputCostPerMTok = 0.1 }, token).ConfigureAwait(false)).Expect(200, "set cache rate").Json<ModelEndpoint>();
            Assert.True(e.CachedInputCostPerMTok == 0.1m, "cache rate via API");
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            StructuredCallResult<TextAnswerResult> r = await server.Context.Models.ChatStructuredAsync<TextAnswerResult>(new ModelCallRequest
            {
                TenantId = e.TenantId, UserId = ta.User.Id, TopicId = "top_acct", Task = ModelTaskEnum.Scoring, ModelEndpointId = e.Id, UserPrompt = "x"
            }, token).ConfigureAwait(false);
            Assert.Equal("ready", r.Value.Answer, "streamed tool arguments reassembled");
            LlmCall call = (await ta.Client.GetAsync("/v1.0/api/llm-calls/" + r.LlmCallId, token).ConfigureAwait(false)).Expect(200, "call").Json<LlmCall>();
            Assert.True(call.Streamed && call.ChunkCount >= 3 && call.TimeToFirstTokenMs.HasValue && call.TimeToFirstTokenMs.Value >= 0, "streaming timings (chunks " + call.ChunkCount + ", ttft " + call.TimeToFirstTokenMs + ")");
            Assert.True(call.StreamingDurationMs.HasValue && call.Attempts == 1, "streaming window and attempts");
            Assert.True(call.InputTokens == 12 && call.OutputTokens == 3 && call.CachedInputTokens == 4 && call.ReasoningTokens == 1 && call.TotalTokens == 15, "token types");
            Assert.True(call.ToolCallCount == 1 && call.ToolNames.Contains("submit_result"), "tool calls recorded");
            Assert.True(call.UserId == ta.User.Id && call.TopicId == "top_acct", "attributed to user and topic");
            decimal expectedInput = Math.Round(8 / 1000000m * 1.0m + 4 / 1000000m * 0.1m, 6);
            decimal expectedOutput = Math.Round(3 / 1000000m * 2.0m, 6);
            Assert.True(call.InputCostUsd == expectedInput && call.OutputCostUsd == expectedOutput && call.EstimatedCostUsd == expectedInput + expectedOutput,
                "split cost with cached rate (" + call.InputCostUsd + " + " + call.OutputCostUsd + ")");
        }

        private static async Task GuardrailAsync(CancellationToken token)
        {
            TestUser ta = await TenantAdminAsync(token).ConfigureAwait(false);
            ModelEndpoint e = await StubEndpointAsync(ta.Client, "stub-chat", null, null, token).ConfigureAwait(false);
            int before = StubModelServer.Shared.Requests.Count;
            await (await GatewayAsync(token).ConfigureAwait(false)).ChatAsync(new ModelCallRequest
            {
                TenantId = e.TenantId, Task = ModelTaskEnum.QueryPlanning, ModelEndpointId = e.Id, SystemPrompt = "Plan queries.", UserPrompt = "Write me a Python web scraper."
            }, token).ConfigureAwait(false);
            await (await GatewayAsync(token).ConfigureAwait(false)).ChatAsync(new ModelCallRequest
            {
                TenantId = e.TenantId, Task = ModelTaskEnum.QueryPlanning, ModelEndpointId = e.Id, UserPrompt = "no system prompt"
            }, token).ConfigureAwait(false);
            // Other suites share the stub server; keep only this test's two requests.
            List<string> sent = StubModelServer.Shared.Requests.Skip(before).Where(b => b.Contains("Write me a Python web scraper", StringComparison.Ordinal) || b.Contains("no system prompt", StringComparison.Ordinal)).ToList();
            Assert.True(sent.Count >= 2, "requests captured");
            foreach (string body in sent)
            {
                using (System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(body))
                {
                    System.Text.Json.JsonElement first = doc.RootElement.GetProperty("messages")[0];
                    Assert.Equal("system", first.GetProperty("role").GetString(), "system message first");
                    string content = first.GetProperty("content").GetString() ?? String.Empty;
                    Assert.True(content.StartsWith("You are a component of My Morning News", StringComparison.Ordinal) && content.Contains("coding assistant", StringComparison.Ordinal), "guardrail leads the system prompt");
                }
            }
        }

        private static async Task UsageReportsAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser ta = await TestFixtures.CreateUserAsync(admin, t.Id, true, token).ConfigureAwait(false);
            TestUser heavy = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            TestUser light = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            ModelEndpoint e = await StubEndpointAsync(ta.Client, "stub-chat", null, null, token).ConfigureAwait(false);
            ModelGateway gateway = await GatewayAsync(token).ConfigureAwait(false);
            for (int i = 0; i < 3; i++)
                await gateway.ChatAsync(new ModelCallRequest { TenantId = t.Id, UserId = heavy.User.Id, Task = ModelTaskEnum.Synthesis, ModelEndpointId = e.Id, UserPrompt = "x" }, token).ConfigureAwait(false);
            await gateway.ChatAsync(new ModelCallRequest { TenantId = t.Id, UserId = light.User.Id, Task = ModelTaskEnum.Scoring, ModelEndpointId = e.Id, UserPrompt = "x" }, token).ConfigureAwait(false);
            await gateway.ChatAsync(new ModelCallRequest { TenantId = t.Id, Task = ModelTaskEnum.Scoring, ModelEndpointId = e.Id, UserPrompt = "x" }, token).ConfigureAwait(false);

            UsageReport byUser = (await ta.Client.GetAsync("/v1.0/api/usage/report?groupBy=User", token).ConfigureAwait(false)).Expect(200, "by user").Json<UsageReport>();
            Assert.Equal(5L, byUser.Totals.Calls, "total calls");
            Assert.Equal(heavy.User.Id, byUser.Rows[0].Key, "most prolific user first");
            Assert.True(byUser.Rows[0].Calls == 3 && byUser.Rows[0].Label == heavy.User.Email && byUser.Rows[0].CostShare > 0.5, "heavy user row");
            Assert.True(byUser.Rows.Any(r => r.Key == "(system)" && r.Calls == 1), "unattributed calls grouped as system");
            Assert.True(byUser.Totals.CostUsd > 0 && byUser.Totals.CachedInputTokens == 20 && byUser.Totals.TotalTokens == 75, "totals");
            UsageReport byTask = (await ta.Client.GetAsync("/v1.0/api/usage/report?groupBy=Task", token).ConfigureAwait(false)).Expect(200, "by task").Json<UsageReport>();
            Assert.True(byTask.Rows.Single(r => r.Key == "Synthesis").Calls == 3, "by task");
            UsageTimeseries series = (await ta.Client.GetAsync("/v1.0/api/usage/timeseries?bucketMinutes=60&from=" + Uri.EscapeDataString(DateTime.UtcNow.AddHours(-6).ToString("o")) + "&to=" + Uri.EscapeDataString(DateTime.UtcNow.AddMinutes(1).ToString("o")), token).ConfigureAwait(false))
                .Expect(200, "timeseries").Json<UsageTimeseries>();
            Assert.True(series.Buckets.Count >= 6 && series.Buckets.Sum(b => b.Calls) == 5 && series.Buckets.Count(b => b.Calls == 0) >= 5, "gap-filled buckets");
            (await ta.Client.GetAsync("/v1.0/api/usage/timeseries?bucketMinutes=1&from=2020-01-01T00:00:00Z", token).ConfigureAwait(false)).Expect(400, "too many buckets");

            (await heavy.Client.GetAsync("/v1.0/api/usage/report?groupBy=User", token).ConfigureAwait(false)).Expect(403, "members cannot see tenant-wide usage");
            (await heavy.Client.GetAsync("/v1.0/api/llm-calls", token).ConfigureAwait(false)).Expect(403, "members cannot read the call log");
            UsageReport mine = (await heavy.Client.GetAsync("/v1.0/api/usage/me", token).ConfigureAwait(false)).Expect(200, "own usage").Json<UsageReport>();
            Assert.True(mine.Totals.Calls == 3 && mine.Rows.All(r => r.Key == "Synthesis"), "own usage only");
            (await ta.Client.GetAsync("/v1.0/api/usage/report?allTenants=true", token).ConfigureAwait(false)).Expect(403, "platform-wide needs global admin");
            UsageReport platform = (await admin.GetAsync("/v1.0/api/usage/report?allTenants=true&groupBy=Tenant", token).ConfigureAwait(false)).Expect(200, "platform").Json<UsageReport>();
            Assert.True(platform.Rows.Any(r => r.Key == t.Id && r.Calls == 5 && r.Label == t.Name), "per-tenant row");
        }

        private static async Task DefaultUpgradeAsync(CancellationToken token)
        {
            TestUser ta = await TenantAdminAsync(token).ConfigureAwait(false);
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            PromptTemplate stale = (await ta.Client.GetAsync("/v1.0/api/prompts?key=research.extract", token).ConfigureAwait(false)).Json<EnumerationResult<PromptTemplate>>().Items.Single();
            PromptTemplate custom = (await ta.Client.GetAsync("/v1.0/api/prompts?key=research.score", token).ConfigureAwait(false)).Json<EnumerationResult<PromptTemplate>>().Items.Single();
            string defaultExtract = DefaultPromptCatalog.Get("research.extract")!.Content;
            string oldText = defaultExtract.Replace(" coding assistant", " assistant for coding", StringComparison.Ordinal);
            await server.Context.Prompts.SaveAsync(stale, oldText, "simulate an older default", null, token).ConfigureAwait(false);
            stale = (await server.Context.Database.PromptTemplates.ReadAsync(stale.TenantId, stale.Id, token).ConfigureAwait(false))!;
            stale.IsCustomized = false;
            await server.Context.Database.PromptTemplates.UpdateAsync(stale, token).ConfigureAwait(false);
            PromptTemplateResponse customDescribed = (await ta.Client.GetAsync("/v1.0/api/prompts/" + custom.Id, token).ConfigureAwait(false)).Json<PromptTemplateResponse>();
            string customText = customDescribed.Content.Replace("You rank research findings", "You strictly rank research findings", StringComparison.Ordinal);
            (await ta.Client.PutAsync("/v1.0/api/prompts/" + custom.Id, new { content = customText }, token).ConfigureAwait(false)).Expect(200, "customize");

            int changed = await server.Context.Prompts.EnsureDefaultsAsync(stale.TenantId, token).ConfigureAwait(false);
            Assert.Equal(1, changed, "one prompt upgraded");
            PromptTemplateResponse upgraded = (await ta.Client.GetAsync("/v1.0/api/prompts/" + stale.Id, token).ConfigureAwait(false)).Json<PromptTemplateResponse>();
            Assert.True(upgraded.Content == defaultExtract && !upgraded.Template.IsCustomized, "uncustomized prompt follows the default");
            PromptTemplateResponse kept = (await ta.Client.GetAsync("/v1.0/api/prompts/" + custom.Id, token).ConfigureAwait(false)).Json<PromptTemplateResponse>();
            Assert.True(kept.Content == customText && kept.Template.IsCustomized, "customized prompt kept");
            Assert.Equal(0, await server.Context.Prompts.EnsureDefaultsAsync(stale.TenantId, token).ConfigureAwait(false), "idempotent");
        }
    }
}
