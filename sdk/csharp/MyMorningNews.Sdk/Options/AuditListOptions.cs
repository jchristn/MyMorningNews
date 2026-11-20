namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;

    /// <summary>
    /// Filters for listing audit records.
    /// </summary>
    public class AuditListOptions : ListOptions
    {
        /// <summary>
        /// Global administrators: only this tenant (null lists every tenant). Null by default.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Only records of this user. Null by default.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Only records of this principal. Null by default.
        /// </summary>
        public string? PrincipalId { get; set; } = null;

        /// <summary>
        /// Only records with this authorization result. Null by default.
        /// </summary>
        public string? AuthorizationResult { get; set; } = null;

        /// <summary>
        /// Only records of this event type. Null by default.
        /// </summary>
        public AuditEventTypeEnum? EventType { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AuditListOptions()
        {
        }

        /// <summary>
        /// Add this resource's filters.
        /// </summary>
        /// <param name="query">Query to add to.</param>
        protected override void AddFilters(Dictionary<string, string> query)
        {
            base.AddFilters(query);
            QueryFormat.Add(query, "tenantId", TenantId);
            QueryFormat.Add(query, "userId", UserId);
            QueryFormat.Add(query, "principalId", PrincipalId);
            QueryFormat.Add(query, "authorizationResult", AuthorizationResult);
            QueryFormat.Add(query, "eventType", EventType);
        }
    }
}
