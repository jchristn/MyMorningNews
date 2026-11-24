namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Responses;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// RBAC, administrative bypasses, and tenant isolation over HTTP.
    /// </summary>
    public static class AuthorizationSuite
    {
        private const string S = "Authorization";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Authorization and tenant isolation", new[]
            {
                Cases.Case(S, "MemberCannotAdminister", "Members cannot manage users, roles, or settings and denials are audited", MemberDeniedAsync),
                Cases.Case(S, "MemberCanReadSelf", "Members can read their own profile and effective permissions", MemberSelfAsync),
                Cases.Case(S, "TenantAdminScoped", "Tenant administrators manage their own tenant only", TenantAdminAsync),
                Cases.Case(S, "CrossTenantNotFound", "Another tenant's users, roles, and credentials are not visible", CrossTenantAsync),
                Cases.Case(S, "ExplicitDenyRole", "A tenant role with an explicit deny blocks reads", ExplicitDenyAsync),
                Cases.Case(S, "AssignedRoleGrants", "Assigning SecurityAdmin grants user management", AssignedRoleAsync),
                Cases.Case(S, "BuiltInRolesImmutable", "Built-in roles cannot be edited or deleted", BuiltInImmutableAsync),
                Cases.Case(S, "ProtectedRecords", "Protected users and tenants cannot be deleted", ProtectedAsync),
                Cases.Case(S, "ReadOnlyCredential", "A read-scoped credential cannot write", ReadOnlyCredentialAsync)
            });
        }

        private static async Task MemberDeniedAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            TestUser member = await TestFixtures.CreateUserAsync(admin, "default", false, token).ConfigureAwait(false);
            AuthorizationFailureResponse denied = (await member.Client.PostAsync("/v1.0/tenants/default/users", new { email = "x@example.test", password = "abcdefgh" }, token).ConfigureAwait(false))
                .Expect(403, "member creates user").Json<AuthorizationFailureResponse>();
            Assert.Equal("Admin:Admin", denied.RequiredPermission, "required permission");
            Assert.Equal("UserWrite", denied.RequestType, "request type");
            (await member.Client.GetAsync("/v1.0/api/settings", token).ConfigureAwait(false)).Expect(403, "member reads settings");
            (await member.Client.PostAsync("/v1.0/tenants/default/roles", new { name = "x" }, token).ConfigureAwait(false)).Expect(403, "member creates role");
            EnumerationResult<AuditRecord> audit = (await admin.GetAsync("/v1.0/api/audit?eventType=AuthorizationDenied&userId=" + member.User.Id, token).ConfigureAwait(false))
                .Expect(200, "audit").Json<EnumerationResult<AuditRecord>>();
            Assert.True(audit.TotalRecords >= 3, "denials persisted (" + audit.TotalRecords + ")");
        }

        private static async Task MemberSelfAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            TestUser member = await TestFixtures.CreateUserAsync(admin, "default", false, token).ConfigureAwait(false);
            (await member.Client.GetAsync("/v1.0/tenants/default/users/" + member.User.Id, token).ConfigureAwait(false)).Expect(200, "read self");
            EffectivePermissionsResponse perms = (await member.Client.GetAsync("/v1.0/tenants/default/users/" + member.User.Id + "/permissions", token).ConfigureAwait(false))
                .Expect(200, "own permissions").Json<EffectivePermissionsResponse>();
            Assert.True(perms.Checks.Any(c => c.ResourceType == ResourceTypeEnum.Topic && c.Operation == OperationTypeEnum.Create && c.Permitted), "member creates topics");
            Assert.True(perms.Checks.Any(c => c.ResourceType == ResourceTypeEnum.Admin && c.Operation == OperationTypeEnum.Admin && !c.Permitted), "member is not admin");
        }

        private static async Task TenantAdminAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant tenant = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser ta = await TestFixtures.CreateUserAsync(admin, tenant.Id, true, token).ConfigureAwait(false);
            (await ta.Client.PostAsync("/v1.0/tenants/" + tenant.Id + "/users", new { email = "new-" + Guid.NewGuid().ToString("N").Substring(0, 6) + "@example.test", password = "abcdefgh1" }, token).ConfigureAwait(false))
                .Expect(201, "tenant admin creates user in own tenant");
            (await ta.Client.GetAsync("/v1.0/tenants/default/users", token).ConfigureAwait(false)).Expect(404, "tenant admin reads another tenant");
            (await ta.Client.PostAsync("/v1.0/tenants", new { name = "nope" }, token).ConfigureAwait(false)).Expect(403, "tenant admin creates tenant");
            (await ta.Client.PostAsync("/v1.0/tenants/" + tenant.Id + "/users", new { email = "ga-" + Guid.NewGuid().ToString("N").Substring(0, 6) + "@example.test", password = "abcdefgh1", isAdmin = true }, token).ConfigureAwait(false))
                .Expect(400, "tenant admin cannot create global admins");
        }

        private static async Task CrossTenantAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant a = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            Tenant b = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser adminA = await TestFixtures.CreateUserAsync(admin, a.Id, true, token).ConfigureAwait(false);
            TestUser userB = await TestFixtures.CreateUserAsync(admin, b.Id, false, token).ConfigureAwait(false);
            foreach (string path in new[] { "/v1.0/tenants/" + b.Id, "/v1.0/tenants/" + b.Id + "/users", "/v1.0/tenants/" + b.Id + "/users/" + userB.User.Id, "/v1.0/tenants/" + b.Id + "/roles", "/v1.0/tenants/" + b.Id + "/credentials" })
                (await adminA.Client.GetAsync(path, token).ConfigureAwait(false)).Expect(404, "cross-tenant " + path);
            (await adminA.Client.DeleteAsync("/v1.0/tenants/" + b.Id + "/users/" + userB.User.Id, token).ConfigureAwait(false)).Expect(404, "cross-tenant delete");
            (await adminA.Client.GetAsync("/v1.0/tenants/" + a.Id + "/users/" + userB.User.Id, token).ConfigureAwait(false)).Expect(404, "foreign user id in own tenant path");
        }

        private static async Task ExplicitDenyAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser member = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            (await member.Client.GetAsync("/v1.0/tenants/" + t.Id + "/users", token).ConfigureAwait(false)).Expect(200, "member reads users before deny");
            Permission deny = (await admin.PostAsync("/v1.0/tenants/" + t.Id + "/permissions", new { name = "deny users", resourceTypes = new[] { "User" }, operationTypes = new[] { "Read" }, permissionType = "Deny" }, token).ConfigureAwait(false))
                .Expect(201, "create deny").Json<Permission>();
            Permission allow = (await admin.PostAsync("/v1.0/tenants/" + t.Id + "/permissions", new { name = "allow all", resourceTypes = new[] { "All" }, operationTypes = new[] { "Read" } }, token).ConfigureAwait(false))
                .Expect(201, "create permit").Json<Permission>();
            UserRole role = (await admin.PostAsync("/v1.0/tenants/" + t.Id + "/roles", new { name = "NoUsers", permissionIds = new[] { deny.Id, allow.Id } }, token).ConfigureAwait(false))
                .Expect(201, "create role").Json<UserRole>();
            (await admin.PostAsync("/v1.0/tenants/" + t.Id + "/users/" + member.User.Id + "/assignments", new { roleId = role.Id }, token).ConfigureAwait(false)).Expect(201, "assign");
            (await member.Client.GetAsync("/v1.0/tenants/" + t.Id + "/users", token).ConfigureAwait(false)).Expect(403, "deny wins");
            (await member.Client.GetAsync("/v1.0/tenants/" + t.Id + "/roles", token).ConfigureAwait(false)).Expect(200, "permit applies elsewhere");
        }

        private static async Task AssignedRoleAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser member = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            object body = new { email = "sa-" + Guid.NewGuid().ToString("N").Substring(0, 6) + "@example.test", password = "abcdefgh1" };
            (await member.Client.PostAsync("/v1.0/tenants/" + t.Id + "/users", body, token).ConfigureAwait(false)).Expect(403, "before assignment");
            (await admin.PostAsync("/v1.0/tenants/" + t.Id + "/users/" + member.User.Id + "/assignments", new { roleName = "SecurityAdmin" }, token).ConfigureAwait(false)).Expect(201, "assign SecurityAdmin");
            (await member.Client.PostAsync("/v1.0/tenants/" + t.Id + "/users", body, token).ConfigureAwait(false)).Expect(201, "after assignment");
        }

        private static async Task BuiltInImmutableAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            EnumerationResult<UserRole> roles = (await admin.GetAsync("/v1.0/tenants/default/roles?isBuiltIn=true&pageSize=50", token).ConfigureAwait(false)).Expect(200, "roles").Json<EnumerationResult<UserRole>>();
            UserRole tenantAdmin = roles.Objects.First(r => r.Name == "TenantAdmin");
            (await admin.PutAsync("/v1.0/tenants/default/roles/" + tenantAdmin.Id, new { description = "changed" }, token).ConfigureAwait(false)).Expect(409, "edit built-in");
            (await admin.DeleteAsync("/v1.0/tenants/default/roles/" + tenantAdmin.Id, token).ConfigureAwait(false)).Expect(409, "delete built-in");
            (await admin.PostAsync("/v1.0/tenants/default/roles", new { name = "TenantAdmin" }, token).ConfigureAwait(false)).Expect(409, "reserved name");
        }

        private static async Task ProtectedAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            (await admin.DeleteAsync("/v1.0/tenants/default", token).ConfigureAwait(false)).Expect(409, "protected tenant");
            UserResponse me = (await admin.GetAsync("/v1.0/api/me", token).ConfigureAwait(false)).Json<UserResponse>();
            (await admin.DeleteAsync("/v1.0/tenants/default/users/" + me.Id, token).ConfigureAwait(false)).Expect(409, "protected admin");
        }

        private static async Task ReadOnlyCredentialAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser ta = await TestFixtures.CreateUserAsync(admin, t.Id, true, token).ConfigureAwait(false);
            CredentialCreatedResponse c = (await ta.Client.PostAsync("/v1.0/tenants/" + t.Id + "/credentials", new { name = "reader", scopes = new[] { "read" } }, token).ConfigureAwait(false))
                .Expect(201, "create read credential").Json<CredentialCreatedResponse>();
            TestApiClient machine = new TestApiClient(admin.BaseUrl);
            machine.Headers["x-access-key"] = c.Credential.AccessKey;
            machine.Headers["x-secret-key"] = c.SecretKey;
            machine.Headers["x-tenant-guid"] = t.Id;
            (await machine.GetAsync("/v1.0/tenants/" + t.Id + "/users", token).ConfigureAwait(false)).Expect(200, "read with read scope");
            (await machine.PostAsync("/v1.0/tenants/" + t.Id + "/users", new { email = "w@example.test", password = "abcdefgh1" }, token).ConfigureAwait(false)).Expect(403, "write with read scope");
        }
    }
}
