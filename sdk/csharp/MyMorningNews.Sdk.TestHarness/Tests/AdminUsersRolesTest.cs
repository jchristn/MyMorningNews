namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Control plane: tenants (read), a tenant-scoped user (CRUD, login), a tenant role with a tenant permission, a role
    /// assignment, and effective permissions. Everything created is deleted.
    /// </summary>
    public class AdminUsersRolesTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Admin tenants, users, roles, permissions, assignments";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AdminUsersRolesTest()
        {
        }

        /// <summary>
        /// Run the case.
        /// </summary>
        /// <param name="context">Shared state.</param>
        /// <param name="result">Result.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public override async Task RunAsync(TestContext context, TestResult result, CancellationToken token)
        {
            MyMorningNewsClient c = context.RequireAdmin();
            string tenant = context.Environment.Tenant;
            EnumerationResult<Tenant> tenants = await StepAsync(context, result, "GET tenants", () => c.Admin.Tenants.ListAsync(new TenantListOptions { Active = true }, token)).ConfigureAwait(false);
            Helpers.Contains(tenants.Items, t => t.Id == tenant, "the harness tenant");
            Tenant self = await StepAsync(context, result, "GET tenant", () => c.Admin.Tenants.GetAsync(tenant, token)).ConfigureAwait(false);
            Helpers.True(self.IsProtected || self.Id == tenant, "tenant read");
            await StepAsync(context, result, "GET unknown tenant (404)", () => Helpers.ExpectErrorAsync(() => c.Admin.Tenants.GetAsync("ten_missing", token), 404)).ConfigureAwait(false);
            await StepAsync(context, result, "POST tenant without a name (400)", () => Helpers.ExpectErrorAsync(() => c.Admin.Tenants.CreateAsync(new TenantUpsertRequest(), token), 400, "ValidationFailed")).ConfigureAwait(false);

            MyMorningNewsException weak = await StepAsync(context, result, "POST user with a short password (400)", () => Helpers.ExpectErrorAsync(() => c.Admin.Users.CreateAsync(tenant,
                new UserCreateRequest { Email = "sdk-csharp-weak@example.com", Password = "x", Active = true }, token), 400, "ValidationFailed")).ConfigureAwait(false);
            Helpers.True(weak.Details.ContainsKey("password"), "details name the password");

            string password = "Harness-" + Helpers.Unique();
            UserResponse user = await StepAsync(context, result, "POST user", () => Fixtures.CreateUserAsync(context, c, password, token)).ConfigureAwait(false);
            Helpers.True(!user.IsAdmin && !user.IsTenantAdmin, "an ordinary member");
            UserResponse read = await StepAsync(context, result, "GET user", () => c.Admin.Users.GetAsync(tenant, user.Id, token)).ConfigureAwait(false);
            Helpers.Equal(user.Email, read.Email, "user email");
            UserResponse updated = await StepAsync(context, result, "PUT user", () => c.Admin.Users.UpdateAsync(tenant, user.Id, new UserUpdateRequest { FirstName = "Edited", TimeZone = "UTC" }, token)).ConfigureAwait(false);
            Helpers.Equal("Edited", updated.FirstName, "first name");
            EnumerationResult<UserResponse> users = await StepAsync(context, result, "GET users (search)", () => c.Admin.Users.ListAsync(tenant, new UserListOptions { Search = user.Email, PageSize = 50 }, token)).ConfigureAwait(false);
            Helpers.Contains(users.Items, u => u.Id == user.Id, "the user");
            await StepAsync(context, result, "POST user with a duplicate email (409)", () => Helpers.ExpectErrorAsync(() => c.Admin.Users.CreateAsync(tenant,
                new UserCreateRequest { Email = user.Email, Password = password, Active = true }, token), 409)).ConfigureAwait(false);

            using (MyMorningNewsClient member = context.NewClient())
            {
                TokenResponse login = await StepAsync(context, result, "POST token as the new user", () => member.LoginAsync(user.Email, password, tenant, token)).ConfigureAwait(false);
                Helpers.Equal(user.Id, login.UserId, "login user");
                await StepAsync(context, result, "POST tenants/{id}/users as a member (403)", () => Helpers.ExpectErrorAsync(() => member.Admin.Users.CreateAsync(tenant,
                    new UserCreateRequest { Email = "sdk-csharp-denied-" + Helpers.Unique() + "@example.com", Password = "Harness-denied-1", Active = true }, token), 403, "Forbidden")).ConfigureAwait(false);
                await StepAsync(context, result, "GET admin/promo-codes as a member (403)", () => Helpers.ExpectErrorAsync(() => member.Admin.PromoCodes.ListAsync(null, token), 403)).ConfigureAwait(false);
                await member.LogoutAsync(token).ConfigureAwait(false);
            }

            Permission perm = await StepAsync(context, result, "POST permission", () => c.Admin.Permissions.CreateAsync(tenant, new PermissionUpsertRequest
            {
                Name = context.Name("perm"),
                ResourceTypes = new List<string> { "Topic" },
                OperationTypes = new List<string> { "Read" },
                PermissionType = PermissionTypeEnum.Permit
            }, token)).ConfigureAwait(false);
            context.Cleanup.Add("permission:" + perm.Id, ct => c.Admin.Permissions.DeleteAsync(tenant, perm.Id, ct));
            Permission perm2 = await StepAsync(context, result, "PUT permission", () => c.Admin.Permissions.UpdateAsync(tenant, perm.Id, new PermissionUpsertRequest { OperationTypes = new List<string> { "Read", "Update" } }, token)).ConfigureAwait(false);
            Helpers.Equal(2, perm2.OperationTypes.Count, "operation types");
            EnumerationResult<Permission> perms = await StepAsync(context, result, "GET permissions (tenant only)", () => c.Admin.Permissions.ListAsync(tenant, new PermissionListOptions { IncludeBuiltIn = false, PageSize = 500 }, token)).ConfigureAwait(false);
            Helpers.Contains(perms.Items, p => p.Id == perm.Id, "the permission");
            Helpers.True(perms.Items.All(p => !p.IsBuiltIn), "built-in permissions are left out");

            UserRole role = await StepAsync(context, result, "POST role", () => c.Admin.Roles.CreateAsync(tenant, new RoleUpsertRequest
            {
                Name = context.Name("role"),
                Description = "Harness role",
                PermissionIds = new List<string> { perm.Id }
            }, token)).ConfigureAwait(false);
            context.Cleanup.Add("role:" + role.Id, ct => c.Admin.Roles.DeleteAsync(tenant, role.Id, ct));
            RoleDetailResponse roleDetail = await StepAsync(context, result, "GET role", () => c.Admin.Roles.GetAsync(tenant, role.Id, token)).ConfigureAwait(false);
            Helpers.Contains(roleDetail.Permissions, p => p.Id == perm.Id, "the role's permission");
            UserRole role2 = await StepAsync(context, result, "PUT role", () => c.Admin.Roles.UpdateAsync(tenant, role.Id, new RoleUpsertRequest { Description = "Edited" }, token)).ConfigureAwait(false);
            Helpers.Equal("Edited", role2.Description, "role description");
            EnumerationResult<UserRole> roles = await StepAsync(context, result, "GET roles", () => c.Admin.Roles.ListAsync(tenant, new RoleListOptions { PageSize = 500 }, token)).ConfigureAwait(false);
            Helpers.Contains(roles.Items, r => r.Id == role.Id, "the role");
            UserRole? builtIn = roles.Items.FirstOrDefault(r => r.IsBuiltIn);
            if (builtIn != null)
                await StepAsync(context, result, "PUT built-in role (409)", () => Helpers.ExpectErrorAsync(() => c.Admin.Roles.UpdateAsync(tenant, builtIn.Id, new RoleUpsertRequest { Description = "x" }, token), 409)).ConfigureAwait(false);

            UserRoleAssignment assignment = await StepAsync(context, result, "POST user assignment", () => c.Admin.Assignments.CreateUserAssignmentAsync(tenant, user.Id, new AssignmentUpsertRequest
            {
                RoleId = role.Id,
                ResourceScope = ResourceScopeEnum.Tenant
            }, token)).ConfigureAwait(false);
            EnumerationResult<UserRoleAssignment> assignments = await StepAsync(context, result, "GET user assignments", () => c.Admin.Assignments.ListUserAssignmentsAsync(tenant, user.Id, null, token)).ConfigureAwait(false);
            Helpers.Contains(assignments.Items, a => a.Id == assignment.Id, "the assignment");
            EffectivePermissionsResponse effective = await StepAsync(context, result, "GET user effective permissions", () => c.Admin.Users.GetEffectivePermissionsAsync(tenant, user.Id, token)).ConfigureAwait(false);
            Helpers.Equal(user.Id, effective.PrincipalId, "principal");
            await StepAsync(context, result, "DELETE user assignment", () => c.Admin.Assignments.DeleteUserAssignmentAsync(tenant, user.Id, assignment.Id, token)).ConfigureAwait(false);

            await StepAsync(context, result, "DELETE role", () => c.Admin.Roles.DeleteAsync(tenant, role.Id, token)).ConfigureAwait(false);
            context.Cleanup.Remove("role:" + role.Id);
            await StepAsync(context, result, "GET deleted role (404)", () => Helpers.ExpectErrorAsync(() => c.Admin.Roles.GetAsync(tenant, role.Id, token), 404)).ConfigureAwait(false);
            await StepAsync(context, result, "DELETE permission", () => c.Admin.Permissions.DeleteAsync(tenant, perm.Id, token)).ConfigureAwait(false);
            context.Cleanup.Remove("permission:" + perm.Id);
            await StepAsync(context, result, "DELETE user", () => Fixtures.DeleteUserAsync(context, c, user.Id, token)).ConfigureAwait(false);
            await StepAsync(context, result, "GET deleted user (404)", () => Helpers.ExpectErrorAsync(() => c.Admin.Users.GetAsync(tenant, user.Id, token), 404)).ConfigureAwait(false);
        }
    }
}
