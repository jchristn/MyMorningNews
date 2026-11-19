namespace Test.Shared.Suites
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Responses;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Session tokens and authentication schemes over HTTP.
    /// </summary>
    public static class AuthenticationSuite
    {
        private const string S = "Authentication";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Authentication and sessions", new[]
            {
                Cases.Case(S, "DefaultAdminLogin", "The default administrator can sign in", DefaultLoginAsync, "smoke"),
                Cases.Case(S, "BadPasswordRejected", "Wrong passwords and unknown tenants get 401", BadPasswordAsync),
                Cases.Case(S, "TenantRequired", "Login without x-tenant-guid gets 400", TenantRequiredAsync),
                Cases.Case(S, "NoCredentials401", "Protected routes require authentication", NoCredentialsAsync, "smoke"),
                Cases.Case(S, "TokenDetails", "Token details describe the principal", DetailsAsync),
                Cases.Case(S, "LogoutRevokes", "Logout revokes the session", LogoutAsync),
                Cases.Case(S, "RefreshRotates", "Refresh issues a new token and revokes the old one", RefreshAsync),
                Cases.Case(S, "ConflictingCarriers", "Different tokens in Authorization and x-token are rejected", ConflictingAsync),
                Cases.Case(S, "MultipleSchemes", "Two schemes on one request are rejected", MultipleSchemesAsync),
                Cases.Case(S, "MalformedToken", "Malformed tokens get 401", MalformedAsync),
                Cases.Case(S, "PasswordHeadersOnlyOnLogin", "Login headers are rejected on ordinary routes", PasswordHeadersAsync),
                Cases.Case(S, "InactiveUserRejected", "A deactivated user's session stops working", InactiveAsync),
                Cases.Case(S, "AdminApiKey", "The administration key authenticates and wrong keys fail", AdminKeyAsync),
                Cases.Case(S, "CredentialAuth", "Access key and secret authenticate; a wrong secret fails", CredentialAsync)
            });
        }

        private static async Task DefaultLoginAsync(CancellationToken token)
        {
            TestApiClient client = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            UserResponse me = (await client.GetAsync("/v1.0/api/me", token).ConfigureAwait(false)).Expect(200, "me").Json<UserResponse>();
            Assert.Equal(SharedTestServer.AdminEmail, me.Email, "email");
            Assert.True(me.IsAdmin && me.IsTenantAdmin, "admin flags");
        }

        private static async Task BadPasswordAsync(CancellationToken token)
        {
            TestApiClient anon = await SharedTestServer.AnonymousClientAsync(token).ConfigureAwait(false);
            anon.Headers["x-email"] = SharedTestServer.AdminEmail;
            anon.Headers["x-password"] = "wrong-password";
            anon.Headers["x-tenant-guid"] = "default";
            (await anon.PostAsync("/v1.0/token", null, token).ConfigureAwait(false)).Expect(401, "wrong password");
            anon.Headers["x-password"] = SharedTestServer.AdminPassword;
            anon.Headers["x-tenant-guid"] = "ten_does_not_exist";
            (await anon.PostAsync("/v1.0/token", null, token).ConfigureAwait(false)).Expect(401, "unknown tenant");
        }

        private static async Task TenantRequiredAsync(CancellationToken token)
        {
            TestApiClient anon = await SharedTestServer.AnonymousClientAsync(token).ConfigureAwait(false);
            anon.Headers["x-email"] = SharedTestServer.AdminEmail;
            anon.Headers["x-password"] = SharedTestServer.AdminPassword;
            (await anon.PostAsync("/v1.0/token", null, token).ConfigureAwait(false)).Expect(400, "missing tenant");
        }

        private static async Task NoCredentialsAsync(CancellationToken token)
        {
            TestApiClient anon = await SharedTestServer.AnonymousClientAsync(token).ConfigureAwait(false);
            (await anon.GetAsync("/v1.0/tenants", token).ConfigureAwait(false)).Expect(401, "tenants without auth");
            (await anon.GetAsync("/v1.0/api/settings", token).ConfigureAwait(false)).Expect(401, "settings without auth");
        }

        private static async Task DetailsAsync(CancellationToken token)
        {
            TestApiClient client = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            TokenDetailsResponse d = (await client.GetAsync("/v1.0/token/details", token).ConfigureAwait(false)).Expect(200, "details").Json<TokenDetailsResponse>();
            Assert.True(d.Valid && d.TenantId == "default" && d.IsAdmin && d.ExpiresUtc > DateTime.UtcNow, "details content");
        }

        private static async Task LogoutAsync(CancellationToken token)
        {
            TestApiClient client = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            (await client.DeleteAsync("/v1.0/token", token).ConfigureAwait(false)).Expect(204, "logout");
            (await client.GetAsync("/v1.0/token", token).ConfigureAwait(false)).Expect(401, "revoked token");
        }

        private static async Task RefreshAsync(CancellationToken token)
        {
            TestApiClient client = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            string old = client.Token!;
            TokenResponse fresh = (await client.PostAsync("/v1.0/token/refresh", null, token).ConfigureAwait(false)).Expect(200, "refresh").Json<TokenResponse>();
            Assert.True(fresh.Token != old, "new token");
            (await client.GetAsync("/v1.0/token", token).ConfigureAwait(false)).Expect(401, "old token revoked");
            client.Token = fresh.Token;
            (await client.GetAsync("/v1.0/token", token).ConfigureAwait(false)).Expect(200, "new token works");
        }

        private static async Task ConflictingAsync(CancellationToken token)
        {
            TestApiClient a = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            TestApiClient b = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            a.Headers["x-token"] = b.Token!;
            (await a.GetAsync("/v1.0/token", token).ConfigureAwait(false)).Expect(401, "conflicting tokens");
            a.Headers["x-token"] = a.Token!;
            (await a.GetAsync("/v1.0/token", token).ConfigureAwait(false)).Expect(200, "matching tokens");
        }

        private static async Task MultipleSchemesAsync(CancellationToken token)
        {
            TestApiClient a = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            a.Headers["x-api-key"] = SharedTestServer.AdminApiKey;
            (await a.GetAsync("/v1.0/tenants", token).ConfigureAwait(false)).Expect(401, "two schemes");
        }

        private static async Task MalformedAsync(CancellationToken token)
        {
            TestApiClient anon = await SharedTestServer.AnonymousClientAsync(token).ConfigureAwait(false);
            anon.Token = "not-a-real-token";
            (await anon.GetAsync("/v1.0/token", token).ConfigureAwait(false)).Expect(401, "malformed token");
        }

        private static async Task PasswordHeadersAsync(CancellationToken token)
        {
            TestApiClient anon = await SharedTestServer.AnonymousClientAsync(token).ConfigureAwait(false);
            anon.Headers["x-email"] = SharedTestServer.AdminEmail;
            anon.Headers["x-password"] = SharedTestServer.AdminPassword;
            anon.Headers["x-tenant-guid"] = "default";
            (await anon.GetAsync("/v1.0/tenants", token).ConfigureAwait(false)).Expect(401, "login headers on data route");
        }

        private static async Task InactiveAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            TestUser user = await TestFixtures.CreateUserAsync(admin, "default", false, token).ConfigureAwait(false);
            (await user.Client.GetAsync("/v1.0/api/me", token).ConfigureAwait(false)).Expect(200, "active user");
            (await admin.PutAsync("/v1.0/tenants/default/users/" + user.User.Id, new { active = false }, token).ConfigureAwait(false)).Expect(200, "deactivate");
            (await user.Client.GetAsync("/v1.0/api/me", token).ConfigureAwait(false)).Expect(401, "inactive user");
        }

        private static async Task AdminKeyAsync(CancellationToken token)
        {
            TestApiClient anon = await SharedTestServer.AnonymousClientAsync(token).ConfigureAwait(false);
            anon.Headers["x-api-key"] = SharedTestServer.AdminApiKey;
            (await anon.GetAsync("/v1.0/tenants", token).ConfigureAwait(false)).Expect(200, "admin key");
            anon.Headers["x-api-key"] = "wrong";
            (await anon.GetAsync("/v1.0/tenants", token).ConfigureAwait(false)).Expect(401, "wrong admin key");
        }

        private static async Task CredentialAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            CredentialCreatedResponse created = (await admin.PostAsync("/v1.0/tenants/default/credentials", new { name = "suite-key" }, token).ConfigureAwait(false))
                .Expect(201, "create credential").Json<CredentialCreatedResponse>();
            Assert.True(created.SecretKey.StartsWith("secret_", StringComparison.Ordinal), "secret returned once");
            TestApiClient machine = await SharedTestServer.AnonymousClientAsync(token).ConfigureAwait(false);
            machine.Headers["x-access-key"] = created.Credential.AccessKey;
            machine.Headers["x-secret-key"] = created.SecretKey;
            machine.Headers["x-tenant-guid"] = "default";
            (await machine.GetAsync("/v1.0/tenants/default/users", token).ConfigureAwait(false)).Expect(200, "credential auth");
            machine.Headers["x-secret-key"] = "secret_wrong";
            (await machine.GetAsync("/v1.0/tenants/default/users", token).ConfigureAwait(false)).Expect(401, "wrong secret");
            CredentialResponse read = (await admin.GetAsync("/v1.0/tenants/default/credentials/" + created.Credential.Id, token).ConfigureAwait(false)).Expect(200, "read credential").Json<CredentialResponse>();
            Assert.True(!read.SecretKeyPreview.Contains(created.SecretKey, StringComparison.Ordinal) && read.SecretKeyPreview.EndsWith(created.SecretKey.Substring(created.SecretKey.Length - 4), StringComparison.Ordinal), "only last four shown");
        }
    }
}
