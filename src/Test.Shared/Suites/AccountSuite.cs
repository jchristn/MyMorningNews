namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Server;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Hosted accounts (plan section 33.1): public sign-up with a personal tenant, email verification, password reset,
    /// account deletion with a grace period and purge, data export, and the mobile universal link files.
    /// </summary>
    public static class AccountSuite
    {
        private const string S = "Accounts";
        private const string Password = "correct horse battery";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Sign-up, verification, password reset, deletion, and export", new[]
            {
                Cases.Case(S, "SignupVerify", "Sign-up makes a personal tenant on the platform key, refuses sign-in until the email is confirmed, and never reveals existing accounts", SignupAsync),
                Cases.Case(S, "SignupBillingPolicy", "New users capture the administrator's non-billed tier; changing server defaults preserves existing users", SignupBillingPolicyAsync),
                Cases.Case(S, "SignupRules", "Closed sign-up, missing terms or age, weak passwords, blocked domains, and floods are refused", RulesAsync),
                Cases.Case(S, "PasswordReset", "A reset link sets a new password once, signs out every session, and sends a confirmation", ResetAsync),
                Cases.Case(S, "Deletion", "Deleting signs out, signing in within the grace period cancels it, and the purge removes the account and its personal tenant", DeletionAsync),
                Cases.Case(S, "Export", "The export is a ZIP of the profile, topics, and briefings", ExportAsync),
                Cases.Case(S, "RequestLimit", "The per-address request limit answers 429 with Retry-After, spares health checks, and the limiter forgets idle addresses", RequestLimitAsync),
                Cases.Case(S, "UniversalLinks", "The iOS and Android link files answer 404 NotConfigured as JSON until configured, then 200 application/json with no sign-in and no redirect", LinksAsync),
                Cases.Case(S, "McpUrl", "Client config publishes a reachable MCP address, derives it from the public base URL when /mcp is routed there, and leaves it out instead of a loopback address on a public server", McpUrlAsync)
            });
        }

        private static string NewEmail()
        {
            return "signup-" + Guid.NewGuid().ToString("N").Substring(0, 10) + "@example.test";
        }

        private static async Task SignupBillingPolicyAsync(CancellationToken token)
        {
            await WithHostingAsync(async (h, server) =>
            {
                bool previousRequired = h.NewUsersRequireSubscription;
                string previousTier = h.NonBilledPlanKey;
                string previousDefault = h.DefaultPlanKey;
                try
                {
                    h.NewUsersRequireSubscription = false;
                    h.NonBilledPlanKey = "complimentary-five";
                    TestApiClient anon = new TestApiClient(await SharedTestServer.GetBaseUrlAsync(token).ConfigureAwait(false));
                    string email = NewEmail();
                    (await SignupAsync(anon, email, token).ConfigureAwait(false)).Expect(202, "signup with non-billed tier");
                    User user = (await server.Context.Database.Users.ReadAllByEmailAsync(email, token).ConfigureAwait(false)).Single();
                    MyMorningNews.Core.Entitlements.EffectivePlan plan = await server.Context.Entitlements.GetEffectiveAsync(user.TenantId, user.Id, token).ConfigureAwait(false);
                    Assert.True(!plan.SubscriptionRequired && plan.Plan.Key == "complimentary-five" && plan.Features["topics.max"] == "5" && plan.Features["systemSubscriptions.max"] == "5" && plan.Features["audio.dailySeconds.max"] == "unlimited", "non-billed tier has its own limits");
                    h.NewUsersRequireSubscription = true;
                    h.DefaultPlanKey = "catalog-only";
                    plan = await server.Context.Entitlements.GetEffectiveAsync(user.TenantId, user.Id, token).ConfigureAwait(false);
                    Assert.True(!plan.SubscriptionRequired && plan.Plan.Key == "complimentary-five", "server changes do not rebill existing users");
                    email = NewEmail();
                    (await SignupAsync(anon, email, token).ConfigureAwait(false)).Expect(202, "signup requiring billing for paid access");
                    user = (await server.Context.Database.Users.ReadAllByEmailAsync(email, token).ConfigureAwait(false)).Single();
                    plan = await server.Context.Entitlements.GetEffectiveAsync(user.TenantId, user.Id, token).ConfigureAwait(false);
                    Assert.True(plan.SubscriptionRequired && plan.Plan.Key == "catalog-only" && plan.Features["topics.max"] == "0", "new policy applies only to new account");
                    h.NewUsersRequireSubscription = false;
                    h.NonBilledPlanKey = "missing-plan";
                    email = NewEmail();
                    await SignupAsync(anon, email, token).ConfigureAwait(false);
                    Assert.True(!(await server.Context.Database.Users.ReadAllByEmailAsync(email, token).ConfigureAwait(false)).Any(), "bad policy never creates a stranded account");
                    return true;
                }
                finally
                {
                    h.NewUsersRequireSubscription = previousRequired;
                    h.NonBilledPlanKey = previousTier;
                    h.DefaultPlanKey = previousDefault;
                }
            }, token).ConfigureAwait(false);
        }

        private static async Task<T> WithHostingAsync<T>(Func<HostingSettings, MyMorningNewsServer, Task<T>> body, CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            HostingSettings h = server.Context.Settings.Hosting;
            bool signup = h.PublicSignupEnabled;
            bool verify = h.RequireEmailVerification;
            int perIp = h.SignupsPerIpPerHour;
            List<string> blocked = h.BlockedEmailDomains;
            try
            {
                h.PublicSignupEnabled = true;
                h.RequireEmailVerification = true;
                h.SignupsPerIpPerHour = 1000;
                return await body(h, server).ConfigureAwait(false);
            }
            finally
            {
                h.PublicSignupEnabled = signup;
                h.RequireEmailVerification = verify;
                h.SignupsPerIpPerHour = perIp;
                h.BlockedEmailDomains = blocked;
            }
        }

        private static async Task<TestResponse> SignupAsync(TestApiClient anon, string email, CancellationToken token, bool terms = true, bool age = true, string password = Password)
        {
            return await anon.PostAsync("/v1.0/public/signup", new { email = email, password = password, displayName = "Signup Tester", timeZone = "America/Chicago", acceptTerms = terms, confirmAge = age }, token).ConfigureAwait(false);
        }

        private static async Task<TestResponse> SignInAsync(TestApiClient anon, string email, string password, CancellationToken token)
        {
            return await anon.SendAsync(HttpMethod.Post, "/v1.0/token", null, new Dictionary<string, string> { { "x-email", email }, { "x-password", password } }, token).ConfigureAwait(false);
        }

        private static async Task<string> LinkTokenAsync(MyMorningNewsServer server, string tenantId, EmailTemplateEnum template, CancellationToken token)
        {
            List<EmailMessage> mail = (await server.Context.Database.EmailMessages.EnumerateAsync(new EmailMessageFilter { TenantId = tenantId, PageSize = 100 }, token).ConfigureAwait(false)).Objects;
            EmailMessage? m = mail.Where(x => x.Template == template).OrderByDescending(x => x.CreatedUtc).FirstOrDefault();
            Assert.True(m != null && !String.IsNullOrEmpty(m.RenderedText), template + " email was written");
            Match match = Regex.Match(m!.RenderedText!, "token=([A-Za-z0-9%_\\-\\.~]+)");
            Assert.True(match.Success, "the email carries a link token");
            return Uri.UnescapeDataString(match.Groups[1].Value);
        }

        private static async Task<User> SignedUpAsync(MyMorningNewsServer server, TestApiClient anon, CancellationToken token)
        {
            string email = NewEmail();
            (await SignupAsync(anon, email, token).ConfigureAwait(false)).Expect(202, "sign up");
            User user = (await server.Context.Database.Users.ReadAllByEmailAsync(email, token).ConfigureAwait(false)).Single();
            string link = await LinkTokenAsync(server, user.TenantId, EmailTemplateEnum.VerifyEmail, token).ConfigureAwait(false);
            (await anon.PostAsync("/v1.0/public/verify-email", new { token = link }, token).ConfigureAwait(false)).Expect(200, "verify");
            return (await server.Context.Database.Users.ReadAsync(user.TenantId, user.Id, token).ConfigureAwait(false))!;
        }

        private static async Task<TestApiClient> ClientForAsync(User user, string password, CancellationToken token)
        {
            TestApiClient c = new TestApiClient(await SharedTestServer.GetBaseUrlAsync(token).ConfigureAwait(false));
            await c.LoginAsync(user.TenantId, user.Email, password, token).ConfigureAwait(false);
            return c;
        }

        private static async Task SignupAsync(CancellationToken token)
        {
            await WithHostingAsync<bool>(async (h, server) =>
            {
                TestApiClient anon = await SharedTestServer.AnonymousClientAsync(token).ConfigureAwait(false);
                ClientConfigResponse config = (await anon.GetAsync("/v1.0/api/client-config", token).ConfigureAwait(false)).Expect(200, "client config").Json<ClientConfigResponse>();
                Assert.True(config.SignupEnabled && config.RequireEmailVerification && config.MinimumAge == 18 && config.TurnstileSiteKey == null, "client config advertises sign-up");
                Assert.Equal(server.Context.Settings.Rest.AppBaseUrl, config.AppUrl, "client config names the web app address");
                Assert.Equal(h.DeletionGraceDays, config.DeletionGraceDays, "client config gives the deletion grace period");

                string email = NewEmail();
                TestResponse first = (await SignupAsync(anon, email.ToUpperInvariant(), token).ConfigureAwait(false)).Expect(202, "sign up");
                User user = (await server.Context.Database.Users.ReadAllByEmailAsync(email, token).ConfigureAwait(false)).Single();
                Tenant tenant = (await server.Context.Database.Tenants.ReadAsync(user.TenantId, token).ConfigureAwait(false))!;
                Assert.True(user.SelfSignup && !user.EmailVerifiedUtc.HasValue && user.IsTenantAdmin && !user.IsAdmin && user.TermsVersion == h.TermsVersion && user.TermsAcceptedUtc.HasValue && user.TimeZone == "America/Chicago",
                    "unverified self sign-up user who accepted the terms");
                Assert.True(tenant.IsPersonal && tenant.Name.EndsWith("(personal)", StringComparison.Ordinal), "personal tenant");
                List<ModelEndpoint> endpoints = (await server.Context.Database.ModelEndpoints.EnumerateAsync(new ModelEndpointFilter { TenantId = tenant.Id, PageSize = 50 }, token).ConfigureAwait(false)).Objects;
                Assert.True(endpoints.Any(e => e.Provider == ModelProviderEnum.Anthropic && e.CredentialId == VaultService.PlatformAnthropicCredentialId), "the personal tenant uses the platform key by reference");
                long vault = await server.Context.Database.VaultCredentials.CountAsync(new VaultCredentialFilter { TenantId = tenant.Id }, token).ConfigureAwait(false);
                Assert.Equal(0L, vault, "no copy of the platform key in the tenant vault");

                TestResponse blocked = (await SignInAsync(anon, email, Password, token).ConfigureAwait(false)).Expect(403, "unverified sign-in refused");
                Assert.True(blocked.Body.Contains("EmailNotVerified", StringComparison.Ordinal), "reason: " + blocked.Body);

                TestResponse again = (await SignupAsync(anon, email, token).ConfigureAwait(false)).Expect(202, "repeat sign-up looks the same");
                Assert.Equal(first.Body, again.Body, "same answer for an existing account");
                Assert.Equal(1, (await server.Context.Database.Users.ReadAllByEmailAsync(email, token).ConfigureAwait(false)).Count, "no second account");
                (await anon.PostAsync("/v1.0/public/verify-email/resend", new { email = email }, token).ConfigureAwait(false)).Expect(202, "resend");
                (await anon.PostAsync("/v1.0/public/verify-email/resend", new { email = NewEmail() }, token).ConfigureAwait(false)).Expect(202, "resend for nobody looks the same");

                string link = await LinkTokenAsync(server, tenant.Id, EmailTemplateEnum.VerifyEmail, token).ConfigureAwait(false);
                ErrorResponse bad = (await anon.PostAsync("/v1.0/public/verify-email", new { token = "not-a-token" }, token).ConfigureAwait(false)).Expect(400, "bad token").Json<ErrorResponse>();
                Assert.Equal("LinkInvalid", bad.Error, "an unknown link is LinkInvalid");
                (await anon.PostAsync("/v1.0/public/verify-email", new { token = link }, token).ConfigureAwait(false)).Expect(200, "verify");
                ErrorResponse used = (await anon.PostAsync("/v1.0/public/verify-email", new { token = link }, token).ConfigureAwait(false)).Expect(400, "a link works once").Json<ErrorResponse>();
                Assert.Equal("LinkUsed", used.Error, "a used verification link says so (UT-027)");
                Assert.True(used.Message.Contains("Sign in", StringComparison.Ordinal), "a used verification link tells the reader to sign in");
                User verified = (await server.Context.Database.Users.ReadAsync(user.TenantId, user.Id, token).ConfigureAwait(false))!;
                Assert.True(verified.EmailVerifiedUtc.HasValue, "verified");
                TokenResponse session = (await SignInAsync(anon, email, Password, token).ConfigureAwait(false)).Expect(200, "sign in without a tenant").Json<TokenResponse>();
                Assert.Equal(tenant.Id, session.TenantId, "signed in to the personal tenant");
                List<EmailMessage> mail = (await server.Context.Database.EmailMessages.EnumerateAsync(new EmailMessageFilter { TenantId = tenant.Id, PageSize = 100 }, token).ConfigureAwait(false)).Objects;
                Assert.True(mail.Any(m => m.Template == EmailTemplateEnum.Welcome), "welcome email after verifying");
                return true;
            }, token).ConfigureAwait(false);
        }

        private static async Task RulesAsync(CancellationToken token)
        {
            await WithHostingAsync<bool>(async (h, server) =>
            {
                TestApiClient anon = await SharedTestServer.AnonymousClientAsync(token).ConfigureAwait(false);
                (await SignupAsync(anon, NewEmail(), token, terms: false).ConfigureAwait(false)).Expect(400, "terms required");
                (await SignupAsync(anon, NewEmail(), token, age: false).ConfigureAwait(false)).Expect(400, "age required");
                (await SignupAsync(anon, NewEmail(), token, password: "short").ConfigureAwait(false)).Expect(400, "weak password");
                (await SignupAsync(anon, "not-an-email", token).ConfigureAwait(false)).Expect(400, "bad email");
                h.BlockedEmailDomains = new List<string> { "throwaway.test" };
                (await SignupAsync(anon, "someone@throwaway.test", token).ConfigureAwait(false)).Expect(400, "blocked domain");
                h.RequireEmailVerification = false;
                string open = NewEmail();
                (await SignupAsync(anon, open, token).ConfigureAwait(false)).Expect(202, "sign up without verification");
                (await SignInAsync(anon, open, Password, token).ConfigureAwait(false)).Expect(200, "signs in at once when verification is off");
                h.PublicSignupEnabled = false;
                (await SignupAsync(anon, NewEmail(), token).ConfigureAwait(false)).Expect(404, "closed");
                h.PublicSignupEnabled = true;
                h.SignupsPerIpPerHour = 1;
                bool limited = false;
                for (int i = 0; i < 3 && !limited; i++) limited = (await SignupAsync(anon, NewEmail(), token).ConfigureAwait(false)).StatusCode == 429;
                Assert.True(limited, "sign-ups from one address are rate limited");
                return true;
            }, token).ConfigureAwait(false);
        }

        private static async Task ResetAsync(CancellationToken token)
        {
            await WithHostingAsync<bool>(async (h, server) =>
            {
                TestApiClient anon = await SharedTestServer.AnonymousClientAsync(token).ConfigureAwait(false);
                User user = await SignedUpAsync(server, anon, token).ConfigureAwait(false);
                TestApiClient signedIn = await ClientForAsync(user, Password, token).ConfigureAwait(false);
                (await signedIn.GetAsync("/v1.0/api/me", token).ConfigureAwait(false)).Expect(200, "signed in");

                (await anon.PostAsync("/v1.0/public/password/forgot", new { email = NewEmail() }, token).ConfigureAwait(false)).Expect(202, "unknown email looks the same");
                (await anon.PostAsync("/v1.0/public/password/forgot", new { email = user.Email }, token).ConfigureAwait(false)).Expect(202, "forgot");
                string link = await LinkTokenAsync(server, user.TenantId, EmailTemplateEnum.PasswordReset, token).ConfigureAwait(false);
                (await anon.PostAsync("/v1.0/public/password/reset", new { token = link, password = "short" }, token).ConfigureAwait(false)).Expect(400, "weak password");
                string next = "a brand new passphrase";
                (await anon.PostAsync("/v1.0/public/password/reset", new { token = link, password = next }, token).ConfigureAwait(false)).Expect(200, "reset");
                (await anon.PostAsync("/v1.0/public/password/reset", new { token = link, password = next + "!" }, token).ConfigureAwait(false)).Expect(400, "a reset link works once");
                (await signedIn.GetAsync("/v1.0/api/me", token).ConfigureAwait(false)).Expect(401, "old sessions end");
                (await SignInAsync(anon, user.Email, Password, token).ConfigureAwait(false)).Expect(401, "old password refused");
                (await SignInAsync(anon, user.Email, next, token).ConfigureAwait(false)).Expect(200, "new password works");
                List<EmailMessage> mail = (await server.Context.Database.EmailMessages.EnumerateAsync(new EmailMessageFilter { TenantId = user.TenantId, PageSize = 100 }, token).ConfigureAwait(false)).Objects;
                Assert.True(mail.Any(m => m.Template == EmailTemplateEnum.PasswordChanged), "confirmation email");
                return true;
            }, token).ConfigureAwait(false);
        }

        private static async Task DeletionAsync(CancellationToken token)
        {
            await WithHostingAsync<bool>(async (h, server) =>
            {
                TestApiClient anon = await SharedTestServer.AnonymousClientAsync(token).ConfigureAwait(false);
                User user = await SignedUpAsync(server, anon, token).ConfigureAwait(false);
                TestApiClient c = await ClientForAsync(user, Password, token).ConfigureAwait(false);
                (await c.PostAsync("/v1.0/api/topics", new { name = "Deletion topic" }, token).ConfigureAwait(false)).Expect(201, "topic");

                TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
                (await admin.PostAsync("/v1.0/api/account/delete", null, token).ConfigureAwait(false)).Expect(409, "the seeded administrator is protected");

                JsonElement first = (await c.PostAsync("/v1.0/api/account/delete", null, token).ConfigureAwait(false)).Expect(202, "delete").Json<JsonElement>();
                DateTime purge = first.GetProperty("purgeAfterUtc").GetDateTime();
                Assert.True(purge > DateTime.UtcNow.AddDays(h.DeletionGraceDays - 0.1) && purge < DateTime.UtcNow.AddDays(h.DeletionGraceDays + 0.1), "purge after the grace period");
                (await c.GetAsync("/v1.0/api/me", token).ConfigureAwait(false)).Expect(401, "signed out");
                User pending = (await server.Context.Database.Users.ReadAsync(user.TenantId, user.Id, token).ConfigureAwait(false))!;
                Assert.True(pending.DeletionRequestedUtc.HasValue, "deletion pending");
                Assert.True((await server.Context.Database.Tenants.ReadAsync(user.TenantId, token).ConfigureAwait(false))!.PurgeAfterUtc.HasValue, "tenant purge date set");

                TokenResponse back = (await SignInAsync(anon, user.Email, Password, token).ConfigureAwait(false)).Expect(200, "signing in again").Json<TokenResponse>();
                Assert.True(back.DeletionCancelled, "the sign-in response says the deletion was cancelled");
                Assert.True(!(await SignInAsync(anon, user.Email, Password, token).ConfigureAwait(false)).Expect(200, "a later sign-in").Json<TokenResponse>().DeletionCancelled, "only the sign-in that cancelled it says so");
                User kept = (await server.Context.Database.Users.ReadAsync(user.TenantId, user.Id, token).ConfigureAwait(false))!;
                Assert.True(!kept.DeletionRequestedUtc.HasValue && !(await server.Context.Database.Tenants.ReadAsync(user.TenantId, token).ConfigureAwait(false))!.PurgeAfterUtc.HasValue, "sign-in cancels the deletion");

                c = await ClientForAsync(user, Password, token).ConfigureAwait(false);
                (await c.PostAsync("/v1.0/api/account/delete", new { password = "not the password", confirm = true }, token).ConfigureAwait(false)).Expect(400, "a wrong password is refused");
                (await c.PostAsync("/v1.0/api/account/delete", new { password = Password, confirm = true }, token).ConfigureAwait(false)).Expect(202, "delete again with the password");
                int early = await server.Context.Account.PurgeDueAsync(DateTime.UtcNow, token).ConfigureAwait(false);
                Assert.True(await server.Context.Database.Users.ReadAsync(user.TenantId, user.Id, token).ConfigureAwait(false) != null, "nothing purged during the grace period (" + early + ")");
                await server.Context.Account.PurgeDueAsync(DateTime.UtcNow.AddDays(h.DeletionGraceDays + 1), token).ConfigureAwait(false);
                Assert.True(await server.Context.Database.Users.ReadAsync(user.TenantId, user.Id, token).ConfigureAwait(false) == null, "user purged");
                Assert.True(await server.Context.Database.Tenants.ReadAsync(user.TenantId, token).ConfigureAwait(false) == null, "personal tenant purged");
                (await SignInAsync(anon, user.Email, Password, token).ConfigureAwait(false)).Expect(401, "account gone");
                return true;
            }, token).ConfigureAwait(false);
        }

        private static async Task ExportAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser u = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            string name = "Export topic " + Guid.NewGuid().ToString("N").Substring(0, 6);
            (await u.Client.PostAsync("/v1.0/api/topics", new { name = name }, token).ConfigureAwait(false)).Expect(201, "topic");
            using (HttpClient http = new HttpClient())
            using (HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, await SharedTestServer.GetBaseUrlAsync(token).ConfigureAwait(false) + "/v1.0/api/account/export"))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", u.Client.Token);
                using (HttpResponseMessage r = await http.SendAsync(req, token).ConfigureAwait(false))
                {
                    Assert.Equal(200, (int)r.StatusCode, "export");
                    Assert.Equal("application/zip", r.Content.Headers.ContentType?.MediaType ?? String.Empty, "zip");
                    byte[] bytes = await r.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
                    using (MemoryStream ms = new MemoryStream(bytes))
                    using (ZipArchive zip = new ZipArchive(ms, ZipArchiveMode.Read))
                    {
                        List<string> names = zip.Entries.Select(e => e.FullName).ToList();
                        Assert.True(new[] { "README.txt", "profile.json", "topics.json", "findings.json", "predictions.json", "feedback.json" }.All(names.Contains), "entries: " + String.Join(",", names));
                        using (StreamReader sr = new StreamReader(zip.GetEntry("topics.json")!.Open()))
                        {
                            Assert.True((await sr.ReadToEndAsync(token).ConfigureAwait(false)).Contains(name, StringComparison.Ordinal), "the topic is in the export");
                        }

                        using (StreamReader sr = new StreamReader(zip.GetEntry("profile.json")!.Open()))
                        {
                            string profile = await sr.ReadToEndAsync(token).ConfigureAwait(false);
                            Assert.True(profile.Contains(u.User.Email, StringComparison.Ordinal) && !profile.Contains("password", StringComparison.OrdinalIgnoreCase), "profile without secrets");
                        }
                    }
                }
            }
        }

        private static async Task RequestLimitAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            HostingSettings h = server.Context.Settings.Hosting;
            int before = h.RequestsPerMinutePerIp;
            TestApiClient anon = await SharedTestServer.AnonymousClientAsync(token).ConfigureAwait(false);
            try
            {
                h.RequestsPerMinutePerIp = 5;
                List<TestResponse> responses = new List<TestResponse>();
                for (int i = 0; i < 8; i++) responses.Add(await anon.GetAsync("/v1.0/api/client-config", token).ConfigureAwait(false));
                TestResponse? limited = responses.FirstOrDefault(r => r.StatusCode == 429);
                Assert.True(limited != null && limited.Body.Contains("TooManyRequests", StringComparison.Ordinal) && limited.Headers.ContainsKey("Retry-After"), "429 with Retry-After: " + String.Join(",", responses.Select(r => r.StatusCode)));
                (await anon.GetAsync(MyMorningNews.Core.Constants.HealthPath, token).ConfigureAwait(false)).Expect(200, "health is exempt");
                h.TrustForwardedFor = true;
                for (int i = 0; i < 8; i++)
                {
                    Dictionary<string, string> forwarded = new Dictionary<string, string> { { "x-forwarded-for", "198.51.100." + (10 + i) } };
                    (await anon.SendAsync(HttpMethod.Get, "/v1.0/api/client-config", null, forwarded, token).ConfigureAwait(false)).Expect(200, "each client behind the proxy has its own budget");
                }

                Dictionary<string, string> spoof = new Dictionary<string, string> { { "x-forwarded-for", "203.0.113.7, 198.51.100.200" } };
                for (int i = 0; i < 5; i++) await anon.SendAsync(HttpMethod.Get, "/v1.0/api/client-config", null, spoof, token).ConfigureAwait(false);
                Dictionary<string, string> sameClient = new Dictionary<string, string> { { "x-forwarded-for", "198.51.100.200" } };
                (await anon.SendAsync(HttpMethod.Get, "/v1.0/api/client-config", null, sameClient, token).ConfigureAwait(false)).Expect(429, "the address the proxy appended is the one counted");
            }
            finally
            {
                h.RequestsPerMinutePerIp = before;
                h.TrustForwardedFor = false;
            }

            (await anon.GetAsync("/v1.0/api/client-config", token).ConfigureAwait(false)).Expect(200, "limit off again");
            MyMorningNews.Core.Services.LoginRateLimiter limiter = new MyMorningNews.Core.Services.LoginRateLimiter(3) { WindowSeconds = 60 };
            for (int i = 0; i < 50; i++) limiter.TryAcquire("ip-" + i);
            Assert.Equal(50, limiter.TrackedKeys, "keys tracked within the window");
            Assert.Equal(50, limiter.Prune(DateTime.UtcNow.AddSeconds(61)), "idle keys pruned");
            Assert.Equal(0, limiter.TrackedKeys, "nothing left");
        }

        private static async Task LinksAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            HostingSettings h = server.Context.Settings.Hosting;
            string team = h.AppleTeamId;
            List<string> prints = h.AndroidCertificateFingerprints;
            TestApiClient anon = await SharedTestServer.AnonymousClientAsync(token).ConfigureAwait(false);
            try
            {
                h.AppleTeamId = String.Empty;
                h.AndroidCertificateFingerprints = new List<string>();
                foreach (string file in new[] { "/.well-known/apple-app-site-association", "/.well-known/assetlinks.json" })
                {
                    TestResponse missing = (await anon.GetAsync(file, token).ConfigureAwait(false)).Expect(404, "not configured: " + file);
                    Assert.True(missing.Body.Contains("NotConfigured", StringComparison.Ordinal) && missing.Body.Contains("MYMORNINGNEWS_HOSTING_", StringComparison.Ordinal), "the 404 names the setting to fill in: " + missing.Body);
                    Assert.True(missing.Headers["Content-Type"].StartsWith("application/json", StringComparison.OrdinalIgnoreCase), "the 404 is JSON, not a page: " + missing.Headers["Content-Type"]);
                }

                h.AppleTeamId = "  ";
                h.AndroidCertificateFingerprints = new List<string> { " ", String.Empty };
                (await anon.GetAsync("/.well-known/apple-app-site-association", token).ConfigureAwait(false)).Expect(404, "a blank team ID is not configured");
                (await anon.GetAsync("/.well-known/assetlinks.json", token).ConfigureAwait(false)).Expect(404, "blank fingerprints are not configured");
                h.AppleTeamId = "ABCDE12345";
                h.AndroidCertificateFingerprints = new List<string> { "aa:bb:cc" };
                string apple = (await anon.GetAsync("/.well-known/apple-app-site-association", token).ConfigureAwait(false)).Expect(200, "apple").Body;
                Assert.True(apple.Contains("ABCDE12345.ai.latticetech.mymorningnews", StringComparison.Ordinal) && apple.Contains("/briefings/*", StringComparison.Ordinal)
                    && apple.Contains("/verify*", StringComparison.Ordinal) && apple.Contains("/reset-password*", StringComparison.Ordinal) && apple.Contains("/signup*", StringComparison.Ordinal), "apple file, including the verify, reset, and sign-up links: " + apple);
                string android = (await anon.GetAsync("/.well-known/assetlinks.json", token).ConfigureAwait(false)).Expect(200, "android").Body;
                Assert.True(android.Contains("ai.latticetech.mymorningnews", StringComparison.Ordinal) && android.Contains("AA:BB:CC", StringComparison.Ordinal), "android file: " + android);
                foreach (string file in new[] { "/.well-known/apple-app-site-association", "/.well-known/assetlinks.json" })
                {
                    TestResponse served = (await anon.GetAsync(file, token).ConfigureAwait(false)).Expect(200, "served without credentials or a redirect: " + file);
                    Assert.True(served.Headers["Content-Type"].StartsWith("application/json", StringComparison.OrdinalIgnoreCase), "content type is application/json: " + served.Headers["Content-Type"]);
                    Assert.True(!served.Headers.ContainsKey("Location"), "no redirect: " + file);
                    using (JsonDocument parsed = JsonDocument.Parse(served.Body))
                    {
                        Assert.True(parsed.RootElement.ValueKind == (file.EndsWith(".json", StringComparison.Ordinal) ? JsonValueKind.Array : JsonValueKind.Object), "valid JSON of the expected shape: " + file);
                    }
                }
            }
            finally
            {
                h.AppleTeamId = team;
                h.AndroidCertificateFingerprints = prints;
            }
        }

        private static async Task McpUrlAsync(CancellationToken token)
        {
            RestSettings local = new RestSettings();
            Assert.Equal("http://127.0.0.1:12105/mcp", McpUrlResolver.Resolve(local) ?? "(null)", "a local install publishes the local MCP address");
            RestSettings hosted = new RestSettings { PublicBaseUrl = "https://app.example.test/" };
            Assert.True(McpUrlResolver.Resolve(hosted) == null, "a public server never publishes a loopback MCP address");
            hosted.McpOnPublicBaseUrl = true;
            Assert.Equal("https://app.example.test/mcp", McpUrlResolver.Resolve(hosted) ?? "(null)", "derived from the public base URL when /mcp is routed there");
            hosted.McpPublicUrl = "https://mcp.example.test/mcp";
            Assert.Equal("https://mcp.example.test/mcp", McpUrlResolver.Resolve(hosted) ?? "(null)", "an explicit public address wins");
            hosted.McpOnPublicBaseUrl = false;
            Assert.Equal("https://mcp.example.test/mcp", McpUrlResolver.Resolve(hosted) ?? "(null)", "an explicit public address needs no routing flag");
            hosted.McpPublicUrl = "http://localhost:12105/mcp";
            Assert.True(McpUrlResolver.Resolve(hosted) == null, "localhost counts as loopback");
            Assert.True(McpUrlResolver.IsLoopback("http://[::1]:12105/mcp") && McpUrlResolver.IsLoopback("http://127.0.0.2/") && !McpUrlResolver.IsLoopback("https://app.example.test") && !McpUrlResolver.IsLoopback("nope") && !McpUrlResolver.IsLoopback(null), "loopback detection");

            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            RestSettings rest = server.Context.SettingsService.Current.Rest;
            string publicBase = rest.PublicBaseUrl;
            string mcp = rest.McpPublicUrl;
            bool routed = rest.McpOnPublicBaseUrl;
            TestApiClient anon = await SharedTestServer.AnonymousClientAsync(token).ConfigureAwait(false);
            try
            {
                rest.PublicBaseUrl = "http://127.0.0.1:12100";
                rest.McpPublicUrl = "http://127.0.0.1:12105/mcp";
                rest.McpOnPublicBaseUrl = false;
                TestResponse localConfig = (await anon.GetAsync("/v1.0/api/client-config", token).ConfigureAwait(false)).Expect(200, "client config, local");
                Assert.Equal("http://127.0.0.1:12105/mcp", localConfig.Json<ClientConfigResponse>().McpUrl ?? "(null)", "local install");

                rest.PublicBaseUrl = "https://app.example.test";
                TestResponse publicConfig = (await anon.GetAsync("/v1.0/api/client-config", token).ConfigureAwait(false)).Expect(200, "client config, public without an MCP address");
                Assert.True(publicConfig.Json<ClientConfigResponse>().McpUrl == null, "no MCP address is published");
                Assert.True(!publicConfig.Body.Contains("127.0.0.1:12105", StringComparison.Ordinal) && !publicConfig.Body.Contains("mcpUrl", StringComparison.Ordinal), "the loopback address is left out of the JSON: " + publicConfig.Body);

                rest.McpOnPublicBaseUrl = true;
                TestResponse routedConfig = (await anon.GetAsync("/v1.0/api/client-config", token).ConfigureAwait(false)).Expect(200, "client config, /mcp routed");
                Assert.Equal("https://app.example.test/mcp", routedConfig.Json<ClientConfigResponse>().McpUrl ?? "(null)", "derived from the public base URL");

                rest.McpPublicUrl = "https://mcp.example.test/mcp";
                TestResponse explicitConfig = (await anon.GetAsync("/v1.0/api/client-config", token).ConfigureAwait(false)).Expect(200, "client config, explicit");
                Assert.Equal("https://mcp.example.test/mcp", explicitConfig.Json<ClientConfigResponse>().McpUrl ?? "(null)", "explicit public address");
            }
            finally
            {
                rest.PublicBaseUrl = publicBase;
                rest.McpPublicUrl = mcp;
                rest.McpOnPublicBaseUrl = routed;
            }
        }
    }
}
