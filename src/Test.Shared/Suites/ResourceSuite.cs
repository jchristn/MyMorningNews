namespace Test.Shared.Suites
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Responses;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Tenant, user, credential, and profile lifecycles over HTTP.
    /// </summary>
    public static class ResourceSuite
    {
        private const string S = "Resources";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Control-plane resource lifecycles", new[]
            {
                Cases.Case(S, "TenantLifecycle", "Tenants can be created, read, updated, and deleted", TenantLifecycleAsync),
                Cases.Case(S, "TenantValidation", "Tenant names and time zones are validated", TenantValidationAsync),
                Cases.Case(S, "UserLifecycle", "Users can be created, listed, updated, and deleted", UserLifecycleAsync),
                Cases.Case(S, "UserValidation", "Duplicate emails, short passwords, and bad bodies are rejected", UserValidationAsync),
                Cases.Case(S, "UserDeleteCascades", "Deleting a user removes their credentials", UserCascadeAsync),
                Cases.Case(S, "ProfileAndPassword", "Users update their profile and change their password", ProfileAsync),
                Cases.Case(S, "CredentialLifecycle", "Credentials can be listed, updated, and deleted", CredentialLifecycleAsync)
            });
        }

        private static async Task TenantLifecycleAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            Assert.True(t.Id.StartsWith("ten_", StringComparison.Ordinal), "tenant id prefix");
            Tenant updated = (await admin.PutAsync("/v1.0/tenants/" + t.Id, new { name = "Renamed", timeZone = "America/Los_Angeles" }, token).ConfigureAwait(false)).Expect(200, "update").Json<Tenant>();
            Assert.Equal("Renamed", updated.Name, "name updated");
            Assert.Equal("America/Los_Angeles", updated.TimeZone, "time zone updated");
            (await admin.DeleteAsync("/v1.0/tenants/" + t.Id, token).ConfigureAwait(false)).Expect(204, "delete");
            (await admin.GetAsync("/v1.0/tenants/" + t.Id, token).ConfigureAwait(false)).Expect(404, "deleted tenant");
            (await admin.DeleteAsync("/v1.0/tenants/" + t.Id, token).ConfigureAwait(false)).Expect(404, "delete again");
        }

        private static async Task TenantValidationAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            (await admin.PostAsync("/v1.0/tenants", new { description = "no name" }, token).ConfigureAwait(false)).Expect(400, "missing name");
            (await admin.PostAsync("/v1.0/tenants", new { name = "tz", timeZone = "Mars/Olympus" }, token).ConfigureAwait(false)).Expect(400, "bad time zone");
            (await admin.PostAsync("/v1.0/tenants", "{not json", token).ConfigureAwait(false)).Expect(400, "malformed JSON");
        }

        private static async Task UserLifecycleAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            UserResponse u = (await admin.PostAsync("/v1.0/tenants/" + t.Id + "/users", new { email = "  Mixed.Case@Example.TEST ", password = "abcdefgh1", firstName = "M" }, token).ConfigureAwait(false))
                .Expect(201, "create").Json<UserResponse>();
            Assert.Equal("mixed.case@example.test", u.Email, "email normalized");
            Assert.True(!(await admin.GetAsync("/v1.0/tenants/" + t.Id + "/users/" + u.Id, token).ConfigureAwait(false)).Body.Contains("password", StringComparison.OrdinalIgnoreCase), "no password material returned");
            EnumerationResult<UserResponse> list = (await admin.GetAsync("/v1.0/tenants/" + t.Id + "/users?search=mixed", token).ConfigureAwait(false)).Expect(200, "list").Json<EnumerationResult<UserResponse>>();
            Assert.Equal(1L, list.TotalRecords, "search finds the user");
            UserResponse updated = (await admin.PutAsync("/v1.0/tenants/" + t.Id + "/users/" + u.Id, new { lastName = "Last", locale = "ja" }, token).ConfigureAwait(false)).Expect(200, "update").Json<UserResponse>();
            Assert.True(updated.LastName == "Last" && updated.Locale == "ja", "fields updated");
            (await admin.DeleteAsync("/v1.0/tenants/" + t.Id + "/users/" + u.Id, token).ConfigureAwait(false)).Expect(204, "delete");
            (await admin.GetAsync("/v1.0/tenants/" + t.Id + "/users/" + u.Id, token).ConfigureAwait(false)).Expect(404, "deleted");
        }

        private static async Task UserValidationAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            (await admin.PostAsync("/v1.0/tenants/" + t.Id + "/users", new { email = "dup@example.test", password = "abcdefgh1" }, token).ConfigureAwait(false)).Expect(201, "first");
            (await admin.PostAsync("/v1.0/tenants/" + t.Id + "/users", new { email = "DUP@example.test", password = "abcdefgh1" }, token).ConfigureAwait(false)).Expect(409, "duplicate email");
            (await admin.PostAsync("/v1.0/tenants/" + t.Id + "/users", new { email = "short@example.test", password = "abc" }, token).ConfigureAwait(false)).Expect(400, "short password");
            (await admin.PostAsync("/v1.0/tenants/" + t.Id + "/users", new { email = "not-an-email", password = "abcdefgh1" }, token).ConfigureAwait(false)).Expect(400, "bad email");
            Tenant other = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            (await admin.PostAsync("/v1.0/tenants/" + other.Id + "/users", new { email = "dup@example.test", password = "abcdefgh1" }, token).ConfigureAwait(false)).Expect(201, "same email in another tenant");
        }

        private static async Task UserCascadeAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser u = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            CredentialCreatedResponse c = (await admin.PostAsync("/v1.0/tenants/" + t.Id + "/credentials", new { name = "k", userId = u.User.Id }, token).ConfigureAwait(false))
                .Expect(201, "credential for user").Json<CredentialCreatedResponse>();
            (await admin.DeleteAsync("/v1.0/tenants/" + t.Id + "/users/" + u.User.Id, token).ConfigureAwait(false)).Expect(204, "delete user");
            (await admin.GetAsync("/v1.0/tenants/" + t.Id + "/credentials/" + c.Credential.Id, token).ConfigureAwait(false)).Expect(404, "credential removed");
            (await u.Client.GetAsync("/v1.0/api/me", token).ConfigureAwait(false)).Expect(401, "session of deleted user");
        }

        private static async Task ProfileAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            TestUser u = await TestFixtures.CreateUserAsync(admin, "default", false, token).ConfigureAwait(false);
            UserResponse me = (await u.Client.PutAsync("/v1.0/api/me", new { firstName = "Self", timeZone = "Europe/Paris" }, token).ConfigureAwait(false)).Expect(200, "update profile").Json<UserResponse>();
            Assert.True(me.FirstName == "Self" && me.TimeZone == "Europe/Paris", "profile updated");
            (await u.Client.PutAsync("/v1.0/api/me/password", new { currentPassword = "wrong", newPassword = "new-password-1" }, token).ConfigureAwait(false)).Expect(400, "wrong current password");
            (await u.Client.PutAsync("/v1.0/api/me/password", new { currentPassword = u.Password, newPassword = "new-password-1" }, token).ConfigureAwait(false)).Expect(204, "change password");
            TestApiClient again = new TestApiClient(admin.BaseUrl);
            await again.LoginAsync("default", u.User.Email, "new-password-1", token).ConfigureAwait(false);
        }

        private static async Task CredentialLifecycleAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser ta = await TestFixtures.CreateUserAsync(admin, t.Id, true, token).ConfigureAwait(false);
            CredentialCreatedResponse c = (await ta.Client.PostAsync("/v1.0/tenants/" + t.Id + "/credentials", new { name = "one" }, token).ConfigureAwait(false))
                .Expect(201, "create").Json<CredentialCreatedResponse>();
            EnumerationResult<CredentialResponse> list = (await ta.Client.GetAsync("/v1.0/tenants/" + t.Id + "/credentials", token).ConfigureAwait(false)).Expect(200, "list").Json<EnumerationResult<CredentialResponse>>();
            Assert.True(list.Objects.Any(x => x.Id == c.Credential.Id), "listed");
            Assert.True(list.Objects.All(x => !String.IsNullOrEmpty(x.SecretKeyPreview)), "previews only");
            CredentialResponse upd = (await ta.Client.PutAsync("/v1.0/tenants/" + t.Id + "/credentials/" + c.Credential.Id, new { name = "renamed", active = false }, token).ConfigureAwait(false))
                .Expect(200, "update").Json<CredentialResponse>();
            Assert.True(upd.Name == "renamed" && !upd.Active, "updated");
            TestApiClient machine = new TestApiClient(admin.BaseUrl);
            machine.Headers["x-access-key"] = c.Credential.AccessKey;
            machine.Headers["x-secret-key"] = c.SecretKey;
            machine.Headers["x-tenant-guid"] = t.Id;
            (await machine.GetAsync("/v1.0/tenants/" + t.Id + "/users", token).ConfigureAwait(false)).Expect(401, "inactive credential");
            (await ta.Client.DeleteAsync("/v1.0/tenants/" + t.Id + "/credentials/" + c.Credential.Id, token).ConfigureAwait(false)).Expect(204, "delete");
        }
    }
}
