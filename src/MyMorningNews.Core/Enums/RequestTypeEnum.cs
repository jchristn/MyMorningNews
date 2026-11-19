namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Classified request type; each maps to a required (resource, operation) pair in OperationScopeMap.
    /// </summary>
    public enum RequestTypeEnum
    {
        /// <summary>
        /// Unclassified request.
        /// </summary>
        Unknown,

        /// <summary>
        /// Read health.
        /// </summary>
        HealthRead,

        /// <summary>
        /// Create a session token.
        /// </summary>
        TokenCreate,

        /// <summary>
        /// Validate or inspect the current token.
        /// </summary>
        TokenRead,

        /// <summary>
        /// Refresh the current token.
        /// </summary>
        TokenRefresh,

        /// <summary>
        /// Revoke the current token.
        /// </summary>
        TokenDelete,

        /// <summary>
        /// Read tenants.
        /// </summary>
        TenantRead,

        /// <summary>
        /// Create or update tenants.
        /// </summary>
        TenantWrite,

        /// <summary>
        /// Delete tenants.
        /// </summary>
        TenantDelete,

        /// <summary>
        /// Read users.
        /// </summary>
        UserRead,

        /// <summary>
        /// Create or update users.
        /// </summary>
        UserWrite,

        /// <summary>
        /// Delete users.
        /// </summary>
        UserDelete,

        /// <summary>
        /// Read own profile.
        /// </summary>
        UserSelfRead,

        /// <summary>
        /// Update own profile or password.
        /// </summary>
        UserSelfWrite,

        /// <summary>
        /// Read credentials.
        /// </summary>
        CredentialRead,

        /// <summary>
        /// Create or update credentials.
        /// </summary>
        CredentialWrite,

        /// <summary>
        /// Delete credentials.
        /// </summary>
        CredentialDelete,

        /// <summary>
        /// Read roles.
        /// </summary>
        RoleRead,

        /// <summary>
        /// Create or update roles.
        /// </summary>
        RoleWrite,

        /// <summary>
        /// Delete roles.
        /// </summary>
        RoleDelete,

        /// <summary>
        /// Read permissions.
        /// </summary>
        PermissionRead,

        /// <summary>
        /// Create or update permissions.
        /// </summary>
        PermissionWrite,

        /// <summary>
        /// Delete permissions.
        /// </summary>
        PermissionDelete,

        /// <summary>
        /// Read role assignments.
        /// </summary>
        AssignmentRead,

        /// <summary>
        /// Create or update role assignments.
        /// </summary>
        AssignmentWrite,

        /// <summary>
        /// Delete role assignments.
        /// </summary>
        AssignmentDelete,

        /// <summary>
        /// Inspect effective permissions.
        /// </summary>
        EffectivePermissionRead,

        /// <summary>
        /// Read audit records.
        /// </summary>
        AuditRead,

        /// <summary>
        /// Delete audit records.
        /// </summary>
        AuditDelete,

        /// <summary>
        /// Read request history.
        /// </summary>
        RequestHistoryRead,

        /// <summary>
        /// Delete request history.
        /// </summary>
        RequestHistoryDelete,

        /// <summary>
        /// Read server settings.
        /// </summary>
        SettingsRead,

        /// <summary>
        /// Update server settings.
        /// </summary>
        SettingsWrite,

        /// <summary>
        /// Restart the server.
        /// </summary>
        ServerRestart,

        /// <summary>
        /// Read server information.
        /// </summary>
        ServerInfoRead
    }
}
