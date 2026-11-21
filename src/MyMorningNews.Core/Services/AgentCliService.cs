namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Settings;
    using PolyPrompt.Clients;
    using PolyPrompt.Helpers;
    using PolyPrompt.Models;
    using SyslogLogging;

    /// <summary>
    /// Runs model endpoints that are agent CLIs (Claude Code, Codex, Gemini CLI, Mux). Each endpoint gets a private home
    /// folder under <see cref="ModelSettings.CliHomeDirectory"/>: its sign-in file (written only when the stored copy
    /// changes, so tokens the CLI refreshes are kept) and an empty working folder. Runs use the CLI's most restricted
    /// permission mode, so it can only read that empty folder. Thread safety: safe for concurrent use.
    /// </summary>
    public class AgentCliService
    {
        #region Public-Members

        /// <summary>
        /// Model name stored when the CLI should use its own default model.
        /// </summary>
        public const string DefaultModel = "default";

        #endregion

        #region Private-Members

        private readonly string _Header = "[AgentCliService] ";
        private readonly Func<ModelSettings> _Settings;
        private readonly LoggingModule _Logging;
        private readonly object _FileLock = new object();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Current model settings.</param>
        /// <param name="logging">Logging.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public AgentCliService(Func<ModelSettings> settings, LoggingModule logging)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Whether a provider is an agent CLI.
        /// </summary>
        /// <param name="provider">Provider.</param>
        /// <returns>True for Claude Code, Codex, Gemini CLI, and Mux.</returns>
        public static bool IsAgentCli(ModelProviderEnum provider)
        {
            return provider == ModelProviderEnum.ClaudeCode
                || provider == ModelProviderEnum.Codex
                || provider == ModelProviderEnum.GeminiCli
                || provider == ModelProviderEnum.Mux;
        }

        /// <summary>
        /// Sign-in methods a provider accepts, recommended first.
        /// </summary>
        /// <param name="provider">Provider.</param>
        /// <returns>Methods.</returns>
        public static IReadOnlyList<ModelAuthMethodEnum> MethodsFor(ModelProviderEnum provider)
        {
            switch (provider)
            {
                case ModelProviderEnum.ClaudeCode:
                    return new[] { ModelAuthMethodEnum.AccessToken, ModelAuthMethodEnum.ApiKey, ModelAuthMethodEnum.ServerSignIn };
                case ModelProviderEnum.Codex:
                case ModelProviderEnum.GeminiCli:
                    return new[] { ModelAuthMethodEnum.AuthFile, ModelAuthMethodEnum.ApiKey, ModelAuthMethodEnum.ServerSignIn };
                case ModelProviderEnum.Mux:
                    return new[] { ModelAuthMethodEnum.AuthFile, ModelAuthMethodEnum.ServerSignIn };
                case ModelProviderEnum.Ollama:
                    return new[] { ModelAuthMethodEnum.None, ModelAuthMethodEnum.ApiKey };
                case ModelProviderEnum.OpenAiCompatible:
                    return new[] { ModelAuthMethodEnum.ApiKey, ModelAuthMethodEnum.None };
                default:
                    return new[] { ModelAuthMethodEnum.ApiKey };
            }
        }

        /// <summary>
        /// The method an endpoint uses: its own, or the provider's usual one when none is stored.
        /// </summary>
        /// <param name="endpoint">Endpoint.</param>
        /// <returns>Method.</returns>
        /// <exception cref="ArgumentNullException">endpoint is null.</exception>
        public static ModelAuthMethodEnum EffectiveMethod(ModelEndpoint endpoint)
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            if (endpoint.AuthMethod.HasValue) return endpoint.AuthMethod.Value;
            if (IsAgentCli(endpoint.Provider)) return String.IsNullOrEmpty(endpoint.CredentialId) ? ModelAuthMethodEnum.ServerSignIn : MethodsFor(endpoint.Provider)[0];
            if (endpoint.Provider == ModelProviderEnum.Ollama || endpoint.Provider == ModelProviderEnum.OpenAiCompatible)
                return String.IsNullOrEmpty(endpoint.CredentialId) ? ModelAuthMethodEnum.None : ModelAuthMethodEnum.ApiKey;
            return ModelAuthMethodEnum.ApiKey;
        }

        /// <summary>
        /// Whether a method needs a stored secret (a key, a token, or a file).
        /// </summary>
        /// <param name="method">Method.</param>
        /// <returns>True for API keys, access tokens, and sign-in files.</returns>
        public static bool NeedsSecret(ModelAuthMethodEnum method)
        {
            return method == ModelAuthMethodEnum.ApiKey || method == ModelAuthMethodEnum.AccessToken || method == ModelAuthMethodEnum.AuthFile;
        }

        /// <summary>
        /// File name a provider's sign-in file is saved as.
        /// </summary>
        /// <param name="provider">Provider.</param>
        /// <returns>File name, or null when the provider takes no file.</returns>
        public static string? AuthFileName(ModelProviderEnum provider)
        {
            switch (provider)
            {
                case ModelProviderEnum.Codex: return "auth.json";
                case ModelProviderEnum.GeminiCli: return "oauth_creds.json";
                case ModelProviderEnum.Mux: return "endpoints.json";
                default: return null;
            }
        }

        /// <summary>
        /// Executable configured for a provider.
        /// </summary>
        /// <param name="provider">Provider.</param>
        /// <returns>Executable name or path.</returns>
        /// <exception cref="ArgumentException">The provider is not an agent CLI.</exception>
        public string ExecutableFor(ModelProviderEnum provider)
        {
            ModelSettings s = _Settings();
            switch (provider)
            {
                case ModelProviderEnum.ClaudeCode: return s.ClaudeCodeExecutable;
                case ModelProviderEnum.Codex: return s.CodexExecutable;
                case ModelProviderEnum.GeminiCli: return s.GeminiCliExecutable;
                case ModelProviderEnum.Mux: return s.MuxExecutable;
                default: throw new ArgumentException(provider + " is not an agent CLI.");
            }
        }

        /// <summary>
        /// Whether a provider's CLI is installed on the server.
        /// </summary>
        /// <param name="provider">Provider.</param>
        /// <returns>True when the executable resolves.</returns>
        public bool IsInstalled(ModelProviderEnum provider)
        {
            if (!IsAgentCli(provider)) return false;
            try
            {
                return ProcessRunner.ResolveExecutable(ExecutableFor(provider)) != null;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>
        /// Whether agent CLI endpoints are allowed on this server.
        /// </summary>
        /// <returns>True when allowed.</returns>
        public bool Allowed()
        {
            return _Settings().AllowAgentClis;
        }

        /// <summary>
        /// Whether a caller may connect (create or change) an agent CLI endpoint: CLIs are allowed, and either anyone may
        /// connect them or the caller is a global administrator (<see cref="ModelSettings.AgentClisAdminsOnly"/>).
        /// </summary>
        /// <param name="globalAdmin">Whether the caller is a global administrator.</param>
        /// <returns>True when the caller may connect one.</returns>
        public bool AllowedFor(bool globalAdmin)
        {
            return Allowed() && (globalAdmin || !_Settings().AgentClisAdminsOnly);
        }

        /// <summary>
        /// Run one turn. The last message is the prompt; earlier non-system messages are history; system messages form the
        /// system prompt.
        /// </summary>
        /// <param name="endpoint">Endpoint.</param>
        /// <param name="secret">Stored secret for the endpoint's method, or null.</param>
        /// <param name="model">Model, or <see cref="DefaultModel"/> for the CLI's own default.</param>
        /// <param name="messages">Conversation.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The CLI's result.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException">The provider is not an agent CLI, there is nothing to send, or a needed secret is missing.</exception>
        /// <exception cref="InvalidOperationException">Agent CLIs are turned off on this server.</exception>
        public async Task<CliAgentResult> RunAsync(ModelEndpoint endpoint, string? secret, string model, List<ChatMessage> messages, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            ArgumentNullException.ThrowIfNull(messages);
            if (!IsAgentCli(endpoint.Provider)) throw new ArgumentException(endpoint.Provider + " is not an agent CLI.");
            if (!Allowed()) throw new InvalidOperationException("Agent CLIs are turned off on this server (Models.AllowAgentClis). Use a provider API key instead.");
            ModelAuthMethodEnum method = EffectiveMethod(endpoint);
            if (NeedsSecret(method) && String.IsNullOrWhiteSpace(secret))
                throw new ArgumentException(endpoint.Name + " needs " + Describe(method, endpoint.Provider) + ". Add it on the Setup page.");

            List<ChatMessage> conversation = messages.Where(m => !String.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase)).ToList();
            if (conversation.Count == 0) throw new ArgumentException("There is no prompt to send.");
            string system = String.Join("\n\n", messages.Where(m => String.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase)).Select(m => m.Text ?? String.Empty));
            ChatMessage last = conversation[conversation.Count - 1];

            string home = PrepareHome(endpoint, method, secret);
            string work = Path.Combine(home, "work");
            Directory.CreateDirectory(work);

            CliAgentRequest request = new CliAgentRequest(String.IsNullOrWhiteSpace(last.Text) ? "(empty)" : last.Text!)
            {
                SystemPrompt = String.IsNullOrWhiteSpace(system) ? null : system,
                History = conversation.Count > 1 ? conversation.Take(conversation.Count - 1).ToList() : null,
                Model = String.IsNullOrWhiteSpace(model) || String.Equals(model, DefaultModel, StringComparison.OrdinalIgnoreCase) ? null : model,
                WorkingDirectory = work,
                TimeoutMs = Math.Max(1000, endpoint.TimeoutMs),
                PermissionMode = CliPermissionMode.Restricted,
                Environment = BuildEnvironment(endpoint.Provider, method, secret, home)
            };

            using (CliAgentClientBase client = CreateClient(endpoint.Provider))
            {
                return await client.RunAsync(request, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Delete an endpoint's private folder (on endpoint deletion). Never throws for a missing folder.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="endpointId">Endpoint.</param>
        public void DeleteHome(string tenantId, string endpointId)
        {
            if (String.IsNullOrWhiteSpace(tenantId) || String.IsNullOrWhiteSpace(endpointId)) return;
            string folder = HomeFolder(tenantId, endpointId);
            try
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                _Logging.Warn(_Header + "unable to remove " + folder + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Plain description of what a method needs, for messages.
        /// </summary>
        /// <param name="method">Method.</param>
        /// <param name="provider">Provider.</param>
        /// <returns>Description.</returns>
        public static string Describe(ModelAuthMethodEnum method, ModelProviderEnum provider)
        {
            switch (method)
            {
                case ModelAuthMethodEnum.AccessToken: return provider == ModelProviderEnum.ClaudeCode ? "a token from claude setup-token" : "an access token";
                case ModelAuthMethodEnum.AuthFile: return "its " + (AuthFileName(provider) ?? "sign-in file");
                case ModelAuthMethodEnum.ApiKey: return "an API key";
                case ModelAuthMethodEnum.ServerSignIn: return "a sign-in on the server";
                default: return "nothing";
            }
        }

        #endregion

        #region Private-Methods

        private CliAgentClientBase CreateClient(ModelProviderEnum provider)
        {
            string executable = ExecutableFor(provider);
            ClientOptions options = new ClientOptions { Logging = _Logging };
            switch (provider)
            {
                case ModelProviderEnum.ClaudeCode: return new ClaudeCodeClient(executable, options);
                case ModelProviderEnum.Codex: return new CodexClient(executable, options);
                case ModelProviderEnum.GeminiCli: return new GeminiCliClient(executable, options);
                case ModelProviderEnum.Mux: return new MuxClient(executable, options);
                default: throw new ArgumentException(provider + " is not an agent CLI.");
            }
        }

        private string HomeFolder(string tenantId, string endpointId)
        {
            return Path.Combine(_Settings().CliHomeDirectory, Safe(tenantId), Safe(endpointId));
        }

        private static string Safe(string id)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in id) sb.Append(Char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            return sb.ToString();
        }

        private string PrepareHome(ModelEndpoint endpoint, ModelAuthMethodEnum method, string? secret)
        {
            string home = HomeFolder(endpoint.TenantId, endpoint.Id);
            lock (_FileLock)
            {
                CreatePrivateDirectory(home);
                if (method != ModelAuthMethodEnum.AuthFile || String.IsNullOrEmpty(secret)) return home;
                string? target = AuthFilePath(endpoint.Provider, home);
                if (target == null) return home;
                CreatePrivateDirectory(Path.GetDirectoryName(target)!);
                string marker = target + ".source";
                string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
                bool changed = !File.Exists(target) || !File.Exists(marker) || !String.Equals(File.ReadAllText(marker).Trim(), hash, StringComparison.Ordinal);
                if (changed)
                {
                    WritePrivateFile(target, secret);
                    WritePrivateFile(marker, hash);
                    if (endpoint.Provider == ModelProviderEnum.GeminiCli)
                        WritePrivateFile(Path.Combine(Path.GetDirectoryName(target)!, "settings.json"), "{\"security\":{\"auth\":{\"selectedType\":\"oauth-personal\"}},\"selectedAuthType\":\"oauth-personal\"}");
                }
            }

            return home;
        }

        private static string? AuthFilePath(ModelProviderEnum provider, string home)
        {
            switch (provider)
            {
                case ModelProviderEnum.Codex: return Path.Combine(home, ".codex", "auth.json");
                case ModelProviderEnum.GeminiCli: return Path.Combine(home, ".gemini", "oauth_creds.json");
                case ModelProviderEnum.Mux: return Path.Combine(home, ".mux", "endpoints.json");
                default: return null;
            }
        }

        private static Dictionary<string, string?> BuildEnvironment(ModelProviderEnum provider, ModelAuthMethodEnum method, string? secret, string home)
        {
            Dictionary<string, string?> env = new Dictionary<string, string?>(StringComparer.Ordinal);
            if (method == ModelAuthMethodEnum.ServerSignIn) return env;

            // A private home keeps the server's own sign-ins and keys out of this endpoint's runs.
            env["HOME"] = home;
            env["XDG_CONFIG_HOME"] = Path.Combine(home, ".config");
            switch (provider)
            {
                case ModelProviderEnum.ClaudeCode:
                    env["ANTHROPIC_API_KEY"] = method == ModelAuthMethodEnum.ApiKey ? secret : null;
                    env["CLAUDE_CODE_OAUTH_TOKEN"] = method == ModelAuthMethodEnum.AccessToken ? secret : null;
                    env["ANTHROPIC_AUTH_TOKEN"] = null;
                    break;
                case ModelProviderEnum.Codex:
                    env["CODEX_HOME"] = Path.Combine(home, ".codex");
                    env["OPENAI_API_KEY"] = method == ModelAuthMethodEnum.ApiKey ? secret : null;
                    env["CODEX_API_KEY"] = method == ModelAuthMethodEnum.ApiKey ? secret : null;
                    break;
                case ModelProviderEnum.GeminiCli:
                    env["GEMINI_API_KEY"] = method == ModelAuthMethodEnum.ApiKey ? secret : null;
                    env["GOOGLE_API_KEY"] = null;
                    env["GOOGLE_GENAI_USE_VERTEXAI"] = null;
                    env["GOOGLE_GENAI_USE_GCA"] = null;
                    break;
                case ModelProviderEnum.Mux:
                    env["MUX_CONFIG_DIR"] = Path.Combine(home, ".mux");
                    break;
            }

            return env;
        }

        private static void CreatePrivateDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                if (OperatingSystem.IsWindows()) Directory.CreateDirectory(path);
                else Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }

        private static void WritePrivateFile(string path, string content)
        {
            string temp = path + ".tmp";
            if (OperatingSystem.IsWindows())
            {
                File.WriteAllText(temp, content);
            }
            else
            {
                FileStreamOptions options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write, UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite };
                using (FileStream stream = new FileStream(temp, options))
                {
                    using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        writer.Write(content);
                    }
                }
            }

            File.Move(temp, path, true);
        }

        #endregion
    }
}
