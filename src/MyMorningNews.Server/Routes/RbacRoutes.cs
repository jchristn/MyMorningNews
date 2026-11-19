namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;
    using MyMorningNews.Core.Telemetry;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// RBAC routes: roles, permissions, user role assignments, credential scope assignments, and effective permissions.
    /// Built-in roles and permissions (null tenant) are listed but cannot be edited; clone them into a tenant role instead.
    /// </summary>
    public class RbacRoutes : RouteBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public RbacRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            string t = "/v1.0/tenants/{tenantId}";
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, t + "/roles", ListRolesAsync, null, openApiMetadata: Paged(Doc("List roles (built-in and tenant)", "Roles").WithParameter(QueryParam("isBuiltIn", typeof(bool))).WithParameter(QueryParam("active", typeof(bool)))));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, t + "/roles", CreateRoleAsync, null, openApiMetadata: Doc("Create a tenant role", "Roles").WithRequestBody(Body<RoleUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, t + "/roles/{roleId}", ReadRoleAsync, null, openApiMetadata: Doc("Read a role with its permissions", "Roles"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, t + "/roles/{roleId}", UpdateRoleAsync, null, openApiMetadata: Doc("Update a tenant role", "Roles").WithRequestBody(Body<RoleUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, t + "/roles/{roleId}", DeleteRoleAsync, null, openApiMetadata: Doc("Delete a tenant role", "Roles"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, t + "/permissions", ListPermissionsAsync, null, openApiMetadata: Paged(Doc("List permissions", "Permissions").WithParameter(QueryParam("includeBuiltIn", typeof(bool))).WithParameter(QueryParam("permissionType", typeof(PermissionTypeEnum)))));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, t + "/permissions", CreatePermissionAsync, null, openApiMetadata: Doc("Create a tenant permission", "Permissions").WithRequestBody(Body<PermissionUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, t + "/permissions/{permissionId}", UpdatePermissionAsync, null, openApiMetadata: Doc("Update a tenant permission", "Permissions").WithRequestBody(Body<PermissionUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, t + "/permissions/{permissionId}", DeletePermissionAsync, null, openApiMetadata: Doc("Delete a tenant permission", "Permissions"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, t + "/users/{userId}/assignments", ListAssignmentsAsync, null, openApiMetadata: Paged(Doc("List a user's role assignments", "Assignments")));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, t + "/users/{userId}/assignments", CreateAssignmentAsync, null, openApiMetadata: Doc("Assign a role to a user", "Assignments").WithRequestBody(Body<AssignmentUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, t + "/users/{userId}/assignments/{assignmentId}", DeleteAssignmentAsync, null, openApiMetadata: Doc("Remove a role assignment", "Assignments"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, t + "/credentials/{credentialId}/scopes", ListScopesAsync, null, openApiMetadata: Paged(Doc("List a credential's scope assignments", "Assignments")));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, t + "/credentials/{credentialId}/scopes", CreateScopeAsync, null, openApiMetadata: Doc("Add a credential scope assignment", "Assignments").WithRequestBody(Body<AssignmentUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, t + "/credentials/{credentialId}/scopes/{scopeId}", DeleteScopeAsync, null, openApiMetadata: Doc("Remove a credential scope assignment", "Assignments"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, t + "/users/{userId}/permissions", UserPermissionsAsync, null, openApiMetadata: Doc("Effective permissions for a user", "Permissions"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, t + "/credentials/{credentialId}/permissions", CredentialPermissionsAsync, null, openApiMetadata: Doc("Effective permissions for a credential", "Permissions"));
        }

        #region Roles

        private async Task ListRolesAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.RoleRead).ConfigureAwait(false)) return;
            UserRoleFilter filter = new UserRoleFilter { TenantId = tenantId, IncludeGlobal = true, IsBuiltIn = QueryBool(ctx, "isBuiltIn"), Active = QueryBool(ctx, "active") };
            ApplyPaging(ctx, filter);
            await SendJsonAsync(ctx, 200, await Server.Database.Roles.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task CreateRoleAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.RoleWrite).ConfigureAwait(false)) return;
            RoleUpsertRequest? req = await ReadBodyAsync<RoleUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            if (String.IsNullOrWhiteSpace(req.Name))
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "Name is required.").ConfigureAwait(false);
                return;
            }

            if (BuiltInRoles.Find(req.Name) != null)
            {
                await SendErrorAsync(ctx, 409, "Conflict", "That name is reserved for a built-in role.").ConfigureAwait(false);
                return;
            }

            UserRole role = await Server.Database.Roles.CreateAsync(new UserRole
            {
                TenantId = tenantId,
                Name = req.Name.Trim(),
                Description = req.Description,
                DefaultScope = req.DefaultScope ?? ResourceScopeEnum.Tenant,
                Active = req.Active ?? true
            }, ctx.Token).ConfigureAwait(false);

            string? error = await SetRolePermissionsAsync(tenantId, role, req.PermissionIds, ctx.Token).ConfigureAwait(false);
            if (error != null)
            {
                await Server.Database.Roles.DeleteAsync(tenantId, role.Id, ctx.Token).ConfigureAwait(false);
                await SendErrorAsync(ctx, 400, "ValidationFailed", error).ConfigureAwait(false);
                return;
            }

            await ChangedAsync(ctx, AuditEventTypeEnum.RoleChanged, "Role " + role.Id + " created.", 201, "role_created").ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, role).ConfigureAwait(false);
        }

        private async Task ReadRoleAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.RoleRead).ConfigureAwait(false)) return;
            UserRole? role = await ReadVisibleRoleAsync(tenantId, Param(ctx, "roleId"), ctx.Token).ConfigureAwait(false);
            if (role == null)
            {
                await SendNotFoundAsync(ctx, "Role").ConfigureAwait(false);
                return;
            }

            EnumerationResult<RolePermissionMap> maps = await Server.Database.RolePermissionMaps.EnumerateAsync(
                new RolePermissionMapFilter { TenantId = role.TenantId, RoleId = role.Id, PageSize = 1000 }, ctx.Token).ConfigureAwait(false);
            List<Permission> permissions = new List<Permission>();
            foreach (RolePermissionMap map in maps.Items)
            {
                Permission? p = await Server.Database.Permissions.ReadByIdAsync(map.PermissionId, ctx.Token).ConfigureAwait(false);
                if (p != null) permissions.Add(p);
            }

            await SendJsonAsync(ctx, 200, new RoleDetailResponse { Role = role, Permissions = permissions }).ConfigureAwait(false);
        }

        private async Task UpdateRoleAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.RoleWrite).ConfigureAwait(false)) return;
            RoleUpsertRequest? req = await ReadBodyAsync<RoleUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            UserRole? role = await ReadVisibleRoleAsync(tenantId, Param(ctx, "roleId"), ctx.Token).ConfigureAwait(false);
            if (role == null)
            {
                await SendNotFoundAsync(ctx, "Role").ConfigureAwait(false);
                return;
            }

            if (role.TenantId == null || role.IsProtected)
            {
                await SendErrorAsync(ctx, 409, "Conflict", "Built-in and protected roles cannot be edited. Create a tenant role instead.").ConfigureAwait(false);
                return;
            }

            if (!String.IsNullOrWhiteSpace(req.Name)) role.Name = req.Name.Trim();
            if (req.Description != null) role.Description = req.Description;
            if (req.DefaultScope.HasValue) role.DefaultScope = req.DefaultScope.Value;
            if (req.Active.HasValue) role.Active = req.Active.Value;
            role = await Server.Database.Roles.UpdateAsync(role, ctx.Token).ConfigureAwait(false);
            string? error = await SetRolePermissionsAsync(tenantId, role, req.PermissionIds, ctx.Token).ConfigureAwait(false);
            if (error != null)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", error).ConfigureAwait(false);
                return;
            }

            await ChangedAsync(ctx, AuditEventTypeEnum.RoleChanged, "Role " + role.Id + " updated.", 200, "role_updated").ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, role).ConfigureAwait(false);
        }

        private async Task DeleteRoleAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.RoleDelete).ConfigureAwait(false)) return;
            UserRole? role = await ReadVisibleRoleAsync(tenantId, Param(ctx, "roleId"), ctx.Token).ConfigureAwait(false);
            if (role == null)
            {
                await SendNotFoundAsync(ctx, "Role").ConfigureAwait(false);
                return;
            }

            if (role.TenantId == null || role.IsProtected)
            {
                await SendErrorAsync(ctx, 409, "Conflict", "Built-in and protected roles cannot be deleted.").ConfigureAwait(false);
                return;
            }

            await Server.Database.Roles.DeleteAsync(tenantId, role.Id, ctx.Token).ConfigureAwait(false);
            await ChangedAsync(ctx, AuditEventTypeEnum.RoleChanged, "Role " + role.Id + " deleted.", 204, "role_deleted").ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        #endregion

        #region Permissions

        private async Task ListPermissionsAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.PermissionRead).ConfigureAwait(false)) return;
            PermissionFilter filter = new PermissionFilter { TenantId = tenantId, IncludeGlobal = QueryBool(ctx, "includeBuiltIn") ?? true, PermissionType = QueryEnum<PermissionTypeEnum>(ctx, "permissionType") };
            ApplyPaging(ctx, filter);
            await SendJsonAsync(ctx, 200, await Server.Database.Permissions.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task CreatePermissionAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.PermissionWrite).ConfigureAwait(false)) return;
            PermissionUpsertRequest? req = await ReadBodyAsync<PermissionUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            Dictionary<string, string> errors = ValidatePermission(req, true);
            if (errors.Count > 0)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "The permission is not valid.", errors).ConfigureAwait(false);
                return;
            }

            Permission permission = await Server.Database.Permissions.CreateAsync(new Permission
            {
                TenantId = tenantId,
                Name = req.Name!.Trim(),
                ResourceTypes = Normalize<ResourceTypeEnum>(req.ResourceTypes!),
                OperationTypes = Normalize<OperationTypeEnum>(req.OperationTypes!),
                PermissionType = req.PermissionType ?? PermissionTypeEnum.Permit,
                Active = req.Active ?? true
            }, ctx.Token).ConfigureAwait(false);
            await ChangedAsync(ctx, AuditEventTypeEnum.PermissionChanged, "Permission " + permission.Id + " created.", 201, "permission_changed").ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, permission).ConfigureAwait(false);
        }

        private async Task UpdatePermissionAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.PermissionWrite).ConfigureAwait(false)) return;
            PermissionUpsertRequest? req = await ReadBodyAsync<PermissionUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            Permission? permission = await Server.Database.Permissions.ReadAsync(tenantId, Param(ctx, "permissionId") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (permission == null)
            {
                await SendNotFoundAsync(ctx, "Permission").ConfigureAwait(false);
                return;
            }

            Dictionary<string, string> errors = ValidatePermission(req, false);
            if (permission.IsProtected) errors["permission"] = "Protected permissions cannot be edited.";
            if (errors.Count > 0)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "The permission is not valid.", errors).ConfigureAwait(false);
                return;
            }

            if (!String.IsNullOrWhiteSpace(req.Name)) permission.Name = req.Name.Trim();
            if (req.ResourceTypes != null) permission.ResourceTypes = Normalize<ResourceTypeEnum>(req.ResourceTypes);
            if (req.OperationTypes != null) permission.OperationTypes = Normalize<OperationTypeEnum>(req.OperationTypes);
            if (req.PermissionType.HasValue) permission.PermissionType = req.PermissionType.Value;
            if (req.Active.HasValue) permission.Active = req.Active.Value;
            permission = await Server.Database.Permissions.UpdateAsync(permission, ctx.Token).ConfigureAwait(false);
            await ChangedAsync(ctx, AuditEventTypeEnum.PermissionChanged, "Permission " + permission.Id + " updated.", 200, "permission_changed").ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, permission).ConfigureAwait(false);
        }

        private async Task DeletePermissionAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.PermissionDelete).ConfigureAwait(false)) return;
            Permission? permission = await Server.Database.Permissions.ReadAsync(tenantId, Param(ctx, "permissionId") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (permission == null)
            {
                await SendNotFoundAsync(ctx, "Permission").ConfigureAwait(false);
                return;
            }

            if (permission.IsProtected)
            {
                await SendErrorAsync(ctx, 409, "Conflict", "Protected permissions cannot be deleted.").ConfigureAwait(false);
                return;
            }

            await Server.Database.Permissions.DeleteAsync(tenantId, permission.Id, ctx.Token).ConfigureAwait(false);
            await ChangedAsync(ctx, AuditEventTypeEnum.PermissionChanged, "Permission " + permission.Id + " deleted.", 204, "permission_changed").ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        #endregion

        #region Assignments

        private async Task ListAssignmentsAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.AssignmentRead).ConfigureAwait(false)) return;
            UserRoleAssignmentFilter filter = new UserRoleAssignmentFilter { TenantId = tenantId, UserId = Param(ctx, "userId") };
            ApplyPaging(ctx, filter);
            await SendJsonAsync(ctx, 200, await Server.Database.UserRoleAssignments.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task CreateAssignmentAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.AssignmentWrite).ConfigureAwait(false)) return;
            AssignmentUpsertRequest? req = await ReadBodyAsync<AssignmentUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            string userId = Param(ctx, "userId") ?? String.Empty;
            if (await Server.Database.Users.ReadAsync(tenantId, userId, ctx.Token).ConfigureAwait(false) == null)
            {
                await SendNotFoundAsync(ctx, "User").ConfigureAwait(false);
                return;
            }

            string? error = await ValidateRoleReferenceAsync(tenantId, req, ctx.Token).ConfigureAwait(false);
            if (error != null)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", error).ConfigureAwait(false);
                return;
            }

            UserRoleAssignment assignment = await Server.Database.UserRoleAssignments.CreateAsync(new UserRoleAssignment
            {
                TenantId = tenantId,
                UserId = userId,
                RoleId = req.RoleId,
                RoleName = req.RoleName,
                ResourceScope = req.ResourceScope,
                ResourceId = req.ResourceScope == ResourceScopeEnum.Resource ? req.ResourceId : null,
                InheritsToChildren = req.InheritsToChildren,
                Active = req.Active
            }, ctx.Token).ConfigureAwait(false);
            await ChangedAsync(ctx, AuditEventTypeEnum.AssignmentChanged, "Assignment " + assignment.Id + " created.", 201, "role_assigned").ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, assignment).ConfigureAwait(false);
        }

        private async Task DeleteAssignmentAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.AssignmentDelete).ConfigureAwait(false)) return;
            UserRoleAssignment? assignment = await Server.Database.UserRoleAssignments.ReadAsync(tenantId, Param(ctx, "assignmentId") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (assignment == null || assignment.UserId != Param(ctx, "userId"))
            {
                await SendNotFoundAsync(ctx, "Assignment").ConfigureAwait(false);
                return;
            }

            await Server.Database.UserRoleAssignments.DeleteAsync(tenantId, assignment.Id, ctx.Token).ConfigureAwait(false);
            await ChangedAsync(ctx, AuditEventTypeEnum.AssignmentChanged, "Assignment " + assignment.Id + " deleted.", 204, "role_assigned").ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task ListScopesAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.AssignmentRead).ConfigureAwait(false)) return;
            CredentialScopeAssignmentFilter filter = new CredentialScopeAssignmentFilter { TenantId = tenantId, CredentialId = Param(ctx, "credentialId") };
            ApplyPaging(ctx, filter);
            await SendJsonAsync(ctx, 200, await Server.Database.CredentialScopeAssignments.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task CreateScopeAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.AssignmentWrite).ConfigureAwait(false)) return;
            AssignmentUpsertRequest? req = await ReadBodyAsync<AssignmentUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            string credentialId = Param(ctx, "credentialId") ?? String.Empty;
            if (await Server.Database.Credentials.ReadAsync(tenantId, credentialId, ctx.Token).ConfigureAwait(false) == null)
            {
                await SendNotFoundAsync(ctx, "Credential").ConfigureAwait(false);
                return;
            }

            bool hasRole = !String.IsNullOrEmpty(req.RoleId) || !String.IsNullOrEmpty(req.RoleName);
            if (hasRole)
            {
                string? error = await ValidateRoleReferenceAsync(tenantId, req, ctx.Token).ConfigureAwait(false);
                if (error != null)
                {
                    await SendErrorAsync(ctx, 400, "ValidationFailed", error).ConfigureAwait(false);
                    return;
                }
            }
            else if (!req.ResourceTypes.Any() || !req.OperationTypes.Any() || !AllParse<ResourceTypeEnum>(req.ResourceTypes) || !AllParse<OperationTypeEnum>(req.OperationTypes))
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "Supply a role, or valid resourceTypes and operationTypes.").ConfigureAwait(false);
                return;
            }

            CredentialScopeAssignment scope = await Server.Database.CredentialScopeAssignments.CreateAsync(new CredentialScopeAssignment
            {
                TenantId = tenantId,
                CredentialId = credentialId,
                RoleId = req.RoleId,
                RoleName = req.RoleName,
                ResourceScope = req.ResourceScope,
                ResourceId = req.ResourceScope == ResourceScopeEnum.Resource ? req.ResourceId : null,
                OperationTypes = hasRole ? new List<string>() : Normalize<OperationTypeEnum>(req.OperationTypes),
                ResourceTypes = hasRole ? new List<string>() : Normalize<ResourceTypeEnum>(req.ResourceTypes),
                Active = req.Active
            }, ctx.Token).ConfigureAwait(false);
            await ChangedAsync(ctx, AuditEventTypeEnum.AssignmentChanged, "Credential scope " + scope.Id + " created.", 201, "role_assigned").ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, scope).ConfigureAwait(false);
        }

        private async Task DeleteScopeAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.AssignmentDelete).ConfigureAwait(false)) return;
            CredentialScopeAssignment? scope = await Server.Database.CredentialScopeAssignments.ReadAsync(tenantId, Param(ctx, "scopeId") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (scope == null || scope.CredentialId != Param(ctx, "credentialId"))
            {
                await SendNotFoundAsync(ctx, "Credential scope").ConfigureAwait(false);
                return;
            }

            await Server.Database.CredentialScopeAssignments.DeleteAsync(tenantId, scope.Id, ctx.Token).ConfigureAwait(false);
            await ChangedAsync(ctx, AuditEventTypeEnum.AssignmentChanged, "Credential scope " + scope.Id + " deleted.", 204, "role_assigned").ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        #endregion

        #region Effective-Permissions

        private async Task UserPermissionsAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            RequestContext rc = Context(ctx);
            string userId = Param(ctx, "userId") ?? String.Empty;
            bool self = String.Equals(rc.UserId, userId, StringComparison.Ordinal) && rc.CredentialId == null;
            if (!self && !await AuthorizeAsync(ctx, RequestTypeEnum.EffectivePermissionRead).ConfigureAwait(false)) return;
            User? user = await Server.Database.Users.ReadAsync(tenantId, userId, ctx.Token).ConfigureAwait(false);
            if (user == null)
            {
                await SendNotFoundAsync(ctx, "User").ConfigureAwait(false);
                return;
            }

            List<EffectiveGrant> grants = await Server.Authorization.GetUserGrantsAsync(tenantId, userId, ctx.Token).ConfigureAwait(false);
            string? bypass = user.IsAdmin ? "Global administrator." : user.IsTenantAdmin ? "Tenant administrator." : null;
            await SendJsonAsync(ctx, 200, BuildResponse(PrincipalTypeEnum.User, userId, tenantId, grants, bypass)).ConfigureAwait(false);
        }

        private async Task CredentialPermissionsAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.EffectivePermissionRead).ConfigureAwait(false)) return;
            Credential? credential = await Server.Database.Credentials.ReadAsync(tenantId, Param(ctx, "credentialId") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (credential == null)
            {
                await SendNotFoundAsync(ctx, "Credential").ConfigureAwait(false);
                return;
            }

            List<EffectiveGrant> grants = await Server.Authorization.GetCredentialGrantsAsync(tenantId, credential.Id, credential.UserId, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, BuildResponse(PrincipalTypeEnum.Credential, credential.Id, tenantId, grants, null)).ConfigureAwait(false);
        }

        private static EffectivePermissionsResponse BuildResponse(PrincipalTypeEnum type, string id, string tenantId, List<EffectiveGrant> grants, string? bypass)
        {
            EffectivePermissionsResponse response = new EffectivePermissionsResponse
            {
                PrincipalType = type,
                PrincipalId = id,
                TenantId = tenantId,
                Bypass = bypass != null,
                BypassReason = bypass,
                Grants = grants
            };

            foreach (ResourceTypeEnum rt in Enum.GetValues<ResourceTypeEnum>().Where(r => r != ResourceTypeEnum.All))
            {
                foreach (OperationTypeEnum op in Enum.GetValues<OperationTypeEnum>().Where(o => o != OperationTypeEnum.All && o != OperationTypeEnum.Write))
                {
                    bool permitted = bypass != null || PermissionEvaluator.Evaluate(grants, rt, op, null, out List<EffectiveGrant> _) == AuthorizationOutcomeEnum.Permitted;
                    response.Checks.Add(new PermissionCheckResult { ResourceType = rt, Operation = op, Permitted = permitted });
                }
            }

            return response;
        }

        #endregion

        #region Helpers

        private async Task<UserRole?> ReadVisibleRoleAsync(string tenantId, string? roleId, CancellationToken token)
        {
            if (String.IsNullOrEmpty(roleId)) return null;
            UserRole? role = await Server.Database.Roles.ReadByIdAsync(roleId, token).ConfigureAwait(false);
            if (role == null) return null;
            return role.TenantId == null || role.TenantId == tenantId ? role : null;
        }

        private async Task<string?> SetRolePermissionsAsync(string tenantId, UserRole role, List<string>? permissionIds, CancellationToken token)
        {
            if (permissionIds == null) return null;
            List<Permission> permissions = new List<Permission>();
            foreach (string id in permissionIds.Distinct())
            {
                Permission? p = await Server.Database.Permissions.ReadByIdAsync(id, token).ConfigureAwait(false);
                if (p == null || (p.TenantId != null && p.TenantId != tenantId)) return "Permission " + id + " was not found.";
                permissions.Add(p);
            }

            EnumerationResult<RolePermissionMap> existing = await Server.Database.RolePermissionMaps.EnumerateAsync(
                new RolePermissionMapFilter { TenantId = tenantId, RoleId = role.Id, PageSize = 1000 }, token).ConfigureAwait(false);
            foreach (RolePermissionMap map in existing.Items) await Server.Database.RolePermissionMaps.DeleteAsync(tenantId, map.Id, token).ConfigureAwait(false);
            foreach (Permission p in permissions)
                await Server.Database.RolePermissionMaps.CreateAsync(new RolePermissionMap { TenantId = tenantId, RoleId = role.Id, PermissionId = p.Id }, token).ConfigureAwait(false);
            return null;
        }

        private async Task<string?> ValidateRoleReferenceAsync(string tenantId, AssignmentUpsertRequest req, CancellationToken token)
        {
            if (req.ResourceScope == ResourceScopeEnum.Resource && String.IsNullOrWhiteSpace(req.ResourceId)) return "resourceId is required for resource-scoped assignments.";
            if (!String.IsNullOrEmpty(req.RoleId))
            {
                UserRole? role = await ReadVisibleRoleAsync(tenantId, req.RoleId, token).ConfigureAwait(false);
                return role == null ? "Role " + req.RoleId + " was not found." : null;
            }

            if (!String.IsNullOrEmpty(req.RoleName)) return BuiltInRoles.Find(req.RoleName) == null ? "Unknown built-in role " + req.RoleName + "." : null;
            return "Supply roleId or roleName.";
        }

        private static Dictionary<string, string> ValidatePermission(PermissionUpsertRequest req, bool create)
        {
            Dictionary<string, string> errors = new Dictionary<string, string>();
            if (create && String.IsNullOrWhiteSpace(req.Name)) errors["name"] = "Required.";
            if ((create || req.ResourceTypes != null) && (req.ResourceTypes == null || !req.ResourceTypes.Any() || !AllParse<ResourceTypeEnum>(req.ResourceTypes)))
                errors["resourceTypes"] = "Use one or more of: " + String.Join(", ", Enum.GetNames<ResourceTypeEnum>()) + ".";
            if ((create || req.OperationTypes != null) && (req.OperationTypes == null || !req.OperationTypes.Any() || !AllParse<OperationTypeEnum>(req.OperationTypes)))
                errors["operationTypes"] = "Use one or more of: " + String.Join(", ", Enum.GetNames<OperationTypeEnum>()) + ".";
            return errors;
        }

        private static bool AllParse<T>(IEnumerable<string> values) where T : struct, Enum
        {
            return values.All(v => Enum.TryParse(v, true, out T _));
        }

        private static List<string> Normalize<T>(IEnumerable<string> values) where T : struct, Enum
        {
            return values.Select(v => Enum.Parse<T>(v, true).ToString()).Distinct().ToList();
        }

        private async Task ChangedAsync(HttpContextBase ctx, AuditEventTypeEnum type, string description, int status, string mutation)
        {
            Server.Authorization.Invalidate();
            MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.RbacMutations, "mutation", mutation);
            await Server.Audit.RecordAsync(type, Context(ctx), status, description, ctx.Token).ConfigureAwait(false);
        }

        #endregion
    }
}
