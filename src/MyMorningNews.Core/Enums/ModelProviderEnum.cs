namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Model provider reached through PolyPrompt.
    /// </summary>
    public enum ModelProviderEnum
    {
        /// <summary>
        /// Anthropic (default).
        /// </summary>
        Anthropic,

        /// <summary>
        /// OpenAI Responses API.
        /// </summary>
        OpenAi,

        /// <summary>
        /// OpenAI-compatible server (vLLM, LM Studio, Ollama /v1, OpenRouter).
        /// </summary>
        OpenAiCompatible,

        /// <summary>
        /// Ollama native API.
        /// </summary>
        Ollama,

        /// <summary>
        /// Google Gemini.
        /// </summary>
        Gemini,

        /// <summary>
        /// xAI.
        /// </summary>
        XAi,

        /// <summary>
        /// Ollama Cloud.
        /// </summary>
        OllamaCloud,

        /// <summary>
        /// Claude Code CLI (claude), signed in with a subscription token from claude setup-token, an Anthropic API key, or
        /// the sign-in already on the server.
        /// </summary>
        ClaudeCode,

        /// <summary>
        /// OpenAI Codex CLI (codex), signed in with the auth.json from a ChatGPT sign-in, an OpenAI API key, or the sign-in
        /// already on the server.
        /// </summary>
        Codex,

        /// <summary>
        /// Google Gemini CLI (gemini), signed in with the oauth_creds.json from a Google sign-in, a Gemini API key, or the
        /// sign-in already on the server.
        /// </summary>
        GeminiCli,

        /// <summary>
        /// Mux CLI (mux), configured by its endpoints.json or the configuration already on the server.
        /// </summary>
        Mux
    }
}
