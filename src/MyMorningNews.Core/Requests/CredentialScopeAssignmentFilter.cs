namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for CredentialScopeAssignment records. Null properties are not applied.
    /// </summary>
    public class CredentialScopeAssignmentFilter : EnumerationFilter
    {
        /// <summary>
        /// Match CredentialId exactly when set.
        /// </summary>
        public string? CredentialId { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
