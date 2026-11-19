namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Text-to-speech provider.
    /// </summary>
    public enum TtsProviderEnum
    {
        /// <summary>
        /// POST {endpoint}/audio/speech (Kokoro-FastAPI, OpenAI, and compatible servers).
        /// </summary>
        OpenAiCompatibleSpeech,

        /// <summary>
        /// No audio.
        /// </summary>
        Disabled
    }
}
