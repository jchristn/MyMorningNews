namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Run a prompt in the prompt lab.
    /// </summary>
    public class PromptLabRequest
    {
        /// <summary>
        /// Prompt key.
        /// </summary>
        public string Key { get; set; } = String.Empty;

        /// <summary>
        /// Draft content to run instead of the saved version.
        /// </summary>
        public string? Content { get; set; } = null;

        /// <summary>
        /// Variable values (text). Missing variables render empty.
        /// </summary>
        public Dictionary<string, string> Variables { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Endpoint override.
        /// </summary>
        public string? ModelEndpointId { get; set; } = null;

        /// <summary>
        /// Model override.
        /// </summary>
        public string? Model { get; set; } = null;

        /// <summary>
        /// Topic scope for prompt resolution.
        /// </summary>
        public string? TopicId { get; set; } = null;
    }
}
