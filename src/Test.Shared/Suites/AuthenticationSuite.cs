namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;
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
                Cases.Case(S, "TenantRequired", "Access-key login without x-tenant-guid gets 400; password login resolves the tenant", TenantRequiredAsync),
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
                Cases.Case(S, "CredentialAuth", "Access key and secret authenticate; a wrong secret fails", CredentialAsync),
                Cases.Case(S, "SignedRequests", "Signed requests authenticate without sending the secret; tampering, replay, and stale dates fail; auth modes are enforced", SignedAsync)
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
            anon.Headers["x-access-key"] = "ak-does-not-matter";
            anon.Headers["x-secret-key"] = "sk-does-not-matter";
            (await anon.PostAsync("/v1.0/token", null, token).ConfigureAwait(false)).Expect(400, "access key needs a tenant");
            TestApiClient user = await SharedTestServer.AnonymousClientAsync(token).ConfigureAwait(false);
            user.Headers["x-email"] = SharedTestServer.AdminEmail;
            user.Headers["x-password"] = SharedTestServer.AdminPassword;
            TokenResponse resolved = (await user.PostAsync("/v1.0/token", null, token).ConfigureAwait(false)).Expect(200, "tenant resolved").Json<TokenResponse>();
            Assert.Equal("default", resolved.TenantId, "default tenant");
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

        private static async Task SignedAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            CredentialCreatedResponse created = (await admin.PostAsync("/v1.0/tenants/default/credentials", new { name = "suite-signed" }, token).ConfigureAwait(false))
                .Expect(201, "create credential").Json<CredentialCreatedResponse>();
            TestApiClient anon = await SharedTestServer.AnonymousClientAsync(token).ConfigureAwait(false);
            string key = created.Credential.AccessKey;
            string secret = created.SecretKey;

            Dictionary<string, string> Sign(string method, string path, string body, DateTime when, string nonce, string? secretOverride = null)
            {
                string date = when.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
                string toSign = RequestSigner.StringToSign(method, path, "default", date, nonce, RequestSigner.BodyHash(Encoding.UTF8.GetBytes(body)));
                return new Dictionary<string, string>
                {
                    { "x-access-key", key }, { "x-tenant-guid", "default" }, { "x-date", date }, { "x-nonce", nonce }, { "x-signature", RequestSigner.Sign(secretOverride ?? secret, toSign) }
                };
            }

            string path = "/v1.0/tenants/default/users?pageSize=2";
            Dictionary<string, string> good = Sign("GET", path, String.Empty, DateTime.UtcNow, "nonce-" + Guid.NewGuid().ToString("N"));
            (await anon.SendAsync(HttpMethod.Get, path, null, good, token).ConfigureAwait(false)).Expect(200, "signed GET");
            (await anon.SendAsync(HttpMethod.Get, path, null, good, token).ConfigureAwait(false)).Expect(401, "replayed nonce");
            (await anon.SendAsync(HttpMethod.Get, "/v1.0/tenants/default/users?pageSize=3", null, Sign("GET", path, String.Empty, DateTime.UtcNow, "nonce-" + Guid.NewGuid().ToString("N")), token).ConfigureAwait(false))
                .Expect(401, "query changed after signing");
            (await anon.SendAsync(HttpMethod.Get, path, null, Sign("GET", path, String.Empty, DateTime.UtcNow.AddMinutes(-10), "nonce-" + Guid.NewGuid().ToString("N")), token).ConfigureAwait(false))
                .Expect(401, "stale date");
            (await anon.SendAsync(HttpMethod.Get, path, null, Sign("GET", path, String.Empty, DateTime.UtcNow, "nonce-" + Guid.NewGuid().ToString("N"), "secret_wrong"), token).ConfigureAwait(false))
                .Expect(401, "wrong secret");
            Dictionary<string, string> both = Sign("GET", path, String.Empty, DateTime.UtcNow, "nonce-" + Guid.NewGuid().ToString("N"));
            both["x-secret-key"] = secret;
            (await anon.SendAsync(HttpMethod.Get, path, null, both, token).ConfigureAwait(false)).Expect(401, "signature and secret together");

            string body = "{\"name\":\"suite-signed-renamed\"}";
            string put = "/v1.0/tenants/default/credentials/" + created.Credential.Id;
            (await anon.SendAsync(HttpMethod.Put, put, body, Sign("PUT", put, body, DateTime.UtcNow, "nonce-" + Guid.NewGuid().ToString("N")), token).ConfigureAwait(false)).Expect(200, "signed PUT with body");
            (await anon.SendAsync(HttpMethod.Put, put, "{\"name\":\"tampered\"}", Sign("PUT", put, body, DateTime.UtcNow, "nonce-" + Guid.NewGuid().ToString("N")), token).ConfigureAwait(false))
                .Expect(401, "body changed after signing");
            TokenDetailsResponse details = (await anon.SendAsync(HttpMethod.Get, "/v1.0/token/details", null, Sign("GET", "/v1.0/token/details", String.Empty, DateTime.UtcNow, "nonce-" + Guid.NewGuid().ToString("N")), token).ConfigureAwait(false))
                .Expect(200, "details").Json<TokenDetailsResponse>();
            Assert.Equal("AccessKeySignature", details.Scheme?.ToString(), "scheme reported");

            CredentialCreatedResponse signedOnly = (await admin.PostAsync("/v1.0/tenants/default/credentials", new { name = "suite-signed-only", authMode = "SignedRequest" }, token).ConfigureAwait(false))
                .Expect(201, "signed-only credential").Json<CredentialCreatedResponse>();
            key = signedOnly.Credential.AccessKey;
            secret = signedOnly.SecretKey;
            (await anon.SendAsync(HttpMethod.Get, path, null, Sign("GET", path, String.Empty, DateTime.UtcNow, "nonce-" + Guid.NewGuid().ToString("N")), token).ConfigureAwait(false)).Expect(200, "signed-only credential signs");
            Dictionary<string, string> direct = new Dictionary<string, string> { { "x-access-key", key }, { "x-secret-key", secret }, { "x-tenant-guid", "default" } };
            (await anon.SendAsync(HttpMethod.Get, path, null, direct, token).ConfigureAwait(false)).Expect(401, "signed-only credential refuses the secret on the wire");
            (await anon.SendAsync(HttpMethod.Post, "/v1.0/token", null, direct, token).ConfigureAwait(false)).Expect(401, "signed-only credential cannot be exchanged");

            CredentialCreatedResponse exchangeOnly = (await admin.PostAsync("/v1.0/tenants/default/credentials", new { name = "suite-exchange-only", authMode = "SessionExchange" }, token).ConfigureAwait(false))
                .Expect(201, "exchange-only credential").Json<CredentialCreatedResponse>();
            key = exchangeOnly.Credential.AccessKey;
            secret = exchangeOnly.SecretKey;
            direct = new Dictionary<string, string> { { "x-access-key", key }, { "x-secret-key", secret }, { "x-tenant-guid", "default" } };
            (await anon.SendAsync(HttpMethod.Post, "/v1.0/token", null, direct, token).ConfigureAwait(false)).Expect(200, "exchange-only credential gets a session");
            (await anon.SendAsync(HttpMethod.Get, path, null, direct, token).ConfigureAwait(false)).Expect(401, "exchange-only credential refuses per-request secrets");
            (await anon.SendAsync(HttpMethod.Get, path, null, Sign("GET", path, String.Empty, DateTime.UtcNow, "nonce-" + Guid.NewGuid().ToString("N")), token).ConfigureAwait(false)).Expect(401, "exchange-only credential refuses signatures");
        }
    }
}
