namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Billing;
    using MyMorningNews.Core.Entitlements;
    using MyMorningNews.Core.Integrations.Implementations;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Serialization;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Server;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>Opt-in real Stripe test-mode scenario. Never accepts a live key; creates and cleans up only its own test objects.</summary>
    public static class StripeSandboxScenario
    {
        /// <summary>Run with MYMORNINGNEWS_STRIPE_SANDBOX_KEY, PRICE and PORTAL environment values. No real cards or charges are used.</summary>
        public static async Task RunAsync(CancellationToken token)
        {
            string key = Environment.GetEnvironmentVariable("MYMORNINGNEWS_STRIPE_SANDBOX_KEY") ?? String.Empty;
            string price = Environment.GetEnvironmentVariable("MYMORNINGNEWS_STRIPE_SANDBOX_PRICE") ?? String.Empty;
            if (!key.StartsWith("sk_test_", StringComparison.Ordinal) || !price.StartsWith("price_", StringComparison.Ordinal))
                throw new InvalidOperationException("This scenario requires a Stripe test secret key and a sandbox price.");
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant tenant = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser user = await TestFixtures.CreateUserAsync(admin, tenant.Id, false, token).ConfigureAwait(false);
            BillingSettings settings = new BillingSettings { Enabled = true, AllowSandbox = true,
                Stripe = new StripeSettings { SecretKey = key, WebhookSecret = "whsec_sandbox_local_callback_fixture", PortalConfigurationId = Environment.GetEnvironmentVariable("MYMORNINGNEWS_STRIPE_SANDBOX_PORTAL") ?? String.Empty } };
            settings.Offers[0].StripePriceId = price;
            BillingSettings originalSettings = server.Context.Settings.Billing;
            BillingService originalService = server.Context.Billing;
            string? customerId = null;
            string? subscriptionId = null;
            string? checkoutId = null;
            using (HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) })
            {
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
                http.DefaultRequestHeaders.Add("Stripe-Version", "2025-09-30.clover");
                try
                {
                    BillingService billing = new BillingService(server.Context.Database, () => settings, new StripeClient(settings.Stripe, http), new AppStoreClient(settings.Apple, http), new GooglePlayClient(settings.Google, http), server.Context.Settings.Auth.SigningKey);
                    server.Context.Settings.Billing = settings;
                    server.Context.Billing = billing;
                    BillingCheckout checkout = await billing.CheckoutAsync(user.User.TenantId, user.User.Id, "paid-5-month", token).ConfigureAwait(false);
                    checkoutId = checkout.SessionId;
                    Assert.True(checkout.CheckoutUrl?.StartsWith("https://checkout.stripe.com/", StringComparison.Ordinal) == true, "real sandbox checkout URL");
                    BillingCheckout repeated = await billing.CheckoutAsync(user.User.TenantId, user.User.Id, "paid-5-month", token).ConfigureAwait(false);
                    Assert.Equal(checkout.SessionId, repeated.SessionId, "retry reuses provider session");
                    EffectivePlan unpaid = await server.Context.Entitlements.GetEffectiveAsync(user.User.TenantId, user.User.Id, token).ConfigureAwait(false);
                    Assert.True(unpaid.BillingProvider == null, "unpaid checkout grants nothing");
                    BillingAccount binding = await billing.AccountAsync(user.User.TenantId, user.User.Id, token).ConfigureAwait(false);
                    JsonElement customer = await CallAsync(http, HttpMethod.Post, "/customers", new Dictionary<string, string>
                    { { "name", "My Morning News automated sandbox validation" }, { "metadata[app]", "mymorningnews-test" }, { "payment_method", "pm_card_visa" }, { "invoice_settings[default_payment_method]", "pm_card_visa" } }, token).ConfigureAwait(false);
                    customerId = customer.GetProperty("id").GetString()!;
                    JsonElement purchased = await CallAsync(http, HttpMethod.Post, "/subscriptions", new Dictionary<string, string>
                    { { "customer", customerId }, { "items[0][price]", price }, { "metadata[userId]", binding.PurchaseAccountToken }, { "payment_behavior", "error_if_incomplete" } }, token).ConfigureAwait(false);
                    subscriptionId = purchased.GetProperty("id").GetString()!;
                    Assert.True(!purchased.GetProperty("livemode").GetBoolean(), "provider confirms test mode");
                    await ReconcileAsync(billing, subscriptionId, "paid", token).ConfigureAwait(false);
                    EffectivePlan paid = await server.Context.Entitlements.GetEffectiveAsync(user.User.TenantId, user.User.Id, token).ConfigureAwait(false);
                    Assert.True(paid.Plan.Key == "paid-5" && paid.BillingProvider == "Stripe", "real paid invoice grants package");
                    Assert.True((await billing.PortalAsync(user.User.TenantId, user.User.Id, token).ConfigureAwait(false)).StartsWith("https://billing.stripe.com/", StringComparison.Ordinal), "real account-bound portal");
                    await billing.PrepareDeletionAsync(user.User.TenantId, user.User.Id, token).ConfigureAwait(false);
                    JsonElement closed = await CallAsync(http, HttpMethod.Get, "/checkout/sessions/" + checkoutId, null, token).ConfigureAwait(false);
                    Assert.Equal("expired", closed.GetProperty("status").GetString(), "account deletion expires unpaid checkout");
                    checkoutId = null;
                    await ReconcileAsync(billing, subscriptionId, "canceling", token).ConfigureAwait(false);
                    Assert.True((await server.Context.Entitlements.GetEffectiveAsync(user.User.TenantId, user.User.Id, token).ConfigureAwait(false)).BillingProvider == "Stripe", "period-end cancellation retains paid access");
                    string invoiceId = purchased.GetProperty("latest_invoice").GetString()!;
                    JsonElement payments = await CallAsync(http, HttpMethod.Get, "/invoice_payments?invoice=" + Uri.EscapeDataString(invoiceId) + "&status=paid", null, token).ConfigureAwait(false);
                    string intent = payments.GetProperty("data")[0].GetProperty("payment").GetProperty("payment_intent").GetString()!;
                    await CallAsync(http, HttpMethod.Post, "/refunds", new Dictionary<string, string> { { "payment_intent", intent } }, token).ConfigureAwait(false);
                    await ReconcileAsync(billing, subscriptionId, "refunded", token).ConfigureAwait(false);
                    Assert.True((await server.Context.Entitlements.GetEffectiveAsync(user.User.TenantId, user.User.Id, token).ConfigureAwait(false)).BillingProvider == null, "real full refund removes paid access");
                }
                finally
                {
                    server.Context.Billing = originalService;
                    server.Context.Settings.Billing = originalSettings;
                    if (checkoutId != null) await CallAsync(http, HttpMethod.Post, "/checkout/sessions/" + checkoutId + "/expire", new Dictionary<string, string>(), CancellationToken.None).ConfigureAwait(false);
                    if (subscriptionId != null) await CallAsync(http, HttpMethod.Delete, "/subscriptions/" + subscriptionId, null, CancellationToken.None).ConfigureAwait(false);
                    if (customerId != null) await CallAsync(http, HttpMethod.Delete, "/customers/" + customerId, null, CancellationToken.None).ConfigureAwait(false);
                }
            }
        }

        private static async Task ReconcileAsync(BillingService billing, string subscriptionId, string stage, CancellationToken token)
        {
            string payload = Serializer.Serialize(new { id = "evt_sandbox_" + stage + "_" + Guid.NewGuid().ToString("N"), type = "customer.subscription.updated", livemode = false, data = new { @object = new { id = subscriptionId } } });
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
            string signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("whsec_sandbox_local_callback_fixture"), Encoding.UTF8.GetBytes(timestamp + "." + payload))).ToLowerInvariant();
            await billing.StripeWebhookAsync(payload, "t=" + timestamp + ",v1=" + signature, token).ConfigureAwait(false);
            Assert.True(await billing.ProcessAsync(token).ConfigureAwait(false) > 0, "sandbox state reconciliation completed");
        }

        private static async Task<JsonElement> CallAsync(HttpClient http, HttpMethod method, string path, Dictionary<string, string>? form, CancellationToken token)
        {
            using (HttpRequestMessage request = new HttpRequestMessage(method, "https://api.stripe.com/v1" + path))
            {
                if (form != null) request.Content = new FormUrlEncodedContent(form);
                using (HttpResponseMessage response = await http.SendAsync(request, token).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Stripe sandbox fixture " + method.Method + " failed with HTTP " + (int)response.StatusCode + ".");
                    using (JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false))) return document.RootElement.Clone();
                }
            }
        }
    }
}
