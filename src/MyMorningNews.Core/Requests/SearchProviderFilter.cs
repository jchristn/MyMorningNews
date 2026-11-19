namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for SearchProvider records. Null properties are not applied.
    /// </summary>
    public class SearchProviderFilter : EnumerationFilter
    {
        /// <summary>
        /// Match Kind exactly when set.
        /// </summary>
        public SearchProviderKindEnum? Kind { get; set; } = null;

        /// <summary>
        /// Match CredentialId exactly when set.
        /// </summary>
        public string? CredentialId { get; set; } = null;

        /// <summary>
        /// Match IsDefault exactly when set.
        /// </summary>
        public bool? IsDefault { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
