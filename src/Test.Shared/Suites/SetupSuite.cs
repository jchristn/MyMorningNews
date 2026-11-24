namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Server;
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
                Cases.Case(S, "Ready", "Working models and search verify with live calls, a successful call counts as proof, and edits clear it", ReadyAsync),
                Cases.Case(S, "Options", "Administrators see every provider with its sign-in methods, including agent CLIs and their sign-in files", OptionsAsync),
                Cases.Case(S, "ClaudeCodeToken", "Claude Code connects with a claude setup-token token in a private home, a rejected token is reported, and an API key pasted as a token is caught", ClaudeCodeTokenAsync),
                Cases.Case(S, "CodexAuthFile", "Codex connects with a pasted auth.json written privately once, a refreshed file is kept, and deleting the endpoint removes it", CodexAuthFileAsync),
                Cases.Case(S, "AgentClisOff", "With agent CLIs turned off they are offered as unavailable and cannot be connected", AgentClisOffAsync),
                Cases.Case(S, "SharedModel", "A workspace on the shared model uses the operator's verified model when no shared key is set", SharedModelAsync),
                Cases.Case(S, "SpeechReady", "Administrators get an optional speech check that passes against a working speech endpoint, is kept between reads, and is checked again by verify", SpeechReadyAsync),
                Cases.Case(S, "SpeechOff", "With speech turned off the speech check is missing and says who can turn it on", SpeechOffAsync),
                Cases.Case(S, "SpeechFailed", "A speech endpoint that does not answer fails the optional check with the reason and does not block setup", SpeechFailedAsync),
                Cases.Case(S, "AgentClisAdminsOnly", "With agent CLIs limited to administrators, a global administrator may connect one and a tenant administrator may not", AgentClisAdminsOnlyAsync)
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
        private const string FakeClaude = "#!/bin/sh\ncat > /dev/null\nif [ \"$CLAUDE_CODE_OAUTH_TOKEN\" = \"sk-ant-oat01-good\" ] && [ -z \"$ANTHROPIC_API_KEY\" ] && [ \"$HOME\" != \"$REAL_HOME\" ]; then\n  echo '{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":false,\"result\":\"ready\",\"session_id\":\"s1\",\"usage\":{\"input_tokens\":5,\"output_tokens\":1}}'\n  exit 0\nfi\necho '{\"type\":\"result\",\"subtype\":\"error\",\"is_error\":true,\"result\":\"Invalid API key. Please run /login\"}'\nexit 1\n";

        private const string FakeCodex = "#!/bin/sh\ncat > /dev/null\nif [ -f \"$CODEX_HOME/auth.json\" ] && grep -q refresh_token \"$CODEX_HOME/auth.json\" && [ -z \"$OPENAI_API_KEY\" ]; then\n  echo '{\"type\":\"thread.started\",\"thread_id\":\"t1\"}'\n  echo '{\"type\":\"item.completed\",\"item\":{\"id\":\"i1\",\"type\":\"agent_message\",\"text\":\"ready\"}}'\n  echo '{\"type\":\"turn.completed\",\"usage\":{\"input_tokens\":5,\"output_tokens\":1}}'\n  exit 0\nfi\necho '{\"type\":\"turn.failed\",\"error\":{\"message\":\"401 Unauthorized: not logged in\"}}'\nexit 1\n";

        private static string WriteScript(string folder, string name, string body)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, body);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            return path;
        }

        private static async Task WithFakeClisAsync(Func<MyMorningNewsServer, string, Task> body, CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            ModelSettings models = server.Context.Settings.Models;
            string root = Path.Combine(Path.GetTempPath(), "mmn-cli-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            string claude = models.ClaudeCodeExecutable;
            string codex = models.CodexExecutable;
            string home = models.CliHomeDirectory;
            bool allowed = models.AllowAgentClis;
            Environment.SetEnvironmentVariable("REAL_HOME", Environment.GetEnvironmentVariable("HOME"));
            try
            {
                models.ClaudeCodeExecutable = WriteScript(Path.Combine(root, "bin"), "claude", FakeClaude);
                models.CodexExecutable = WriteScript(Path.Combine(root, "bin"), "codex", FakeCodex);
                models.CliHomeDirectory = Path.Combine(root, "homes");
                models.AllowAgentClis = true;
                await body(server, models.CliHomeDirectory).ConfigureAwait(false);
            }
            finally
            {
                models.ClaudeCodeExecutable = claude;
                models.CodexExecutable = codex;
                models.CliHomeDirectory = home;
                models.AllowAgentClis = allowed;
                try { Directory.Delete(root, true); } catch (IOException) { }
            }
        }

        private static async Task OptionsAsync(CancellationToken token)
        {
            TestUser[] users = await WorkspaceAsync(token).ConfigureAwait(false);
            SetupOptionsResponse o = (await users[0].Client.GetAsync("/v1.0/api/setup/options", token).ConfigureAwait(false)).Expect(200, "options").Json<SetupOptionsResponse>();
            SetupProviderOption cc = o.Providers.Single(p => p.Provider == ModelProviderEnum.ClaudeCode);
            Assert.True(cc.Group == SetupProviderGroupEnum.AgentCli && cc.Methods.Select(m => m.Method).SequenceEqual(new[] { ModelAuthMethodEnum.AccessToken, ModelAuthMethodEnum.ApiKey, ModelAuthMethodEnum.ServerSignIn }),
                "Claude Code: token, key, or server sign-in");
            SetupProviderOption cx = o.Providers.Single(p => p.Provider == ModelProviderEnum.Codex);
            Assert.True(cx.Methods[0].Method == ModelAuthMethodEnum.AuthFile && cx.Methods[0].Input == "File" && cx.Methods[0].FileName == "auth.json", "Codex takes auth.json first");
            Assert.Equal("oauth_creds.json", o.Providers.Single(p => p.Provider == ModelProviderEnum.GeminiCli).Methods[0].FileName, "Gemini CLI sign-in file");
            Assert.True(o.Providers.Single(p => p.Provider == ModelProviderEnum.Anthropic).Recommended, "the Anthropic API is recommended");
            Assert.True(o.Providers.Single(p => p.Provider == ModelProviderEnum.Ollama).NeedsBaseUrl, "Ollama asks for an address");
            (await users[1].Client.GetAsync("/v1.0/api/setup/options", token).ConfigureAwait(false)).Expect(403, "members cannot see options");
        }

        private static async Task ClaudeCodeTokenAsync(CancellationToken token)
        {
            if (OperatingSystem.IsWindows()) return;
            await WithFakeClisAsync(async (server, homes) =>
            {
                TestUser ta = (await WorkspaceAsync(token).ConfigureAwait(false))[0];
                List<SearchProvider> providers = (await ta.Client.GetAsync("/v1.0/api/search-providers", token).ConfigureAwait(false)).Expect(200, "providers").Json<EnumerationResult<SearchProvider>>().Items;
                SearchProvider searx = providers.First(p => p.Kind == SearchProviderKindEnum.Searxng);
                (await ta.Client.PutAsync("/v1.0/api/search-providers/" + searx.Id, new { endpoint = StubModelServer.Shared.BaseUrl + "/setup" }, token).ConfigureAwait(false)).Expect(200, "isolated search dependency");
                ErrorResponse wrongKind = (await ta.Client.PostAsync("/v1.0/api/setup/connect", new { provider = "ClaudeCode", authMethod = "AccessToken", secret = "sk-ant-api03-abc" }, token).ConfigureAwait(false))
                    .Expect(400, "an API key pasted as a token").Json<ErrorResponse>();
                Assert.True(wrongKind.Message.Contains("Anthropic API key", StringComparison.Ordinal) && wrongKind.Details != null && wrongKind.Details["field"] == "secret", "caught, on the secret field: " + wrongKind.Message);

                SetupStatusResponse rejected = (await ta.Client.PostAsync("/v1.0/api/setup/connect", new { provider = "ClaudeCode", authMethod = "AccessToken", secret = "sk-ant-oat01-bad" }, token).ConfigureAwait(false))
                    .Expect(200, "bad token").Json<SetupStatusResponse>();
                SetupCheck failed = rejected.Checks.Single(c => c.Kind == SetupCheckKindEnum.Model);
                Assert.True(failed.Provider == "ClaudeCode" && failed.State == SetupCheckStateEnum.Failed && failed.Message!.Contains("not signed in", StringComparison.Ordinal),
                    "every chat task now uses Claude Code and its rejection is shown: " + failed.State + " " + failed.Message);

                SetupStatusResponse good = (await ta.Client.PostAsync("/v1.0/api/setup/connect", new { provider = "ClaudeCode", authMethod = "AccessToken", secret = "sk-ant-oat01-good" }, token).ConfigureAwait(false))
                    .Expect(200, "good token").Json<SetupStatusResponse>();
                SetupCheck ok = good.Checks.Single(c => c.Kind == SetupCheckKindEnum.Model);
                Assert.True(ok.State == SetupCheckStateEnum.Ready && ok.AuthMethod == ModelAuthMethodEnum.AccessToken && ok.ResourceId == failed.ResourceId, "the same endpoint is fixed in place: " + ok.State + " " + ok.Message);
                List<VaultCredentialResponse> creds = (await ta.Client.GetAsync("/v1.0/api/vault/credentials", token).ConfigureAwait(false)).Expect(200, "credentials").Json<EnumerationResult<VaultCredentialResponse>>().Items;
                Assert.True(creds.Count(c => c.Name == "Claude Code token") == 1 && creds.Single(c => c.Name == "Claude Code token").Kind == VaultCredentialKindEnum.BearerToken, "one token credential, rotated in place");
                ModelEndpoint e = (await ta.Client.GetAsync("/v1.0/api/model-endpoints/" + ok.ResourceId, token).ConfigureAwait(false)).Expect(200, "endpoint").Json<ModelEndpoint>();
                Assert.True(e.SupportsTools == false && !e.SupportsEmbeddings && e.Endpoint == null, "agent CLIs answer in JSON, without tools or embeddings");
                Assert.True(Directory.Exists(Path.Combine(homes, ta.User.TenantId, e.Id)), "a private home for the endpoint");
                (await ta.Client.PostAsync("/v1.0/api/refinement-sessions", new { seedText = "Heat pump rebates" }, token).ConfigureAwait(false)).Expect(201, "the wizard opens");

                // Changing a working sign-in to one that fails keeps the working one.
                SetupStatusResponse change = (await ta.Client.PostAsync("/v1.0/api/setup/connect", new { provider = "ClaudeCode", authMethod = "AccessToken", secret = "sk-ant-oat01-revoked" }, token).ConfigureAwait(false))
                    .Expect(200, "failed change").Json<SetupStatusResponse>();
                Assert.True(change.Connection != null && !change.Connection.Succeeded && change.Connection.KeptPrevious && change.Ready, "the failed change is reported and the working token is kept: succeeded=" + change.Connection?.Succeeded + ", kept=" + change.Connection?.KeptPrevious + ", ready=" + change.Ready + ", checks=" + String.Join(";", change.Checks.Select(c => c.Kind + ":" + c.State + ":" + c.Message)));
                Assert.Equal(SetupCheckStateEnum.Ready, change.Checks.Single(c => c.Kind == SetupCheckKindEnum.Model).State, "still ready");
                creds = (await ta.Client.GetAsync("/v1.0/api/vault/credentials", token).ConfigureAwait(false)).Expect(200, "credentials").Json<EnumerationResult<VaultCredentialResponse>>().Items;
                Assert.True(creds.Count(c => c.Kind == VaultCredentialKindEnum.BearerToken) == 1 && creds.Single(c => c.Kind == VaultCredentialKindEnum.BearerToken).SecretPreview.EndsWith("good", StringComparison.Ordinal),
                    "the rejected token was not kept");
                (await ta.Client.PostAsync("/v1.0/api/model-endpoints/" + ok.ResourceId + "/test", new { }, token).ConfigureAwait(false)).Expect(200, "the working token still answers");

                // Switching to another provider that fails also keeps the working one.
                SetupStatusResponse other = (await ta.Client.PostAsync("/v1.0/api/setup/connect", new { provider = "Codex", authMethod = "AuthFile", secret = "{\"auth_mode\":\"chatgpt\",\"tokens\":{}}" }, token).ConfigureAwait(false))
                    .Expect(200, "failed switch").Json<SetupStatusResponse>();
                Assert.True(other.Connection!.KeptPrevious && other.Ready && other.Checks.Single(c => c.Kind == SetupCheckKindEnum.Model).Provider == "ClaudeCode", "the workspace keeps Claude Code");
            }, token).ConfigureAwait(false);
        }

        private static async Task CodexAuthFileAsync(CancellationToken token)
        {
            if (OperatingSystem.IsWindows()) return;
            await WithFakeClisAsync(async (server, homes) =>
            {
                TestUser ta = (await WorkspaceAsync(token).ConfigureAwait(false))[0];
                (await ta.Client.PostAsync("/v1.0/api/setup/connect", new { provider = "Codex", authMethod = "AuthFile", secret = "not json" }, token).ConfigureAwait(false)).Expect(400, "not a file");
                (await ta.Client.PostAsync("/v1.0/api/setup/connect", new { provider = "Codex", authMethod = "AuthFile", secret = "{\"other\":1}" }, token).ConfigureAwait(false)).Expect(400, "no sign-in in it");
                string auth = "{\"auth_mode\":\"chatgpt\",\"tokens\":{\"access_token\":\"a\",\"refresh_token\":\"r\"}}";
                SetupStatusResponse s = (await ta.Client.PostAsync("/v1.0/api/setup/connect", new { provider = "Codex", authMethod = "AuthFile", secret = auth }, token).ConfigureAwait(false))
                    .Expect(200, "connect").Json<SetupStatusResponse>();
                SetupCheck model = s.Checks.Single(c => c.Kind == SetupCheckKindEnum.Model);
                Assert.True(model.State == SetupCheckStateEnum.Ready && model.Provider == "Codex", "Codex ready: " + model.State + " " + model.Message);
                string file = Path.Combine(homes, ta.User.TenantId, model.ResourceId!, ".codex", "auth.json");
                Assert.True(File.Exists(file) && File.ReadAllText(file) == auth, "auth.json written to the endpoint's private home");
                if (!OperatingSystem.IsWindows())
                    Assert.True((File.GetUnixFileMode(file) & (UnixFileMode.GroupRead | UnixFileMode.OtherRead)) == 0, "only the server user can read it");

                string refreshed = "{\"auth_mode\":\"chatgpt\",\"tokens\":{\"access_token\":\"a2\",\"refresh_token\":\"r2\"}}";
                File.WriteAllText(file, refreshed);
                (await ta.Client.PostAsync("/v1.0/api/model-endpoints/" + model.ResourceId + "/test", new { }, token).ConfigureAwait(false)).Expect(200, "another call");
                Assert.Equal(refreshed, File.ReadAllText(file), "a file the CLI refreshed is not overwritten by the stored copy");

                (await ta.Client.DeleteAsync("/v1.0/api/model-endpoints/" + model.ResourceId, token).ConfigureAwait(false)).Expect(204, "delete endpoint");
                Assert.True(!Directory.Exists(Path.Combine(homes, ta.User.TenantId, model.ResourceId!)), "deleting the endpoint removes its sign-in");
            }, token).ConfigureAwait(false);
        }

        private static async Task AgentClisOffAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            bool allowed = server.Context.Settings.Models.AllowAgentClis;
            try
            {
                server.Context.Settings.Models.AllowAgentClis = false;
                TestUser ta = (await WorkspaceAsync(token).ConfigureAwait(false))[0];
                SetupOptionsResponse o = (await ta.Client.GetAsync("/v1.0/api/setup/options", token).ConfigureAwait(false)).Expect(200, "options").Json<SetupOptionsResponse>();
                Assert.True(o.Providers.Where(p => p.Group == SetupProviderGroupEnum.AgentCli).All(p => !p.Available && p.UnavailableReason != null), "agent CLIs are shown as unavailable");
                Assert.True(o.Providers.Where(p => p.Group == SetupProviderGroupEnum.Api).All(p => p.Available), "APIs stay available");
                (await ta.Client.PostAsync("/v1.0/api/setup/connect", new { provider = "ClaudeCode", authMethod = "ServerSignIn" }, token).ConfigureAwait(false)).Expect(400, "cannot connect");
                (await ta.Client.PostAsync("/v1.0/api/model-endpoints", new { name = "cc", provider = "ClaudeCode", authMethod = "ServerSignIn" }, token).ConfigureAwait(false)).Expect(400, "nor add one on the Models page");
            }
            finally
            {
                server.Context.Settings.Models.AllowAgentClis = allowed;
            }
        }
        private static async Task AgentClisAdminsOnlyAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            ModelSettings models = server.Context.Settings.Models;
            bool allowed = models.AllowAgentClis;
            bool adminsOnly = models.AgentClisAdminsOnly;
            try
            {
                models.AllowAgentClis = true;
                models.AgentClisAdminsOnly = true;
                TestUser ta = (await WorkspaceAsync(token).ConfigureAwait(false))[0];
                SetupOptionsResponse mine = (await ta.Client.GetAsync("/v1.0/api/setup/options", token).ConfigureAwait(false)).Expect(200, "tenant admin options").Json<SetupOptionsResponse>();
                SetupProviderOption cc = mine.Providers.Single(p => p.Provider == ModelProviderEnum.ClaudeCode);
                Assert.True(!cc.Available && cc.TurnedOff && cc.UnavailableReason!.Contains("only administrators", StringComparison.Ordinal), "a tenant administrator cannot use agent CLIs: " + cc.UnavailableReason);
                Assert.True(mine.Providers.Where(p => p.Group == SetupProviderGroupEnum.Api).All(p => p.Available), "API keys stay available");
                (await ta.Client.PostAsync("/v1.0/api/setup/connect", new { provider = "ClaudeCode", authMethod = "AccessToken", secret = "sk-ant-oat01-x" }, token).ConfigureAwait(false)).Expect(400, "connect refused");
                (await ta.Client.PostAsync("/v1.0/api/model-endpoints", new { name = "cc", provider = "ClaudeCode", authMethod = "ServerSignIn" }, token).ConfigureAwait(false)).Expect(400, "endpoint refused");

                TestApiClient global = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
                SetupOptionsResponse theirs = (await global.GetAsync("/v1.0/api/setup/options", token).ConfigureAwait(false)).Expect(200, "global admin options").Json<SetupOptionsResponse>();
                SetupProviderOption gcc = theirs.Providers.Single(p => p.Provider == ModelProviderEnum.ClaudeCode);
                Assert.True(!gcc.TurnedOff && (gcc.Available || !gcc.Installed), "a global administrator may connect agent CLIs (unless the CLI is not installed here)");
            }
            finally
            {
                models.AllowAgentClis = allowed;
                models.AgentClisAdminsOnly = adminsOnly;
            }
        }
        private static async Task SpeechReadyAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TtsSettings tts = server.Context.Settings.Tts;
            TestUser[] users = await WorkspaceAsync(token).ConfigureAwait(false);
            SetupStatusResponse s = await StatusAsync(users[0], token).ConfigureAwait(false);
            SetupCheck speech = s.Checks.Single(c => c.Kind == SetupCheckKindEnum.Speech);
            Assert.True(speech.Key == "speech" && !speech.Required && speech.State == SetupCheckStateEnum.Ready && speech.Message == null, "speech is an optional check that passes: " + speech.State + " " + speech.Message);
            Assert.True(speech.Model == tts.Model && speech.Voice == tts.Voice && speech.Provider == "OpenAiCompatibleSpeech" && speech.BaseUrl == tts.Endpoint
                && speech.ResourceName!.StartsWith("127.0.0.1:", StringComparison.Ordinal) && speech.VerifiedUtc.HasValue, "it names the endpoint host, model, and default voice: " + speech.ResourceName);
            Assert.True(!(await StatusAsync(users[1], token).ConfigureAwait(false)).Checks.Any(c => c.Kind == SetupCheckKindEnum.Speech), "members do not see the speech check");

            int before = SpeechProbes();
            SetupCheck again = (await StatusAsync(users[0], token).ConfigureAwait(false)).Checks.Single(c => c.Kind == SetupCheckKindEnum.Speech);
            Assert.True(again.State == SetupCheckStateEnum.Ready && SpeechProbes() == before, "reading the status again does not call the speech endpoint again");
            SetupStatusResponse verified = (await users[0].Client.PostAsync("/v1.0/api/setup/verify", null, token).ConfigureAwait(false)).Expect(200, "verify").Json<SetupStatusResponse>();
            Assert.True(verified.Checks.Single(c => c.Kind == SetupCheckKindEnum.Speech).State == SetupCheckStateEnum.Ready && SpeechProbes() == before + 1, "verify speaks one more sample");
            int requests = StubModelServer.Shared.Requests.Count;
            SetupStatusResponse only = (await users[0].Client.PostAsync("/v1.0/api/setup/verify?check=Speech", null, token).ConfigureAwait(false)).Expect(200, "verify speech only").Json<SetupStatusResponse>();
            Assert.True(only.Checks.Any(c => c.Kind == SetupCheckKindEnum.Model) && only.Checks.Single(c => c.Kind == SetupCheckKindEnum.Speech).State == SetupCheckStateEnum.Ready
                && SpeechProbes() == before + 2 && StubModelServer.Shared.Requests.Count == requests, "check=Speech re-checks only the speech endpoint and still reports every check");
            (await users[1].Client.PostAsync("/v1.0/api/setup/verify?check=Speech", null, token).ConfigureAwait(false)).Expect(403, "members cannot re-check");
        }

        private static int SpeechProbes()
        {
            return StubModelServer.Shared.SpeechRequests.Count(r => r.Contains("\"Ready.\"", StringComparison.Ordinal));
        }

        private static async Task SpeechOffAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TtsSettings tts = server.Context.Settings.Tts;
            bool enabled = tts.Enabled;
            try
            {
                tts.Enabled = false;
                TestUser ta = (await WorkspaceAsync(token).ConfigureAwait(false))[0];
                SetupCheck speech = (await StatusAsync(ta, token).ConfigureAwait(false)).Checks.Single(c => c.Kind == SetupCheckKindEnum.Speech);
                Assert.True(!speech.Required && speech.State == SetupCheckStateEnum.Missing && speech.Message!.Contains("turned off", StringComparison.Ordinal) && speech.Message.Contains("Server settings", StringComparison.Ordinal),
                    "speech turned off is reported as missing: " + speech.State + " " + speech.Message);
                (await ta.Client.PostAsync("/v1.0/api/voices/preview", new { voice = "af_heart" }, token).ConfigureAwait(false)).Expect(409, "nothing to preview while speech is off");
            }
            finally
            {
                tts.Enabled = enabled;
            }
        }

        private static async Task SpeechFailedAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TtsSettings tts = server.Context.Settings.Tts;
            string endpoint = tts.Endpoint;
            try
            {
                tts.Endpoint = "http://127.0.0.1:" + PortAllocator.GetFreePort() + "/v1";
                TestUser ta = (await WorkspaceAsync(token).ConfigureAwait(false))[0];
                SetupStatusResponse s = await StatusAsync(ta, token).ConfigureAwait(false);
                SetupCheck speech = s.Checks.Single(c => c.Kind == SetupCheckKindEnum.Speech);
                Assert.True(!speech.Required && speech.State == SetupCheckStateEnum.Failed && speech.Message!.Contains("Could not reach the speech endpoint", StringComparison.Ordinal),
                    "a dead speech endpoint fails the check with the reason: " + speech.State + " " + speech.Message);
                Assert.True(s.Checks.Where(c => c.Required).All(c => c.Kind != SetupCheckKindEnum.Speech), "speech never blocks setup");
                ErrorResponse preview = (await ta.Client.PostAsync("/v1.0/api/voices/preview", new { voice = tts.Voice }, token).ConfigureAwait(false)).Expect(502, "preview reports the dead endpoint").Json<ErrorResponse>();
                Assert.Equal("TtsError", preview.Error, "preview error code");

                tts.Endpoint = endpoint;
                SetupCheck back = (await StatusAsync(ta, token).ConfigureAwait(false)).Checks.Single(c => c.Kind == SetupCheckKindEnum.Speech);
                Assert.True(back.State == SetupCheckStateEnum.Ready, "a changed endpoint is checked at once: " + back.State + " " + back.Message);
            }
            finally
            {
                tts.Endpoint = endpoint;
            }
        }

        private static async Task SharedModelAsync(CancellationToken token)
        {
            string variable = MyMorningNews.Core.Constants.EnvironmentPrefix + "ANTHROPIC_API_KEY";
            string? before = Environment.GetEnvironmentVariable(variable);
            TestApiClient global = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            ModelEndpoint? operatorEndpoint = null;
            ModelAssignment? assignment = null;
            try
            {
                Environment.SetEnvironmentVariable(variable, null);
                TestUser ta = (await WorkspaceAsync(token).ConfigureAwait(false))[0];
                // New workspaces on a hosted server get the seeded Anthropic endpoint on the shared key; set that here.
                MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
                SetupCheck seeded = (await StatusAsync(ta, token).ConfigureAwait(false)).Checks.First(c => c.Kind == SetupCheckKindEnum.Model);
                ModelEndpoint mine = (await server.Context.Database.ModelEndpoints.ReadAsync(ta.User.TenantId, seeded.ResourceId!, token).ConfigureAwait(false))!;
                mine.CredentialId = "platform:anthropic";
                await server.Context.Database.ModelEndpoints.UpdateAsync(mine, token).ConfigureAwait(false);
                (await ta.Client.PostAsync("/v1.0/api/refinement-sessions", new { seedText = "Heat pump rebates" }, token).ConfigureAwait(false)).Expect(409, "no shared key and no operator model: refused");

                operatorEndpoint = (await global.PostAsync("/v1.0/api/model-endpoints", new { name = "operator stub " + Guid.NewGuid().ToString("N").Substring(0, 6), provider = "OpenAiCompatible", endpoint = StubModelServer.Shared.BaseUrl, model = "stub-refine", supportsChat = true }, token).ConfigureAwait(false))
                    .Expect(201, "operator endpoint").Json<ModelEndpoint>();
                assignment = (await global.PutAsync("/v1.0/api/model-assignments", new { task = "Refinement", modelEndpointId = operatorEndpoint.Id }, token).ConfigureAwait(false)).Json<ModelAssignment>();
                (await global.PostAsync("/v1.0/api/model-endpoints/" + operatorEndpoint.Id + "/test", new { }, token).ConfigureAwait(false)).Expect(200, "operator model answers, which verifies it");

                SetupCheck check = (await StatusAsync(ta, token).ConfigureAwait(false)).Checks.First(c => c.Kind == SetupCheckKindEnum.Model && c.Tasks.Contains(ModelTaskEnum.Refinement));
                Assert.True(check.State == SetupCheckStateEnum.Ready && check.UsesPlatformKey && check.ResourceId == operatorEndpoint.Id, "the workspace's refinement runs on the operator's model: " + check.State + " " + check.Message);
                RefinementStateResponse session = (await ta.Client.PostAsync("/v1.0/api/refinement-sessions", new { seedText = "Heat pump rebates" }, token).ConfigureAwait(false)).Expect(201, "wizard starts").Json<RefinementStateResponse>();
                (await ta.Client.PostAsync("/v1.0/api/refinement-sessions/" + session.Session.Id + "/dimensions", new { }, token).ConfigureAwait(false)).Expect(200, "a model step runs on the operator's model");
                EnumerationResult<LlmCall> calls = (await ta.Client.GetAsync("/v1.0/api/llm-calls?pageSize=5", token).ConfigureAwait(false)).Expect(200, "calls").Json<EnumerationResult<LlmCall>>();
                Assert.True(calls.Items.Count > 0 && calls.Items.All(c => c.TenantId == ta.User.TenantId), "the calls are logged in the workspace that made them");
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, before);
                if (assignment != null) await global.DeleteAsync("/v1.0/api/model-assignments/" + assignment.Id, token).ConfigureAwait(false);
                if (operatorEndpoint != null) await global.DeleteAsync("/v1.0/api/model-endpoints/" + operatorEndpoint.Id, token).ConfigureAwait(false);
            }
        }
    }
}
