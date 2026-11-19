namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Service a stored secret belongs to.
    /// </summary>
    public enum VaultProviderEnum
    {
        /// <summary>
        /// Anthropic API.
        /// </summary>
        Anthropic,

        /// <summary>
        /// OpenAI API.
        /// </summary>
        OpenAi,

        /// <summary>
        /// Google Gemini API.
        /// </summary>
        Gemini,

        /// <summary>
        /// xAI API.
        /// </summary>
        XAi,

        /// <summary>
        /// Ollama Cloud.
        /// </summary>
        OllamaCloud,

        /// <summary>
        /// OpenAI-compatible server.
        /// </summary>
        OpenAiCompatible,

        /// <summary>
        /// Tavily search.
        /// </summary>
        Tavily,

        /// <summary>
        /// You.com search.
        /// </summary>
        YouCom,

        /// <summary>
        /// Mailgun email.
        /// </summary>
        Mailgun,

        /// <summary>
        /// ElevenLabs speech.
        /// </summary>
        ElevenLabs,

        /// <summary>
        /// MCP server.
        /// </summary>
        Mcp,

        /// <summary>
        /// Anything else.
        /// </summary>
        Custom
    }
}
