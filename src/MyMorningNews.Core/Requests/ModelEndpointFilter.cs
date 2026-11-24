namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for ModelEndpoint records. Null properties are not applied.
    /// </summary>
    public class ModelEndpointFilter : EnumerationQuery
    {
        /// <summary>
        /// Match Provider exactly when set.
        /// </summary>
        public ModelProviderEnum? Provider { get; set; } = null;

        /// <summary>
        /// Match CredentialId exactly when set.
        /// </summary>
        public string? CredentialId { get; set; } = null;

        /// <summary>
        /// Match SupportsChat exactly when set.
        /// </summary>
        public bool? SupportsChat { get; set; } = null;

        /// <summary>
        /// Match SupportsEmbeddings exactly when set.
        /// </summary>
        public bool? SupportsEmbeddings { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
