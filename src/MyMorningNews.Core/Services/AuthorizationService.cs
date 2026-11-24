namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;

    /// <summary>
    /// Authorizes requests: administrative bypasses, then effective-permission RBAC with explicit deny over permit.
    /// Effective grants are cached per principal and tenant and invalidated through a monotonic version counter.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class AuthorizationService
    {
        #region Public-Members

        /// <summary>
        /// Current authorization data version. Incremented on any RBAC change.
        /// </summary>
        public long Version
        {
            get { return Interlocked.Read(ref _Version); }
        }

        #endregion

        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly ConcurrentDictionary<string, CachedGrants> _Cache = new ConcurrentDictionary<string, CachedGrants>(StringComparer.Ordinal);
        private long _Version = 1;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <exception cref="ArgumentNullException">database is null.</exception>
        public AuthorizationService(DatabaseDriverBase database)
        {
            ArgumentNullException.ThrowIfNull(database);
            _Database = database;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Invalidate every cached grant set. Call after any change to roles, permissions, assignments, users, or credentials.
        /// </summary>
        public void Invalidate()
        {
            Interlocked.Increment(ref _Version);
            _Cache.Clear();
        }

        /// <summary>
        /// Authorize a request type for the principal in a request context. The result is stored on <c>ctx.Authorization</c>.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="requestType">Request type.</param>
        /// <param name="resourceId">Target resource identifier, when the operation targets one.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Authorization result.</returns>
        /// <exception cref="ArgumentNullException">ctx is null.</exception>
        public async Task<AuthorizationContext> AuthorizeAsync(RequestContext ctx, RequestTypeEnum requestType, string? resourceId = null, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            RequiredPermission required = OperationScopeMap.Get(requestType);
            AuthorizationContext result = new AuthorizationContext
            {
                RequestType = requestType,
                ResourceType = required.ResourceType,
                Operation = required.Operation,
                ResourceId = resourceId
            };
            ctx.RequestType = requestType;
            ctx.Authorization = result;

            if (required.Anonymous)
            {
                result.Outcome = AuthorizationOutcomeEnum.Permitted;
                return result;
            }

            if (!ctx.IsAuthenticated)
            {
                result.Outcome = AuthorizationOutcomeEnum.DeniedImplicit;
                result.Reason = "Authentication required.";
                return result;
            }

            if (required.AnyAuthenticated)
            {
                result.Outcome = AuthorizationOutcomeEnum.Permitted;
                return result;
            }

            if (ctx.Authentication.IsSystemAdministrator)
            {
                result.Outcome = AuthorizationOutcomeEnum.Permitted;
                result.BypassReason = "System administration key.";
                return result;
            }

            if (ctx.IsAdmin)
            {
                result.Outcome = AuthorizationOutcomeEnum.Permitted;
                result.BypassReason = "User is a global administrator (IsAdmin).";
                return result;
            }

            if (ctx.IsTenantAdmin && !String.IsNullOrEmpty(ctx.TenantId))
            {
                result.Outcome = AuthorizationOutcomeEnum.Permitted;
                result.BypassReason = "User is a tenant administrator (IsTenantAdmin) within their tenant.";
                return result;
            }

            if (String.IsNullOrEmpty(ctx.TenantId))
            {
                result.Outcome = AuthorizationOutcomeEnum.DeniedImplicit;
                result.Reason = "The request could not be bound to a tenant.";
                return result;
            }

            List<EffectiveGrant> grants = ctx.CredentialId != null
                ? await GetCredentialGrantsAsync(ctx.TenantId, ctx.CredentialId, ctx.UserId, token).ConfigureAwait(false)
                : await GetUserGrantsAsync(ctx.TenantId, ctx.UserId ?? String.Empty, token).ConfigureAwait(false);

            if (ctx.CredentialId != null && ctx.Scopes.Any() && !LegacyScopeAllows(ctx.Scopes, required.Operation))
            {
                result.Outcome = AuthorizationOutcomeEnum.DeniedImplicit;
                result.Reason = "Credential scope does not include " + required.Operation + ".";
                return result;
            }

            AuthorizationOutcomeEnum outcome = PermissionEvaluator.Evaluate(grants, required.ResourceType, required.Operation, resourceId, out List<EffectiveGrant> matched);
            result.Outcome = outcome;
            result.Matched = matched;
            if (outcome == AuthorizationOutcomeEnum.DeniedExplicit) result.Reason = "An explicit deny permission matched " + required + ".";
            else if (outcome == AuthorizationOutcomeEnum.DeniedImplicit) result.Reason = "No permission grants " + required + ".";
            return result;
        }

        /// <summary>
        /// Whether a principal holds a permission, without recording it on a request.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="resourceType">Resource type.</param>
        /// <param name="operation">Operation.</param>
        /// <param name="resourceId">Resource identifier, optional.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when permitted.</returns>
        public async Task<bool> HasPermissionAsync(RequestContext ctx, ResourceTypeEnum resourceType, OperationTypeEnum operation, string? resourceId = null, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            if (!ctx.IsAuthenticated) return false;
            if (ctx.Authentication.IsSystemAdministrator || ctx.IsAdmin || ctx.IsTenantAdmin) return true;
            if (String.IsNullOrEmpty(ctx.TenantId)) return false;
            List<EffectiveGrant> grants = ctx.CredentialId != null
                ? await GetCredentialGrantsAsync(ctx.TenantId, ctx.CredentialId, ctx.UserId, token).ConfigureAwait(false)
                : await GetUserGrantsAsync(ctx.TenantId, ctx.UserId ?? String.Empty, token).ConfigureAwait(false);
            return PermissionEvaluator.Evaluate(grants, resourceType, operation, resourceId, out List<EffectiveGrant> _) == AuthorizationOutcomeEnum.Permitted;
        }

        /// <summary>
        /// Effective grants for a user in a tenant. Users without assignments receive the TenantMember role.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Grants.</returns>
        public async Task<List<EffectiveGrant>> GetUserGrantsAsync(string tenantId, string userId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(userId)) return new List<EffectiveGrant>();
            string key = "u:" + tenantId + ":" + userId;
            if (TryGetCached(key, out List<EffectiveGrant>? cached)) return cached!;

            UserRoleAssignmentFilter filter = new UserRoleAssignmentFilter { TenantId = tenantId, UserId = userId, Active = true, PageSize = 1000 };
            EnumerationResult<UserRoleAssignment> assignments = await _Database.UserRoleAssignments.EnumerateAsync(filter, token).ConfigureAwait(false);

            List<EffectiveGrant> grants = new List<EffectiveGrant>();
            if (assignments.Objects.Count == 0)
            {
                grants.AddRange(await ExpandRoleAsync(tenantId, null, BuiltInRoles.TenantMember, ResourceScopeEnum.Tenant, null, true, token).ConfigureAwait(false));
            }
            else
            {
                foreach (UserRoleAssignment a in assignments.Objects)
                {
                    grants.AddRange(await ExpandRoleAsync(tenantId, a.RoleId, a.RoleName, a.ResourceScope, a.ResourceId, a.InheritsToChildren, token).ConfigureAwait(false));
                }
            }

            _Cache[key] = new CachedGrants(Version, grants);
            return grants;
        }

        /// <summary>
        /// Effective grants for a credential, intersected with the owning user's grants (owner ceiling).
        /// A credential without scope assignments inherits its owner's grants.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="credentialId">Credential.</param>
        /// <param name="ownerUserId">Owning user.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Grants.</returns>
        public async Task<List<EffectiveGrant>> GetCredentialGrantsAsync(string tenantId, string credentialId, string? ownerUserId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(credentialId)) throw new ArgumentNullException(nameof(credentialId));
            string key = "c:" + tenantId + ":" + credentialId;
            if (TryGetCached(key, out List<EffectiveGrant>? cached)) return cached!;

            List<EffectiveGrant> owner = String.IsNullOrEmpty(ownerUserId) ? new List<EffectiveGrant>() : await GetUserGrantsAsync(tenantId, ownerUserId, token).ConfigureAwait(false);
            User? ownerUser = String.IsNullOrEmpty(ownerUserId) ? null : await _Database.Users.ReadAsync(tenantId, ownerUserId, token).ConfigureAwait(false);
            bool ownerIsAdmin = ownerUser != null && (ownerUser.IsAdmin || ownerUser.IsTenantAdmin);

            CredentialScopeAssignmentFilter filter = new CredentialScopeAssignmentFilter { TenantId = tenantId, CredentialId = credentialId, Active = true, PageSize = 1000 };
            EnumerationResult<CredentialScopeAssignment> assignments = await _Database.CredentialScopeAssignments.EnumerateAsync(filter, token).ConfigureAwait(false);

            List<EffectiveGrant> grants;
            if (assignments.Objects.Count == 0)
            {
                grants = ownerIsAdmin
                    ? new List<EffectiveGrant> { new EffectiveGrant { ResourceType = ResourceTypeEnum.All, Operation = OperationTypeEnum.All, Source = "owner:administrator" } }
                    : new List<EffectiveGrant>(owner);
            }
            else
            {
                List<EffectiveGrant> credential = new List<EffectiveGrant>();
                foreach (CredentialScopeAssignment a in assignments.Objects)
                {
                    if (!String.IsNullOrEmpty(a.RoleId) || !String.IsNullOrEmpty(a.RoleName))
                        credential.AddRange(await ExpandRoleAsync(tenantId, a.RoleId, a.RoleName, a.ResourceScope, a.ResourceId, true, token).ConfigureAwait(false));
                    foreach (string type in a.ResourceTypes)
                    {
                        if (!Enum.TryParse(type, true, out ResourceTypeEnum rt)) continue;
                        foreach (string op in a.OperationTypes)
                        {
                            if (!Enum.TryParse(op, true, out OperationTypeEnum ot)) continue;
                            credential.Add(new EffectiveGrant { ResourceType = rt, Operation = ot, ResourceScope = a.ResourceScope, ResourceId = a.ResourceId, Source = "credential-scope:" + a.Id });
                        }
                    }
                }

                grants = ownerIsAdmin ? credential : Ceiling(credential, owner);
            }

            _Cache[key] = new CachedGrants(Version, grants);
            return grants;
        }

        #endregion

        #region Private-Methods

        private bool TryGetCached(string key, out List<EffectiveGrant>? grants)
        {
            grants = null;
            if (_Cache.TryGetValue(key, out CachedGrants? entry) && entry.Version == Version)
            {
                grants = entry.Grants;
                return true;
            }

            return false;
        }

        private static bool LegacyScopeAllows(List<string> scopes, OperationTypeEnum operation)
        {
            bool read = scopes.Any(s => String.Equals(s, "read", StringComparison.OrdinalIgnoreCase));
            bool write = scopes.Any(s => String.Equals(s, "write", StringComparison.OrdinalIgnoreCase));
            bool admin = scopes.Any(s => String.Equals(s, "admin", StringComparison.OrdinalIgnoreCase));
            switch (operation)
            {
                case OperationTypeEnum.Read: return read || write || admin;
                case OperationTypeEnum.Admin: return admin;
                default: return write || admin;
            }
        }

        private static List<EffectiveGrant> Ceiling(List<EffectiveGrant> credential, List<EffectiveGrant> owner)
        {
            List<EffectiveGrant> result = new List<EffectiveGrant>();
            foreach (EffectiveGrant g in credential)
            {
                if (g.PermissionType == PermissionTypeEnum.Deny)
                {
                    result.Add(g);
                    continue;
                }

                bool ownerHas = owner.Any(o => o.PermissionType == PermissionTypeEnum.Permit
                    && (o.ResourceType == ResourceTypeEnum.All || o.ResourceType == g.ResourceType)
                    && (o.Operation == OperationTypeEnum.All || o.Operation == g.Operation || (o.Operation == OperationTypeEnum.Write && (g.Operation == OperationTypeEnum.Create || g.Operation == OperationTypeEnum.Update || g.Operation == OperationTypeEnum.Delete)))
                    && (o.ResourceScope == ResourceScopeEnum.Tenant || String.Equals(o.ResourceId, g.ResourceId, StringComparison.Ordinal)));
                if (ownerHas) result.Add(g);
            }

            result.AddRange(owner.Where(o => o.PermissionType == PermissionTypeEnum.Deny));
            return result;
        }

        private async Task<List<EffectiveGrant>> ExpandRoleAsync(
            string tenantId,
            string? roleId,
            string? roleName,
            ResourceScopeEnum scope,
            string? resourceId,
            bool inherits,
            CancellationToken token)
        {
            UserRole? role = null;
            if (!String.IsNullOrEmpty(roleId))
            {
                role = await _Database.Roles.ReadByIdAsync(roleId, token).ConfigureAwait(false);
                if (role != null && role.TenantId != null && role.TenantId != tenantId) role = null;
            }

            if (role == null && !String.IsNullOrEmpty(roleName))
            {
                UserRoleFilter rf = new UserRoleFilter { TenantId = tenantId, IncludeGlobal = true, Name = roleName, PageSize = 10 };
                EnumerationResult<UserRole> roles = await _Database.Roles.EnumerateAsync(rf, token).ConfigureAwait(false);
                role = roles.Objects.OrderBy(r => r.TenantId == null ? 1 : 0).FirstOrDefault();
            }

            List<EffectiveGrant> grants = new List<EffectiveGrant>();
            if (role == null)
            {
                BuiltInRoleDefinition? def = BuiltInRoles.Find(roleName);
                if (def == null) return grants;
                foreach (BuiltInPermissionDefinition p in def.Permissions)
                    foreach (ResourceTypeEnum rt in p.ResourceTypes)
                        foreach (OperationTypeEnum op in p.Operations)
                            grants.Add(new EffectiveGrant { ResourceType = rt, Operation = op, PermissionType = p.PermissionType, ResourceScope = scope, ResourceId = resourceId, InheritsToChildren = inherits, Source = "builtin:" + def.Name });
                return grants;
            }

            if (!role.Active) return grants;
            RolePermissionMapFilter mf = new RolePermissionMapFilter { TenantId = role.TenantId, IncludeGlobal = role.TenantId != null, RoleId = role.Id, Active = true, PageSize = 1000 };
            EnumerationResult<RolePermissionMap> maps = await _Database.RolePermissionMaps.EnumerateAsync(mf, token).ConfigureAwait(false);
            foreach (RolePermissionMap map in maps.Objects)
            {
                Permission? permission = await _Database.Permissions.ReadByIdAsync(map.PermissionId, token).ConfigureAwait(false);
                if (permission == null || !permission.Active) continue;
                foreach (string type in permission.ResourceTypes)
                {
                    if (!Enum.TryParse(type, true, out ResourceTypeEnum rt)) continue;
                    foreach (string op in permission.OperationTypes)
                    {
                        if (!Enum.TryParse(op, true, out OperationTypeEnum ot)) continue;
                        grants.Add(new EffectiveGrant { ResourceType = rt, Operation = ot, PermissionType = permission.PermissionType, ResourceScope = scope, ResourceId = resourceId, InheritsToChildren = inherits, Source = "role:" + role.Name });
                    }
                }
            }

            return grants;
        }

        #endregion
    }
}
