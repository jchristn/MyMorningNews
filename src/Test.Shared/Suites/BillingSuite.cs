namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Billing;
    using MyMorningNews.Core.Entitlements;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Integrations.Implementations;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Serialization;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Server;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>Billing account isolation, concurrency, stale-event protection and real HTTP dependency simulation.</summary>
    public static class BillingSuite
    {
        /// <summary>Register core tests, plus Stripe simulation when MYMORNINGNEWS_BILLING_SIM_URL points at the local Postman simulation.</summary>
        public static TestSuiteDescriptor Create()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Cases.Case("Billing", "Replacement", "Native replacement ownership is checked before granting and superseded purchases cannot reactivate", ReplacementAsync),
                Cases.Case("Billing", "Concurrency", "Purchase bindings and pending checkout operations are atomic; expired checkout operations keep history", ConcurrencyAsync),
                Cases.Case("Billing", "Ownership", "Verified subscription ownership is immutable, stale snapshots cannot reactivate access, and admin assignments take precedence", OwnershipAsync)
            };
            if (!String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MYMORNINGNEWS_BILLING_SIM_URL")))
                cases.Add(Cases.Case("Billing", "StripeSimulation", "Real HTTP checkout survives an ambiguous failure; signed duplicate and delayed events reconcile current state and provider outages retry", StripeSimulationAsync));
            if (!String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MYMORNINGNEWS_STRIPE_SANDBOX_KEY")))
                cases.Add(Cases.Case("Billing", "StripeSandbox", "Real Stripe sandbox checkout, payment, cancellation and refund reconcile account access", StripeSandboxScenario.RunAsync));
            return new TestSuiteDescriptor("Billing", "Verified subscriptions", cases);
        }

        private static async Task ReplacementAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TestUser owner = await UserAsync(token).ConfigureAwait(false);
            TestUser thief = await UserAsync(token).ConfigureAwait(false);
            Plan plan = (await server.Context.Database.Plans.EnumerateAsync(new PlanFilter { Key = "paid-5" }, token).ConfigureAwait(false)).Objects.Single();
            BillingSubscription old = (await server.Context.Database.BillingSubscriptions.SaveVerifiedAsync(new BillingSubscription
            {
                TenantId = owner.User.TenantId, UserId = owner.User.Id, Provider = "Google", Environment = "Production", ProviderId = "old-" + Guid.NewGuid().ToString("N"),
                PlanId = plan.Id, Status = "Active", EndsUtc = DateTime.UtcNow.AddMonths(1), LastVerifiedUtc = DateTime.UtcNow
            }, token).ConfigureAwait(false))!;
            BillingSubscription next = new BillingSubscription { TenantId = thief.User.TenantId, UserId = thief.User.Id, Provider = "Google", Environment = "Production", ProviderId = "new-" + Guid.NewGuid().ToString("N"), PlanId = plan.Id, Status = "Active", EndsUtc = DateTime.UtcNow.AddMonths(1), LastVerifiedUtc = DateTime.UtcNow };
            bool refused = false;
            try { await server.Context.Database.BillingSubscriptions.SaveReplacementAsync(next, old.ProviderId, token).ConfigureAwait(false); }
            catch (InvalidOperationException) { refused = true; }
            Assert.True(refused && await server.Context.Database.BillingSubscriptions.ReadExternalAsync("Google", "Production", next.ProviderId, token).ConfigureAwait(false) == null, "cross-account replacement writes no entitlement");
            next.TenantId = owner.User.TenantId; next.UserId = owner.User.Id;
            await server.Context.Database.BillingSubscriptions.SaveReplacementAsync(next, old.ProviderId, token).ConfigureAwait(false);
            Assert.Equal("Expired", (await server.Context.Database.BillingSubscriptions.ReadExternalAsync("Google", "Production", old.ProviderId, token).ConfigureAwait(false))!.Status, "old token superseded atomically");
            old.LastVerifiedUtc = DateTime.UtcNow.AddMinutes(1);
            Assert.True(await server.Context.Database.BillingSubscriptions.SaveVerifiedAsync(old, token).ConfigureAwait(false) == null, "even a newer old-token reply cannot reactivate");
        }

        private static async Task<TestUser> UserAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant tenant = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            return await TestFixtures.CreateUserAsync(admin, tenant.Id, false, token).ConfigureAwait(false);
        }

        private static async Task ConcurrencyAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TestUser user = await UserAsync(token).ConfigureAwait(false);
            BillingAccount[] accounts = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => server.Context.Billing.AccountAsync(user.User.TenantId, user.User.Id, token))).ConfigureAwait(false);
            Assert.Equal(1, accounts.Select(a => a.PurchaseAccountToken).Distinct().Count(), "stable immutable binding");
            BillingCheckout[] checkouts = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => server.Context.Database.BillingCheckouts.ReserveAsync(new BillingCheckout
            { TenantId = user.User.TenantId, UserId = user.User.Id, OfferKey = "paid-5-month", ExpiresUtc = DateTime.UtcNow.AddHours(1) }, token))).ConfigureAwait(false);
            Assert.Equal(1, checkouts.Select(c => c.Id).Distinct().Count(), "one pending operation");
            BillingCheckout expired = checkouts[0]; expired.ExpiresUtc = DateTime.UtcNow.AddSeconds(-1);
            await server.Context.Database.BillingCheckouts.UpdateAsync(expired, token).ConfigureAwait(false);
            BillingCheckout next = await server.Context.Database.BillingCheckouts.ReserveAsync(new BillingCheckout
            { TenantId = user.User.TenantId, UserId = user.User.Id, OfferKey = "paid-20-year", ExpiresUtc = DateTime.UtcNow.AddHours(1) }, token).ConfigureAwait(false);
            Assert.True(next.Id != expired.Id && next.OfferKey == "paid-20-year", "new checkout after expiration");
            Assert.Equal("Expired", (await server.Context.Database.BillingCheckouts.ReadAsync(user.User.TenantId, expired.Id, token).ConfigureAwait(false))!.Status, "expired operation retained");
            string eventId = "test-" + Guid.NewGuid().ToString("N");
            await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => server.Context.Database.BillingEvents.EnqueueAsync(new BillingEvent
            { Provider = "Stripe", Environment = "Sandbox", EventId = eventId, ProviderId = "sub_test" }, token))).ConfigureAwait(false);
            Assert.Equal(1L, await server.Context.Database.BillingEvents.CountAsync(new BillingEventFilter { EventId = eventId }, token).ConfigureAwait(false), "one durable event");
            BillingEvent?[] claims = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => server.Context.Database.BillingEvents.ClaimAsync(token))).ConfigureAwait(false);
            Assert.Equal(1, claims.Count(c => c != null), "one worker lease");
            BillingEvent claimed = claims.Single(c => c != null)!;
            BillingEvent expiredClaim = (await server.Context.Database.BillingEvents.ReadAsync(claimed.Id, token).ConfigureAwait(false))!;
            expiredClaim.RetryUtc = DateTime.UtcNow.AddSeconds(-1);
            await server.Context.Database.BillingEvents.UpdateAsync(expiredClaim, token).ConfigureAwait(false);
            BillingEvent reclaimed = (await server.Context.Database.BillingEvents.ClaimAsync(token).ConfigureAwait(false))!;
            claimed.Status = "Completed";
            Assert.True(!await server.Context.Database.BillingEvents.FinishClaimAsync(claimed, token).ConfigureAwait(false), "stale worker cannot finish a reclaimed lease");
            reclaimed.Status = "Completed";
            Assert.True(await server.Context.Database.BillingEvents.FinishClaimAsync(reclaimed, token).ConfigureAwait(false), "current lease completes");
            claimed.Status = "Pending";
            Assert.True(!await server.Context.Database.BillingEvents.FinishClaimAsync(claimed, token).ConfigureAwait(false), "late failure cannot reopen completed work");
        }

        private static async Task OwnershipAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TestUser owner = await UserAsync(token).ConfigureAwait(false);
            TestUser attacker = await UserAsync(token).ConfigureAwait(false);
            Plan paid = (await server.Context.Database.Plans.EnumerateAsync(new PlanFilter { Key = "paid-5" }, token).ConfigureAwait(false)).Objects.Single();
            BillingSubscription subscription = new BillingSubscription
            {
                TenantId = owner.User.TenantId, UserId = owner.User.Id, Provider = "Apple", Environment = "Production",
                ProviderId = "original-" + Guid.NewGuid().ToString("N"), PlanId = paid.Id, Status = "Active", EndsUtc = DateTime.UtcNow.AddDays(20), LastVerifiedUtc = DateTime.UtcNow
            };
            await server.Context.Database.BillingSubscriptions.SaveVerifiedAsync(subscription, token).ConfigureAwait(false);
            EffectivePlan plan = await server.Context.Entitlements.GetEffectiveAsync(owner.User.TenantId, owner.User.Id, token).ConfigureAwait(false);
            Assert.True(plan.Plan.Key == "paid-5" && plan.BillingProvider == "Apple", "verified subscription grants plan");
            subscription.TenantId = attacker.User.TenantId; subscription.UserId = attacker.User.Id;
            bool refused = false;
            try { await server.Context.Database.BillingSubscriptions.SaveVerifiedAsync(subscription, token).ConfigureAwait(false); }
            catch (InvalidOperationException) { refused = true; }
            Assert.True(refused, "purchase cannot move to another account");
            subscription.TenantId = owner.User.TenantId; subscription.UserId = owner.User.Id;
            subscription.Status = "Revoked"; subscription.LastVerifiedUtc = DateTime.UtcNow;
            await server.Context.Database.BillingSubscriptions.SaveVerifiedAsync(subscription, token).ConfigureAwait(false);
            subscription.Status = "Active"; subscription.LastVerifiedUtc = DateTime.UtcNow.AddMinutes(-10);
            Assert.True(await server.Context.Database.BillingSubscriptions.SaveVerifiedAsync(subscription, token).ConfigureAwait(false) == null, "older response ignored");
            plan = await server.Context.Entitlements.GetEffectiveAsync(owner.User.TenantId, owner.User.Id, token).ConfigureAwait(false);
            Assert.True(plan.BillingProvider == null, "revocation removed paid access");
            subscription.LastVerifiedUtc = DateTime.UtcNow;
            await server.Context.Database.BillingSubscriptions.SaveVerifiedAsync(subscription, token).ConfigureAwait(false);
            Plan catalog = (await server.Context.Database.Plans.EnumerateAsync(new PlanFilter { Key = "catalog-only" }, token).ConfigureAwait(false)).Objects.Single();
            await server.Context.Database.PlanAssignments.CreateAsync(new PlanAssignment { TenantId = owner.User.TenantId, UserId = owner.User.Id, PlanId = catalog.Id, Source = PlanSourceEnum.Admin, StartsUtc = DateTime.UtcNow.AddMinutes(-1) }, token).ConfigureAwait(false);
            plan = await server.Context.Entitlements.GetEffectiveAsync(owner.User.TenantId, owner.User.Id, token).ConfigureAwait(false);
            Assert.True(plan.Plan.Key == "catalog-only" && plan.BillingProvider == null, "explicit administrator assignment takes precedence");
        }

        private static async Task StripeSimulationAsync(CancellationToken token)
        {
            string simulation = Environment.GetEnvironmentVariable("MYMORNINGNEWS_BILLING_SIM_URL")!;
            TestApiClient dependency = new TestApiClient(simulation);
            (await dependency.PostAsync("/__control", new { reset = true, failAfterWrite = true }, token).ConfigureAwait(false)).Expect(200, "simulation reset");
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            BillingSettings originalSettings = server.Context.Settings.Billing;
            BillingService originalService = server.Context.Billing;
            BillingSettings settings = new BillingSettings { Enabled = true, AllowSandbox = true, Stripe = new StripeSettings { SecretKey = "sk_test_simulation", WebhookSecret = "whsec_simulation", ApiBaseUrl = simulation } };
            settings.Offers[0].StripePriceId = "price_paid_5_month";
            using (HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
            {
                try
                {
                    server.Context.Settings.Billing = settings;
                    server.Context.Billing = new BillingService(server.Context.Database, () => settings, new StripeClient(settings.Stripe, http), new AppStoreClient(settings.Apple, http), new GooglePlayClient(settings.Google, http), server.Context.Settings.Auth.SigningKey);
                    TestUser user = await UserAsync(token).ConfigureAwait(false);
                    (await user.Client.PostAsync("/v1.0/api/account/billing/checkout", new { offerKey = "paid-5-month" }, token).ConfigureAwait(false)).Expect(502, "ambiguous provider failure after session creation");
                    (await user.Client.PostAsync("/v1.0/api/account/billing/checkout", new { offerKey = "paid-5-month" }, token).ConfigureAwait(false)).Expect(200, "idempotent retry");
                    JsonElement state = (await dependency.GetAsync("/__state", token).ConfigureAwait(false)).Expect(200, "simulation evidence").Json<JsonElement>();
                    Assert.Equal(1, state.GetProperty("sessions").EnumerateObject().Count(), "one Stripe session across retry");
                    string id = state.GetProperty("subscriptions").EnumerateObject().Single().Name;
                    EffectivePlan before = await server.Context.Entitlements.GetEffectiveAsync(user.User.TenantId, user.User.Id, token).ConfigureAwait(false);
                    Assert.True(before.BillingProvider == null, "checkout redirect cannot grant access");
                    (await dependency.PostAsync("/__control", new { subscriptionId = id, patch = new { status = "active" } }, token).ConfigureAwait(false)).Expect(200, "provider confirms payment");
                    TestApiClient anonymous = new TestApiClient(user.Client.BaseUrl);
                    string payload = Event("evt_active", id);
                    (await anonymous.SendAsync(HttpMethod.Post, "/v1.0/public/billing/stripe", payload, new Dictionary<string, string> { { "Stripe-Signature", "t=1,v1=bad" } }, token).ConfigureAwait(false)).Expect(400, "forged event rejected");
                    await SendEventAsync(anonymous, payload, token).ConfigureAwait(false);
                    await SendEventAsync(anonymous, payload, token).ConfigureAwait(false);
                    await server.Context.Billing.ProcessAsync(token).ConfigureAwait(false);
                    EffectivePlan active = await server.Context.Entitlements.GetEffectiveAsync(user.User.TenantId, user.User.Id, token).ConfigureAwait(false);
                    Assert.True(active.Plan.Key == "paid-5" && active.BillingProvider == "Stripe", "verified active subscription grants access");
                    Assert.Equal(1L, await server.Context.Database.BillingEvents.CountAsync(new BillingEventFilter { EventId = "evt_active" }, token).ConfigureAwait(false), "duplicate delivery deduplicated");
                    (await user.Client.PostAsync("/v1.0/api/account/billing/checkout", new { offerKey = "paid-5-month" }, token).ConfigureAwait(false)).Expect(409, "duplicate purchase blocked");
                    (await user.Client.PostAsync("/v1.0/api/account/billing/portal", null, token).ConfigureAwait(false)).Expect(200, "account-bound portal");
                    foreach (string stateName in new[] { "unpaid", "refunded", "disputed", "won", "discounted" })
                    {
                        object patch = stateName == "unpaid" ? new { invoiceStatus = "open" } : stateName == "refunded" ? new { invoiceStatus = "paid", refunded = 1000 } : stateName == "disputed" ? new { refunded = 0, dispute = "needs_response" } : stateName == "won" ? new { dispute = "won" } : new { zeroInvoice = true, dispute = "" };
                        (await dependency.PostAsync("/__control", new { subscriptionId = id, patch }, token).ConfigureAwait(false)).Expect(200, "payment state changed");
                        string chargeEvent = Serializer.Serialize(new { id = "evt_" + stateName, type = "charge.refunded", livemode = false, data = new { @object = new { id = "ch_" + id.Substring(4) } } });
                        await SendEventAsync(anonymous, chargeEvent, token).ConfigureAwait(false);
                        await server.Context.Billing.ProcessAsync(token).ConfigureAwait(false);
                        EffectivePlan checkedPlan = await server.Context.Entitlements.GetEffectiveAsync(user.User.TenantId, user.User.Id, token).ConfigureAwait(false);
                        Assert.True((checkedPlan.BillingProvider == "Stripe") == (stateName == "won" || stateName == "discounted"), "payment reconciliation for " + stateName);
                    }
                    (await dependency.PostAsync("/__control", new { subscriptionId = id, patch = new { status = "past_due" }, failures = true }, token).ConfigureAwait(false)).Expect(200, "provider outage");
                    await SendEventAsync(anonymous, Event("evt_failure", id), token).ConfigureAwait(false);
                    await server.Context.Billing.ProcessAsync(token).ConfigureAwait(false);
                    BillingEvent retry = (await server.Context.Database.BillingEvents.EnumerateAsync(new BillingEventFilter { EventId = "evt_failure" }, token).ConfigureAwait(false)).Objects.Single();
                    Assert.True(retry.Status == "Pending" && retry.Attempts == 1 && retry.Error != null, "provider failure retained for retry");
                    (await dependency.PostAsync("/__control", new { failures = false }, token).ConfigureAwait(false)).Expect(200, "provider recovery");
                    retry.RetryUtc = DateTime.UtcNow.AddSeconds(-1);
                    await server.Context.Database.BillingEvents.UpdateAsync(retry, token).ConfigureAwait(false);
                    await server.Context.Billing.ProcessAsync(token).ConfigureAwait(false);
                    await SendEventAsync(anonymous, Event("evt_delayed_active", id), token).ConfigureAwait(false);
                    await server.Context.Billing.ProcessAsync(token).ConfigureAwait(false);
                    EffectivePlan suspended = await server.Context.Entitlements.GetEffectiveAsync(user.User.TenantId, user.User.Id, token).ConfigureAwait(false);
                    Assert.True(suspended.BillingProvider == null, "delayed active event cannot override current past-due state");
                    TestUser offered = await UserAsync(token).ConfigureAwait(false);
                    Plan custom = (await server.Context.Database.Plans.EnumerateAsync(new PlanFilter { Key = "complimentary-five" }, token).ConfigureAwait(false)).Objects.Single();
                    PromoCode offerCode = await server.Context.Database.PromoCodes.CreateAsync(new PromoCode { Code = "OFFER-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant(), PlanId = custom.Id, BenefitKind = "PaidOffer", BillingPlanKey = "paid-5", DiscountPercent = 25.5m, DiscountMonths = 3, NewAccountsOnly = true }, token).ConfigureAwait(false);
                    await server.Context.PromoCodes.RedeemAsync(offered.User.TenantId, offered.User.Id, offerCode.Code, PromoChannelEnum.Signup, true, token).ConfigureAwait(false);
                    (await offered.Client.PostAsync("/v1.0/api/account/billing/checkout", new { offerKey = "paid-5-month" }, token).ConfigureAwait(false)).Expect(200, "discounted checkout");
                    state = (await dependency.GetAsync("/__state", token).ConfigureAwait(false)).Json<JsonElement>();
                    Assert.Equal(1, state.GetProperty("coupons").EnumerateObject().Count(), "one account-specific coupon");
                    string couponForm = state.GetProperty("coupons").EnumerateObject().Single().Value.GetProperty("form").GetString()!;
                    Assert.True(couponForm.Contains("percent_off=25.5", StringComparison.Ordinal) && couponForm.Contains("duration_in_months=3", StringComparison.Ordinal), "captured percentage and duration sent to provider");
                    string offeredId = state.GetProperty("subscriptions").EnumerateObject().Single(x => x.Name != id).Name;
                    (await dependency.PostAsync("/__control", new { subscriptionId = offeredId, patch = new { status = "active" } }, token).ConfigureAwait(false)).Expect(200, "discount purchase paid");
                    await SendEventAsync(anonymous, Event("evt_signup_offer", offeredId), token).ConfigureAwait(false);
                    await server.Context.Billing.ProcessAsync(token).ConfigureAwait(false);
                    EffectivePlan customPlan = await server.Context.Entitlements.GetEffectiveAsync(offered.User.TenantId, offered.User.Id, token).ConfigureAwait(false);
                    Assert.True(customPlan.Plan.Id == custom.Id && customPlan.BillingProvider == "Stripe", "verified checkout metadata binds custom tier");

                    (await dependency.PostAsync("/__control", new { subscriptionId = offeredId, patch = new { metadata = new { userId = "another-application-user" } } }, token).ConfigureAwait(false)).Expect(200, "foreign application metadata");
                    await SendEventAsync(anonymous, Event("evt_foreign_application", offeredId), token).ConfigureAwait(false);
                    await server.Context.Billing.ProcessAsync(token).ConfigureAwait(false);
                    BillingEvent ignored = (await server.Context.Database.BillingEvents.EnumerateAsync(new BillingEventFilter { EventId = "evt_foreign_application" }, token).ConfigureAwait(false)).Objects.Single();
                    Assert.Equal("Completed", ignored.Status, "authenticated unrelated purchase is ignored instead of retrying forever");

                }
                finally { server.Context.Billing = originalService; server.Context.Settings.Billing = originalSettings; }
            }
        }

        private static string Event(string eventId, string subscriptionId)
        {
            return Serializer.Serialize(new { id = eventId, type = "customer.subscription.updated", livemode = false, created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), data = new { @object = new { id = subscriptionId, @object = "subscription", status = "active" } } });
        }

        private static async Task SendEventAsync(TestApiClient client, string payload, CancellationToken token)
        {
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
            string signature;
            using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes("whsec_simulation")))
                signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(timestamp + "." + payload))).ToLowerInvariant();
            (await client.SendAsync(HttpMethod.Post, "/v1.0/public/billing/stripe", payload, new Dictionary<string, string> { { "Stripe-Signature", "t=" + timestamp + ",v1=" + signature } }, token).ConfigureAwait(false)).Expect(200, "signed callback accepted durably");
        }
    }
}
