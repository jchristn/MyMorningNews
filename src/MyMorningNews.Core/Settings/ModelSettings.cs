namespace MyMorningNews.Core.Settings
{
    using System;

    /// <summary>
    /// Defaults used when seeding a tenant's model endpoints. Existing endpoints are never changed by these settings.
    /// </summary>
    public class ModelSettings
    {
        #region Public-Members

        /// <summary>
        /// Default chat model for the seeded Anthropic endpoint. Default claude-opus-5-5.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Chat model for the seeded Anthropic endpoint.")]
        public string DefaultChatModel
        {
            get { return _DefaultChatModel; }
            set { _DefaultChatModel = String.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(DefaultChatModel)) : value.Trim(); }
        }

        /// <summary>
        /// Ollama base URL for the seeded embedding endpoint. Default http://host.docker.internal:11434.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Ollama URL for the seeded embedding endpoint.", EnvironmentVariable = "OLLAMA_ENDPOINT")]
        public string DefaultOllamaEndpoint
        {
            get { return _DefaultOllamaEndpoint; }
            set { _DefaultOllamaEndpoint = String.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(DefaultOllamaEndpoint)) : value.Trim(); }
        }

        /// <summary>
        /// Embedding model for the seeded Ollama endpoint. Default all-minilm (384 dimensions).
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Embedding model for the seeded Ollama endpoint.")]
        public string DefaultEmbeddingModel
        {
            get { return _DefaultEmbeddingModel; }
            set { _DefaultEmbeddingModel = String.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(DefaultEmbeddingModel)) : value.Trim(); }
        }

        /// <summary>
        /// Whether model endpoints may use agent CLIs (Claude Code, Codex, Gemini CLI, Mux) running inside the server.
        /// Default true. Turn it off on a shared or hosted server: the CLIs run as the server's own user, with read-only tools
        /// in a private, empty folder per endpoint.
        /// </summary>
        [Setting(Description = "Allow model endpoints that run agent CLIs (Claude Code, Codex, Gemini CLI, Mux) inside the server.", EnvironmentVariable = "MODELS_ALLOW_AGENT_CLIS")]
        public bool AllowAgentClis { get; set; } = true;

        /// <summary>
        /// When agent CLIs are allowed, limit connecting them to global administrators. Default false. The hosted profile
        /// turns it on: anyone can sign up there, so only the server's operators may run CLIs inside the server.
        /// </summary>
        [Setting(Description = "Only global administrators can connect agent CLI endpoints.", EnvironmentVariable = "MODELS_AGENT_CLIS_ADMINS_ONLY")]
        public bool AgentClisAdminsOnly { get; set; } = false;

        /// <summary>
        /// Folder that holds a private home folder for each agent CLI endpoint (its sign-in file and an empty working
        /// folder). Default .mymorningnews/cli under the server user's home, which the Docker stack keeps in the clihome volume.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Folder for each agent CLI endpoint's private sign-in and working folders.", EnvironmentVariable = "MODELS_CLI_HOME_DIRECTORY")]
        public string CliHomeDirectory
        {
            get { return _CliHomeDirectory; }
            set { _CliHomeDirectory = String.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(CliHomeDirectory)) : value.Trim(); }
        }

        /// <summary>
        /// Claude Code executable, a name on PATH or a full path. Default claude.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Claude Code executable (name on PATH or full path).")]
        public string ClaudeCodeExecutable
        {
            get { return _ClaudeCodeExecutable; }
            set { _ClaudeCodeExecutable = String.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(ClaudeCodeExecutable)) : value.Trim(); }
        }

        /// <summary>
        /// Codex executable, a name on PATH or a full path. Default codex.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Codex executable (name on PATH or full path).")]
        public string CodexExecutable
        {
            get { return _CodexExecutable; }
            set { _CodexExecutable = String.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(CodexExecutable)) : value.Trim(); }
        }

        /// <summary>
        /// Gemini CLI executable, a name on PATH or a full path. Default gemini.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Gemini CLI executable (name on PATH or full path).")]
        public string GeminiCliExecutable
        {
            get { return _GeminiCliExecutable; }
            set { _GeminiCliExecutable = String.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(GeminiCliExecutable)) : value.Trim(); }
        }

        /// <summary>
        /// Mux executable, a name on PATH or a full path. Default mux.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Mux executable (name on PATH or full path).")]
        public string MuxExecutable
        {
            get { return _MuxExecutable; }
            set { _MuxExecutable = String.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(MuxExecutable)) : value.Trim(); }
        }

        #endregion

        #region Private-Members

        private string _DefaultChatModel = "claude-opus-5-5";
        private string _DefaultOllamaEndpoint = "http://host.docker.internal:11434";
        private string _DefaultEmbeddingModel = "all-minilm";
        private string _CliHomeDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".mymorningnews", "cli");
        private string _ClaudeCodeExecutable = "claude";
        private string _CodexExecutable = "codex";
        private string _GeminiCliExecutable = "gemini";
        private string _MuxExecutable = "mux";

        #endregion
    }
}
