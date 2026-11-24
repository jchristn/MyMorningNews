namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for RefinementMessage records. Null properties are not applied.
    /// </summary>
    public class RefinementMessageFilter : EnumerationQuery
    {
        /// <summary>
        /// Match SessionId exactly when set.
        /// </summary>
        public string? SessionId { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
