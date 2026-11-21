namespace MyMorningNews.Core.Responses
{
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// A model provider the Setup page can connect, with the ways it can sign in.
    /// </summary>
    public class SetupProviderOption
    {
        /// <summary>
        /// Provider.
        /// </summary>
        public ModelProviderEnum Provider { get; set; } = ModelProviderEnum.Anthropic;

        /// <summary>
        /// Group for display.
        /// </summary>
        public SetupProviderGroupEnum Group { get; set; } = SetupProviderGroupEnum.Api;

        /// <summary>
        /// Whether the provider can be used on this server now (for agent CLIs: allowed and installed).
        /// </summary>
        public bool Available { get; set; } = true;

        /// <summary>
        /// For agent CLIs: whether the executable is installed in the server. Always true for other providers.
        /// </summary>
        public bool Installed { get; set; } = true;

        /// <summary>
        /// For agent CLIs: whether the server's operator turned agent CLIs off (Models.AllowAgentClis).
        /// </summary>
        public bool TurnedOff { get; set; } = false;

        /// <summary>
        /// Why the provider is unavailable, when it is.
        /// </summary>
        public string? UnavailableReason { get; set; } = null;

        /// <summary>
        /// Whether this is the suggested provider.
        /// </summary>
        public bool Recommended { get; set; } = false;

        /// <summary>
        /// Model filled in by default; "default" lets an agent CLI choose; null means the user must name one.
        /// </summary>
        public string? DefaultModel { get; set; } = null;

        /// <summary>
        /// Whether a base URL is asked for (Ollama and OpenAI-compatible servers).
        /// </summary>
        public bool NeedsBaseUrl { get; set; } = false;

        /// <summary>
        /// Base URL filled in by default.
        /// </summary>
        public string? DefaultBaseUrl { get; set; } = null;

        /// <summary>
        /// Sign-in methods, recommended first.
        /// </summary>
        public List<SetupAuthOption> Methods { get; set; } = new List<SetupAuthOption>();
    }
}
