namespace MyMorningNews.Benchmark.Stub
{
    /// <summary>
    /// A dependency the stub stands in for.
    /// </summary>
    public enum FaultTarget
    {
        /// <summary>
        /// Chat completions (/v1/chat/completions).
        /// </summary>
        Model,

        /// <summary>
        /// Embeddings (/v1/embeddings).
        /// </summary>
        Embeddings,

        /// <summary>
        /// Search (/search/...).
        /// </summary>
        Search,

        /// <summary>
        /// Corpus pages (/corpus/...).
        /// </summary>
        Pages,

        /// <summary>
        /// Text to speech (/v1/audio/speech).
        /// </summary>
        Tts
    }
}
