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
        OllamaCloud
    }
}
