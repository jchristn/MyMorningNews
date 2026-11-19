namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Server;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Backend contracts for the mobile app: tenant-less sign-in, rotating refresh tokens, push devices, and client config.
    /// </summary>
    public static class MobileSuite
    {
        private const string S = "Mobile";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Mobile backend contracts", new[]
            {
                Cases.Case(S, "SignIn.TenantLess", "Sign-in without a tenant resolves one tenant and lists several", TenantLessAsync),
                Cases.Case(S, "Refresh.Rotation", "Mobile refresh tokens rotate, and reuse revokes the sign-in", RefreshRotationAsync),
                Cases.Case(S, "Refresh.Logout", "Logout revokes the refresh token", RefreshLogoutAsync),
                Cases.Case(S, "Devices.Lifecycle", "Devices register idempotently, are private to their owner, and are removed", DevicesAsync),
                Cases.Case(S, "Devices.Push", "Push sends through Expo and prunes unregistered devices", PushAsync),
                Cases.Case(S, "ClientConfig", "Client config is public and carries links and the copyright", ClientConfigAsync, "fast")
            });
        }

        private static TestApiClient Anonymous(string baseUrl, string email, string password, string? tenantId, bool mobile)
        {
            TestApiClient c = new TestApiClient(baseUrl);
            c.Headers["x-email"] = email;
            c.Headers["x-password"] = password;
            if (tenantId != null) c.Headers["x-tenant-guid"] = tenantId;
            if (mobile) c.Headers["x-client"] = "mobile";
            return c;
        }

        private static async Task<string> CreateUserWithAsync(TestApiClient admin, string tenantId, string email, string password, CancellationToken token)
        {
            return (await admin.PostAsync("/v1.0/tenants/" + tenantId + "/users", new UserCreateRequest { Email = email, Password = password }, token).ConfigureAwait(false))
                .Expect(201, "create user").Json<UserResponse>().Id;
        }

        private static async Task TenantLessAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant a = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            Tenant b = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            string single = "solo-" + Guid.NewGuid().ToString("N").Substring(0, 8) + "@example.test";
            string multi = "multi-" + Guid.NewGuid().ToString("N").Substring(0, 8) + "@example.test";
            await CreateUserWithAsync(admin, a.Id, single, "pw-single-1", token).ConfigureAwait(false);
            await CreateUserWithAsync(admin, a.Id, multi, "pw-multi-1", token).ConfigureAwait(false);
            await CreateUserWithAsync(admin, b.Id, multi, "pw-multi-1", token).ConfigureAwait(false);

            TokenResponse one = (await Anonymous(admin.BaseUrl, single.ToUpperInvariant(), "pw-single-1", null, false).PostAsync("/v1.0/token", null, token).ConfigureAwait(false))
                .Expect(200, "single tenant").Json<TokenResponse>();
            Assert.Equal(a.Id, one.TenantId, "resolved tenant");
            Assert.True(one.RefreshToken == null, "no refresh token for web clients");

            TenantSelectionResponse choose = (await Anonymous(admin.BaseUrl, multi, "pw-multi-1", null, false).PostAsync("/v1.0/token", null, token).ConfigureAwait(false))
                .Expect(409, "several tenants").Json<TenantSelectionResponse>();
            Assert.True(choose.Error == "TenantSelectionRequired" && choose.Tenants.Select(t => t.Id).OrderBy(x => x).SequenceEqual(new[] { a.Id, b.Id }.OrderBy(x => x)), "tenant list");
            (await Anonymous(admin.BaseUrl, multi, "pw-multi-1", b.Id, false).PostAsync("/v1.0/token", null, token).ConfigureAwait(false)).Expect(200, "explicit tenant");
            (await Anonymous(admin.BaseUrl, multi, "wrong-password", null, false).PostAsync("/v1.0/token", null, token).ConfigureAwait(false)).Expect(401, "wrong password reveals nothing");
            (await Anonymous(admin.BaseUrl, "nobody-" + Guid.NewGuid().ToString("N") + "@example.test", "x", null, false).PostAsync("/v1.0/token", null, token).ConfigureAwait(false)).Expect(401, "unknown email");
        }

        private static async Task RefreshRotationAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            string email = "mobile-" + Guid.NewGuid().ToString("N").Substring(0, 8) + "@example.test";
            await CreateUserWithAsync(admin, t.Id, email, "pw-mobile-1", token).ConfigureAwait(false);
            TokenResponse first = (await Anonymous(admin.BaseUrl, email, "pw-mobile-1", null, true).PostAsync("/v1.0/token", null, token).ConfigureAwait(false))
                .Expect(200, "mobile sign-in").Json<TokenResponse>();
            Assert.True(!String.IsNullOrEmpty(first.RefreshToken) && first.RefreshExpiresUtc.HasValue && first.RefreshExpiresUtc.Value > DateTime.UtcNow.AddDays(29), "refresh token issued for 30 days");

            TestApiClient anon = new TestApiClient(admin.BaseUrl);
            TokenResponse second = (await anon.PostAsync("/v1.0/token/refresh-token", new RefreshTokenRequest { RefreshToken = first.RefreshToken! }, token).ConfigureAwait(false))
                .Expect(200, "refresh").Json<TokenResponse>();
            Assert.True(second.Token != first.Token && second.RefreshToken != first.RefreshToken && second.SessionId != first.SessionId, "rotated");

            TestApiClient oldClient = new TestApiClient(admin.BaseUrl) { Token = first.Token };
            (await oldClient.GetAsync("/v1.0/token", token).ConfigureAwait(false)).Expect(401, "old session revoked");
            TestApiClient newClient = new TestApiClient(admin.BaseUrl) { Token = second.Token };
            (await newClient.GetAsync("/v1.0/api/me", token).ConfigureAwait(false)).Expect(200, "new session works");

            (await anon.PostAsync("/v1.0/token/refresh-token", new RefreshTokenRequest { RefreshToken = first.RefreshToken! }, token).ConfigureAwait(false)).Expect(401, "reuse rejected");
            (await anon.PostAsync("/v1.0/token/refresh-token", new RefreshTokenRequest { RefreshToken = second.RefreshToken! }, token).ConfigureAwait(false)).Expect(401, "family revoked after reuse");
            (await newClient.GetAsync("/v1.0/api/me", token).ConfigureAwait(false)).Expect(401, "sessions revoked after reuse");
            (await anon.PostAsync("/v1.0/token/refresh-token", new RefreshTokenRequest { RefreshToken = "not-a-token" }, token).ConfigureAwait(false)).Expect(401, "unknown token");
        }

        private static async Task RefreshLogoutAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            string email = "logout-" + Guid.NewGuid().ToString("N").Substring(0, 8) + "@example.test";
            await CreateUserWithAsync(admin, t.Id, email, "pw-logout-1", token).ConfigureAwait(false);
            TokenResponse signedIn = (await Anonymous(admin.BaseUrl, email, "pw-logout-1", t.Id, true).PostAsync("/v1.0/token", null, token).ConfigureAwait(false)).Expect(200, "sign-in").Json<TokenResponse>();
            TestApiClient c = new TestApiClient(admin.BaseUrl) { Token = signedIn.Token };
            (await c.DeleteAsync("/v1.0/token", token).ConfigureAwait(false)).Expect(204, "logout");
            (await new TestApiClient(admin.BaseUrl).PostAsync("/v1.0/token/refresh-token", new RefreshTokenRequest { RefreshToken = signedIn.RefreshToken! }, token).ConfigureAwait(false)).Expect(401, "refresh after logout");
        }

        private static async Task DevicesAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser owner = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            TestUser other = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            string push = "ExponentPushToken[" + Guid.NewGuid().ToString("N").Substring(0, 16) + "]";
            Device created = (await owner.Client.PostAsync("/v1.0/api/devices", new DeviceRegisterRequest { ExpoPushToken = push, Platform = "iOS", Name = "Test iPhone", Locale = "en" }, token).ConfigureAwait(false))
                .Expect(201, "register").Json<Device>();
            Assert.True(created.Id.StartsWith("dev_", StringComparison.Ordinal) && created.Platform == "ios", "device created");
            Device again = (await owner.Client.PostAsync("/v1.0/api/devices", new DeviceRegisterRequest { ExpoPushToken = push, Platform = "ios", Name = "Renamed" }, token).ConfigureAwait(false))
                .Expect(200, "re-register").Json<Device>();
            Assert.True(again.Id == created.Id && again.Name == "Renamed", "idempotent");
            (await owner.Client.PostAsync("/v1.0/api/devices", new DeviceRegisterRequest { ExpoPushToken = "garbage", Platform = "ios" }, token).ConfigureAwait(false)).Expect(400, "bad token");
            (await owner.Client.PostAsync("/v1.0/api/devices", new DeviceRegisterRequest { ExpoPushToken = push, Platform = "windows" }, token).ConfigureAwait(false)).Expect(400, "bad platform");
            List<Device> mine = (await owner.Client.GetAsync("/v1.0/api/devices", token).ConfigureAwait(false)).Expect(200, "list").Json<List<Device>>();
            Assert.Equal(1, mine.Count, "one device");
            List<Device> theirs = (await other.Client.GetAsync("/v1.0/api/devices", token).ConfigureAwait(false)).Expect(200, "other list").Json<List<Device>>();
            Assert.Equal(0, theirs.Count, "devices are private");
            (await other.Client.DeleteAsync("/v1.0/api/devices/" + created.Id, token).ConfigureAwait(false)).Expect(404, "cannot delete another user's device");
            (await owner.Client.DeleteAsync("/v1.0/api/devices/" + created.Id, token).ConfigureAwait(false)).Expect(204, "delete");
        }

        private static async Task PushAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            server.Context.SettingsService.Current.Push.ExpoPushUrl = StubModelServer.Shared.BaseUrl + "/push/send";
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser user = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            Device live = (await user.Client.PostAsync("/v1.0/api/devices", new DeviceRegisterRequest { ExpoPushToken = "ExponentPushToken[live-" + Guid.NewGuid().ToString("N").Substring(0, 8) + "]", Platform = "android" }, token).ConfigureAwait(false))
                .Expect(201, "live device").Json<Device>();
            await user.Client.PostAsync("/v1.0/api/devices", new DeviceRegisterRequest { ExpoPushToken = "ExponentPushToken[dead-" + Guid.NewGuid().ToString("N").Substring(0, 8) + "]", Platform = "ios" }, token).ConfigureAwait(false);

            int before = StubModelServer.Shared.PushRequests.Count;
            PushResult test = (await user.Client.PostAsync("/v1.0/api/devices/" + live.Id + "/test", null, token).ConfigureAwait(false)).Expect(200, "test push").Json<PushResult>();
            Assert.True(test.Accepted == 1 && test.Failed == 0, "test accepted");
            string sent = StubModelServer.Shared.PushRequests.Skip(before).First();
            Assert.True(sent.Contains("\"kind\":\"test\"", StringComparison.Ordinal) && sent.Contains("\"channelId\":\"account\"", StringComparison.Ordinal), "payload carries ids, not content");

            PushResult all = await server.Context.Push.SendToUserAsync(t.Id, user.User.Id, new PushMessage { Title = "Ready", Body = "Your briefing is ready.", ChannelId = "briefings" }, token).ConfigureAwait(false);
            Assert.True(all.Accepted == 1 && all.Removed == 1, "dead device pruned (accepted " + all.Accepted + ", removed " + all.Removed + ")");
            List<Device> remaining = (await user.Client.GetAsync("/v1.0/api/devices", token).ConfigureAwait(false)).Json<List<Device>>();
            Assert.True(remaining.Count == 1 && remaining[0].Id == live.Id, "only the live device remains");

            server.Context.SettingsService.Current.Push.Enabled = false;
            PushResult off = await server.Context.Push.SendToUserAsync(t.Id, user.User.Id, new PushMessage { Title = "x", Body = "y" }, token).ConfigureAwait(false);
            server.Context.SettingsService.Current.Push.Enabled = true;
            Assert.Equal(0, off.Accepted, "disabled push sends nothing");
        }

        private static async Task ClientConfigAsync(CancellationToken token)
        {
            TestApiClient anon = await SharedTestServer.AnonymousClientAsync(token).ConfigureAwait(false);
            ClientConfigResponse config = (await anon.GetAsync("/v1.0/api/client-config", token).ConfigureAwait(false)).Expect(200, "client config").Json<ClientConfigResponse>();
            Assert.True(config.ProductName.Length > 0 && config.Links.ContainsKey("deleteAccount") && config.Copyright == "(c) 2026 Lattice AI LLC", "config contents");
        }
    }
}
