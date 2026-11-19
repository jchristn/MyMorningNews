namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Security;

    /// <summary>
    /// Computed effective permissions for a principal.
    /// </summary>
    public class EffectivePermissionsResponse
    {
        /// <summary>
        /// Principal type.
        /// </summary>
        public PrincipalTypeEnum PrincipalType { get; set; } = PrincipalTypeEnum.User;

        /// <summary>
        /// Principal.
        /// </summary>
        public string PrincipalId { get; set; } = String.Empty;

        /// <summary>
        /// Tenant.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Whether an administrative bypass applies (all checks permitted).
        /// </summary>
        public bool Bypass { get; set; } = false;

        /// <summary>
        /// Bypass reason.
        /// </summary>
        public string? BypassReason { get; set; } = null;

        /// <summary>
        /// Expanded grants.
        /// </summary>
        public List<EffectiveGrant> Grants { get; set; } = new List<EffectiveGrant>();

        /// <summary>
        /// Results for every resource type and operation.
        /// </summary>
        public List<PermissionCheckResult> Checks { get; set; } = new List<PermissionCheckResult>();
    }
}
