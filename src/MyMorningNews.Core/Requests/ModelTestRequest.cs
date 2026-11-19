namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Send a sample prompt to a model endpoint.
    /// </summary>
    public class ModelTestRequest
    {
        /// <summary>
        /// Prompt.
        /// </summary>
        public string Prompt { get; set; } = "Reply with the single word: ready";

        /// <summary>
        /// Model override.
        /// </summary>
        public string? Model { get; set; } = null;
    }
}
