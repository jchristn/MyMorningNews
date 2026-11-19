namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Create or update a voice profile. Null fields are unchanged on update.
    /// </summary>
    public class VoiceProfileUpsertRequest
    {
        /// <summary>
        /// Name.
        /// </summary>
        public string? Name { get; set; } = null;

        /// <summary>
        /// Provider.
        /// </summary>
        public TtsProviderEnum? Provider { get; set; } = null;

        /// <summary>
        /// Base URL; empty uses Tts.Endpoint.
        /// </summary>
        public string? Endpoint { get; set; } = null;

        /// <summary>
        /// Model.
        /// </summary>
        public string? Model { get; set; } = null;

        /// <summary>
        /// Narrator voice.
        /// </summary>
        public string? Voice { get; set; } = null;

        /// <summary>
        /// Second voice; empty clears it.
        /// </summary>
        public string? SecondaryVoice { get; set; } = null;

        /// <summary>
        /// Speed (0.5 to 2.0).
        /// </summary>
        public double? Speed { get; set; } = null;

        /// <summary>
        /// Vault credential; empty clears it.
        /// </summary>
        public string? CredentialId { get; set; } = null;

        /// <summary>
        /// Make this the default voice.
        /// </summary>
        public bool? IsDefault { get; set; } = null;
    }
}
