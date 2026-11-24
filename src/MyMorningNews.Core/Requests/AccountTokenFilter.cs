namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for AccountToken records. Null properties are not applied.
    /// </summary>
    public class AccountTokenFilter : EnumerationQuery
    {
        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match Kind exactly when set.
        /// </summary>
        public AccountTokenKindEnum? Kind { get; set; } = null;

        /// <summary>
        /// Match TokenHash exactly when set.
        /// </summary>
        public string? TokenHash { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
