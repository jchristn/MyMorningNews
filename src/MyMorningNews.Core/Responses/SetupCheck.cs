namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// One thing the workspace needs before people can use it, and whether it is ready.
    /// </summary>
    public class SetupCheck
    {
        /// <summary>
        /// Stable key: model:{endpointId} (or model:none), search, embeddings, or email.
        /// </summary>
        public string Key { get; set; } = String.Empty;

        /// <summary>
        /// What the check covers.
        /// </summary>
        public SetupCheckKindEnum Kind { get; set; } = SetupCheckKindEnum.Model;

        /// <summary>
        /// Whether the workspace is blocked until this check is ready.
        /// </summary>
        public bool Required { get; set; } = true;

        /// <summary>
        /// State.
        /// </summary>
        public SetupCheckStateEnum State { get; set; } = SetupCheckStateEnum.Missing;

        /// <summary>
        /// Plain-language detail, such as the provider's error text. Null when there is nothing to add.
        /// </summary>
        public string? Message { get; set; } = null;

        /// <summary>
        /// Model endpoint or search provider ID, when the check is about one.
        /// </summary>
        public string? ResourceId { get; set; } = null;

        /// <summary>
        /// Model endpoint or search provider name.
        /// </summary>
        public string? ResourceName { get; set; } = null;

        /// <summary>
        /// Provider name (a ModelProviderEnum or SearchProviderKindEnum value).
        /// </summary>
        public string? Provider { get; set; } = null;

        /// <summary>
        /// Model name, for model and embedding checks.
        /// </summary>
        public string? Model { get; set; } = null;

        /// <summary>
        /// Server address, for Ollama and OpenAI-compatible model checks shown to administrators. Null otherwise.
        /// </summary>
        public string? BaseUrl { get; set; } = null;

        /// <summary>
        /// Tasks the endpoint serves, for model checks.
        /// </summary>
        public List<ModelTaskEnum> Tasks { get; set; } = new List<ModelTaskEnum>();

        /// <summary>
        /// How the endpoint signs in, for model checks.
        /// </summary>
        public ModelAuthMethodEnum? AuthMethod { get; set; } = null;

        /// <summary>
        /// Whether the sign-in method needs a stored secret (an API key, a token, or a sign-in file).
        /// </summary>
        public bool NeedsApiKey { get; set; } = false;

        /// <summary>
        /// Whether a key is stored or supplied by the platform.
        /// </summary>
        public bool HasApiKey { get; set; } = false;

        /// <summary>
        /// Whether the endpoint uses the hosted service's key, which users never enter.
        /// </summary>
        public bool UsesPlatformKey { get; set; } = false;

        /// <summary>
        /// When the resource was last checked (UTC), or null.
        /// </summary>
        public DateTime? VerifiedUtc { get; set; } = null;
    }
}
