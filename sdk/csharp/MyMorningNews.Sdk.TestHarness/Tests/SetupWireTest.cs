namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Requests;
    using MyMorningNews.Sdk.Serialization;

    /// <summary>
    /// Offline: the Setup options response reads agent CLI providers, groups, TurnedOff, and sign-in methods; a connect
    /// status reads Connection and a check's BaseUrl; the connect request writes
    /// the provider and sign-in method as names and leaves out unset values; ConnectAsync rejects a null request.
    /// </summary>
    public class SetupWireTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name
        {
            get
            {
                return "Offline: setup options and connect wire shapes";
            }
        }

        /// <summary>
        /// Offline.
        /// </summary>
        public override bool RequiresServer
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SetupWireTest()
        {
        }

        /// <summary>
        /// Run the case.
        /// </summary>
        /// <param name="context">Shared state.</param>
        /// <param name="result">Result.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public override async Task RunAsync(TestContext context, TestResult result, CancellationToken token)
        {
            string json = "{\"serverContainer\":\"mymorningnews-server\",\"providers\":["
                + "{\"provider\":\"ClaudeCode\",\"group\":\"AgentCli\",\"available\":false,\"installed\":true,\"turnedOff\":true,\"unavailableReason\":\"turned off\","
                + "\"recommended\":false,\"defaultModel\":\"claude-opus-5-5\",\"needsBaseUrl\":false,\"methods\":["
                + "{\"method\":\"AccessToken\",\"input\":\"Text\",\"recommended\":true},{\"method\":\"ServerSignIn\",\"input\":\"None\",\"recommended\":false}]},"
                + "{\"provider\":\"Codex\",\"group\":\"AgentCli\",\"methods\":[{\"method\":\"AuthFile\",\"input\":\"File\",\"fileName\":\"auth.json\",\"recommended\":true}]},"
                + "{\"provider\":\"Ollama\",\"group\":\"SelfHosted\",\"needsBaseUrl\":true,\"defaultBaseUrl\":\"http://127.0.0.1:12120\",\"methods\":[{\"method\":\"None\",\"input\":\"None\"}]}]}";
            SetupOptionsResponse? options = MyMorningNewsSerializer.Deserialize<SetupOptionsResponse>(json);
            Helpers.NotNull(options, "options");
            Helpers.Equal("mymorningnews-server", options!.ServerContainer, "server container");
            Helpers.Equal(3, options.Providers.Count, "providers");
            SetupProviderOption cc = options.Providers[0];
            Helpers.Equal(ModelProviderEnum.ClaudeCode, cc.Provider, "Claude Code provider");
            Helpers.Equal(SetupProviderGroupEnum.AgentCli, cc.Group, "agent CLI group");
            Helpers.True(!cc.Available && cc.Installed && cc.TurnedOff, "installed but turned off");
            Helpers.True(!cc.Recommended, "Claude Code is not the recommended provider");
            Helpers.Equal("turned off", cc.UnavailableReason, "unavailable reason");
            Helpers.Equal(ModelAuthMethodEnum.AccessToken, cc.Methods[0].Method, "AccessToken first");
            Helpers.True(cc.Methods[0].Recommended, "first method recommended");
            Helpers.Equal(ModelAuthMethodEnum.ServerSignIn, cc.Methods[1].Method, "server sign-in");
            Helpers.Equal(ModelProviderEnum.Codex, options.Providers[1].Provider, "Codex provider");
            Helpers.Equal("auth.json", options.Providers[1].Methods[0].FileName, "Codex sign-in file");
            Helpers.Equal(SetupProviderGroupEnum.SelfHosted, options.Providers[2].Group, "self-hosted group");
            Helpers.Equal(ModelAuthMethodEnum.None, options.Providers[2].Methods[0].Method, "no credentials");
            Helpers.True(options.Providers[2].NeedsBaseUrl, "Ollama needs a base URL");

            SetupStatusResponse? connected = MyMorningNewsSerializer.Deserialize<SetupStatusResponse>("{\"ready\":true,\"canConfigure\":true,\"checks\":["
                + "{\"key\":\"model:mdl_x\",\"kind\":\"Model\",\"required\":true,\"state\":\"Ready\",\"provider\":\"Ollama\",\"baseUrl\":\"http://127.0.0.1:12120\",\"authMethod\":\"None\"}],"
                + "\"connection\":{\"modelEndpointId\":\"mdl_y\",\"succeeded\":false,\"keptPrevious\":true,\"message\":\"401 Unauthorized\"}}");
            Helpers.NotNull(connected, "status");
            Helpers.Equal("http://127.0.0.1:12120", connected!.Checks[0].BaseUrl, "check base URL");
            SetupConnectionResult connection = Helpers.NotNull(connected.Connection, "connection");
            Helpers.True(!connection.Succeeded && connection.KeptPrevious, "failed test kept the working model");
            Helpers.Equal("mdl_y", connection.ModelEndpointId, "tried endpoint");
            Helpers.Equal("401 Unauthorized", connection.Message, "connection message");
            SetupStatusResponse? plain = MyMorningNewsSerializer.Deserialize<SetupStatusResponse>("{\"ready\":false,\"checks\":[]}");
            Helpers.True(plain != null && plain.Connection == null, "connection is null outside connect");

            ModelEndpoint? endpoint = MyMorningNewsSerializer.Deserialize<ModelEndpoint>("{\"id\":\"mdl_x\",\"provider\":\"GeminiCli\",\"authMethod\":\"AuthFile\"}");
            Helpers.NotNull(endpoint, "endpoint");
            Helpers.Equal(ModelProviderEnum.GeminiCli, endpoint!.Provider, "Gemini CLI provider");
            Helpers.Equal<ModelAuthMethodEnum?>(ModelAuthMethodEnum.AuthFile, endpoint.AuthMethod, "endpoint auth method");

            SetupConnectRequest request = new SetupConnectRequest
            {
                Provider = ModelProviderEnum.ClaudeCode,
                AuthMethod = ModelAuthMethodEnum.AccessToken,
                Secret = "sk-ant-oat01-test"
            };
            string body = MyMorningNewsSerializer.Serialize(request);
            Helpers.True(body.Contains("\"provider\":\"ClaudeCode\"", StringComparison.Ordinal), "provider written as a name");
            Helpers.True(body.Contains("\"authMethod\":\"AccessToken\"", StringComparison.Ordinal), "auth method written as a name");
            Helpers.True(body.Contains("\"secret\":\"sk-ant-oat01-test\"", StringComparison.Ordinal), "secret written");
            Helpers.True(!body.Contains("\"modelEndpointId\"", StringComparison.Ordinal) && !body.Contains("\"baseUrl\"", StringComparison.Ordinal), "unset values left out");

            using (MyMorningNewsClient offline = new MyMorningNewsClient("http://127.0.0.1:1"))
            {
                bool threw = false;
                try
                {
                    await offline.Setup.ConnectAsync(null!, token).ConfigureAwait(false);
                }
                catch (ArgumentNullException)
                {
                    threw = true;
                }

                Helpers.True(threw, "ConnectAsync rejects a null request");
            }
        }
    }
}
