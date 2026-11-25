namespace Test.Shared.Helpers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;

    /// <summary>
    /// Builders for per-case tenants and users through the API.
    /// </summary>
    public static class TestFixtures
    {
        /// <summary>
        /// Create a tenant as the default administrator.
        /// </summary>
        /// <param name="admin">Administrator client.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tenant.</returns>
        public static async Task<Tenant> CreateTenantAsync(TestApiClient admin, CancellationToken token)
        {
            TestResponse r = await admin.PostAsync("/v1.0/tenants", new TenantUpsertRequest { Name = "Test " + Guid.NewGuid().ToString("N").Substring(0, 8) }, token).ConfigureAwait(false);
            return r.Expect(201, "create tenant").Json<Tenant>();
        }

        /// <summary>
        /// Create a user in a tenant and return a client logged in as that user.
        /// </summary>
        /// <param name="admin">Administrator client.</param>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="tenantAdmin">Whether the user is a tenant administrator.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Logged-in client and the created user.</returns>
        public static async Task<TestUser> CreateUserAsync(TestApiClient admin, string tenantId, bool tenantAdmin, CancellationToken token)
        {
            string email = "user-" + Guid.NewGuid().ToString("N").Substring(0, 10) + "@example.test";
            string password = "test-password-1";
            TestResponse r = await admin.PostAsync("/v1.0/tenants/" + tenantId + "/users", new UserCreateRequest { Email = email, Password = password, IsTenantAdmin = tenantAdmin, SendWelcomeEmail = false }, token).ConfigureAwait(false);
            UserResponse user = r.Expect(201, "create user").Json<UserResponse>();
            TestApiClient client = new TestApiClient(admin.BaseUrl);
            await client.LoginAsync(tenantId, email, password, token).ConfigureAwait(false);
            return new TestUser(user, client, password);
        }
    }
}
