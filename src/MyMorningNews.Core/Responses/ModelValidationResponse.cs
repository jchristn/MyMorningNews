namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Result of validating a model endpoint.
    /// </summary>
    public class ModelValidationResponse
    {
        /// <summary>
        /// Endpoint reachable.
        /// </summary>
        public bool Reachable { get; set; } = false;

        /// <summary>
        /// Whether every check that applies to the endpoint passed (chat when it has a chat model, embeddings when it has an embedding model). Default false.
        /// </summary>
        public bool Succeeded { get; set; } = false;

        /// <summary>
        /// Configured chat model exists (null model counts as found).
        /// </summary>
        public bool ModelFound { get; set; } = false;

        /// <summary>
        /// Tool calls work.
        /// </summary>
        public bool? SupportsTools { get; set; } = null;

        /// <summary>
        /// Embedding dimensions, when embeddings are configured.
        /// </summary>
        public int? EmbeddingDimensions { get; set; } = null;

        /// <summary>
        /// Diagnostic messages.
        /// </summary>
        public List<string> Messages { get; set; } = new List<string>();

        /// <summary>
        /// Validation time.
        /// </summary>
        public DateTime ValidatedUtc { get; set; } = DateTime.UtcNow;
    }
}
