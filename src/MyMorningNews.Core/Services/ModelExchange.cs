namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using PolyPrompt.Clients;
    using PolyPrompt.Models;

    /// <summary>
    /// Provider-neutral view of one completed model exchange, streamed or not.
    /// </summary>
    public class ModelExchange
    {
        /// <summary>
        /// Response text.
        /// </summary>
        public string? Text { get; set; } = null;

        /// <summary>
        /// Tool calls.
        /// </summary>
        public List<ToolCall> ToolCalls { get; set; } = new List<ToolCall>();

        /// <summary>
        /// Finish reason.
        /// </summary>
        public string? FinishReason { get; set; } = null;

        /// <summary>
        /// Provider HTTP status.
        /// </summary>
        public int? StatusCode { get; set; } = null;

        /// <summary>
        /// Assistant message to append when continuing the conversation.
        /// </summary>
        public ChatMessage AssistantMessage { get; set; } = ChatMessage.Assistant(String.Empty);
    }
}
