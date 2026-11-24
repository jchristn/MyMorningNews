namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>Save a private model or speech connection. Secrets are write-only; an empty key retains an unchanged provider's key.</summary>
    public class ProviderConnectionRequest
    {
        /// <summary>Chat, Embedding, or Speech. Default Chat.</summary>
        public string Kind { get; set; } = "Chat";
        /// <summary>Anthropic, OpenAi, Gemini, XAi, OpenAiCompatible, or Local speech.</summary>
        public string Provider { get; set; } = "Anthropic";
        /// <summary>Model identifier, required except for Local speech.</summary>
        public string Model { get; set; } = String.Empty;
        /// <summary>Optional API endpoint. Custom URLs must pass server egress restrictions.</summary>
        public string? Endpoint { get; set; }
        /// <summary>Write-only API key, up to 8192 characters. Null retains a key for the same provider and URL.</summary>
        public string? ApiKey { get; set; }
        /// <summary>Speech voice identifier. Required for a personal speech API.</summary>
        public string? Voice { get; set; }
    }
}
