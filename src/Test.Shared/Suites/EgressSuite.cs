namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Security;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Server;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Outbound request protection and platform credentials for the hosted service (plan section 33.3).
    /// </summary>
    public static class EgressSuite
    {
        private const string S = "Egress";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Private network blocking and platform credentials", new[]
            {
                Cases.Case(S, "Addresses", "Loopback, private, link-local, carrier NAT, unique-local, and metadata addresses are blocked; public ones are not", AddressesAsync),
                Cases.Case(S, "Enforcement", "With the guard on, URLs, connections, and saved endpoints that reach private networks are refused", EnforcementAsync),
                Cases.Case(S, "PlatformCredential", "The platform key resolves from the server environment and tenants cannot assign it", PlatformAsync)
            });
        }

        private static Task AddressesAsync(CancellationToken token)
        {
            foreach (string blocked in new[] { "127.0.0.1", "10.1.2.3", "172.16.0.1", "172.31.255.255", "192.168.1.1", "169.254.169.254", "100.64.0.1", "0.0.0.0", "224.0.0.1", "::1", "fe80::1", "fd00::1", "fc00::1", "::ffff:10.0.0.1", "fd00:ec2::254" })
                Assert.True(NetworkGuard.IsBlockedAddress(IPAddress.Parse(blocked)), blocked + " is blocked");
            foreach (string open in new[] { "8.8.8.8", "1.1.1.1", "172.32.0.1", "100.128.0.1", "2606:4700:4700::1111" })
                Assert.True(!NetworkGuard.IsBlockedAddress(IPAddress.Parse(open)), open + " is allowed");
            return Task.CompletedTask;
        }

        private static async Task EnforcementAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            string health = await SharedTestServer.GetBaseUrlAsync(token).ConfigureAwait(false) + "/";
            using (HttpClient guarded = new HttpClient(NetworkGuard.CreateHandler()))
            {
                (await guarded.GetAsync(health, token).ConfigureAwait(false)).Dispose();
                await NetworkGuard.EnsureAllowedAsync("http://127.0.0.1:1/", token).ConfigureAwait(false);
                bool restrict = server.Context.Settings.Hosting.RestrictPrivateNetworks;
                try
                {
                    server.Context.Settings.Hosting.RestrictPrivateNetworks = true;
                    foreach (string url in new[] { "http://127.0.0.1:1/", "http://localhost/", "http://169.254.169.254/latest/meta-data/", "http://[::1]/", "ftp://example.com/", "http://metadata.google.internal/" })
                    {
                        bool refused = false;
                        try
                        {
                            await NetworkGuard.EnsureAllowedAsync(url, token).ConfigureAwait(false);
                        }
                        catch (EgressBlockedException)
                        {
                            refused = true;
                        }

                        Assert.True(refused, url + " refused");
                    }

                    bool connectRefused = false;
                    try
                    {
                        (await guarded.GetAsync(health, token).ConfigureAwait(false)).Dispose();
                    }
                    catch (HttpRequestException)
                    {
                        connectRefused = true;
                    }

                    Assert.True(connectRefused, "the guarded handler refuses a loopback connection");
                    List<string> allowed = server.Context.Settings.Hosting.AllowedPrivateHosts;
                    try
                    {
                        server.Context.Settings.Hosting.AllowedPrivateHosts = new List<string> { "127.0.0.1" };
                        await NetworkGuard.EnsureAllowedAsync(health, token).ConfigureAwait(false);
                        (await guarded.GetAsync(health, token).ConfigureAwait(false)).Dispose();
                    }
                    finally
                    {
                        server.Context.Settings.Hosting.AllowedPrivateHosts = allowed;
                    }

                    TestResponse endpoint = (await admin.PostAsync("/v1.0/api/model-endpoints", new { name = "Metadata", provider = "Ollama", model = "x", endpoint = "http://169.254.169.254/" }, token).ConfigureAwait(false)).Expect(400, "metadata endpoint refused");
                    Assert.True(endpoint.Body.Contains("EgressBlocked", StringComparison.Ordinal), "reason: " + endpoint.Body);
                    (await admin.PostAsync("/v1.0/api/mcp-servers", new { name = "Local", url = "http://127.0.0.1:12105/mcp" }, token).ConfigureAwait(false)).Expect(400, "loopback MCP server refused");
                }
                finally
                {
                    server.Context.Settings.Hosting.RestrictPrivateNetworks = restrict;
                }
            }
        }

        private static async Task PlatformAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            string variable = MyMorningNews.Core.Constants.EnvironmentPrefix + "ANTHROPIC_API_KEY";
            string? before = Environment.GetEnvironmentVariable(variable);
            try
            {
                Environment.SetEnvironmentVariable(variable, "sk-ant-platform-test");
                Assert.Equal("sk-ant-platform-test", await server.Context.Vault.ResolveAsync("any-tenant", VaultService.PlatformAnthropicCredentialId, token).ConfigureAwait(false) ?? String.Empty, "resolved from the environment");
                Environment.SetEnvironmentVariable(variable, null);
                bool missing = false;
                try
                {
                    await server.Context.Vault.ResolveAsync("any-tenant", VaultService.PlatformAnthropicCredentialId, token).ConfigureAwait(false);
                }
                catch (VaultSecretUnavailableException)
                {
                    missing = true;
                }

                Assert.True(missing, "a missing platform key is a clear error");
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, before);
            }

            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            MyMorningNews.Core.Models.Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser u = await TestFixtures.CreateUserAsync(admin, t.Id, true, token).ConfigureAwait(false);
            TestResponse r = (await u.Client.PostAsync("/v1.0/api/model-endpoints", new { name = "Borrowed", provider = "Anthropic", model = "claude-opus-5-5", credentialId = VaultService.PlatformAnthropicCredentialId }, token).ConfigureAwait(false)).Expect(400, "tenants cannot assign the platform key");
            Assert.True(r.Body.Contains("operator", StringComparison.Ordinal), "reason: " + r.Body);
        }
    }
}
