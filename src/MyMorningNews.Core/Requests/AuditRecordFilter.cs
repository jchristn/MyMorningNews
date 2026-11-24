namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for AuditRecord records. Null properties are not applied.
    /// </summary>
    public class AuditRecordFilter : EnumerationQuery
    {
        /// <summary>
        /// Match EventType exactly when set.
        /// </summary>
        public AuditEventTypeEnum? EventType { get; set; } = null;

        /// <summary>
        /// Match PrincipalId exactly when set.
        /// </summary>
        public string? PrincipalId { get; set; } = null;

        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match AuthorizationResult exactly when set.
        /// </summary>
        public string? AuthorizationResult { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
