namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Create a topic override of a tenant prompt.
    /// </summary>
    public class PromptOverrideRequest
    {
        /// <summary>
        /// Prompt key.
        /// </summary>
        public string Key { get; set; } = String.Empty;

        /// <summary>
        /// Topic.
        /// </summary>
        public string TopicId { get; set; } = String.Empty;

        /// <summary>
        /// Initial content; defaults to the tenant prompt.
        /// </summary>
        public string? Content { get; set; } = null;
    }
}
