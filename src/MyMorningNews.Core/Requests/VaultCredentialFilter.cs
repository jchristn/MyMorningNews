namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for VaultCredential records. Null properties are not applied.
    /// </summary>
    public class VaultCredentialFilter : EnumerationQuery
    {
        /// <summary>
        /// Match Name exactly when set.
        /// </summary>
        public string? Name { get; set; } = null;

        /// <summary>
        /// Match Kind exactly when set.
        /// </summary>
        public VaultCredentialKindEnum? Kind { get; set; } = null;

        /// <summary>
        /// Match Provider exactly when set.
        /// </summary>
        public VaultProviderEnum? Provider { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
