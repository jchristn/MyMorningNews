namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Server.Services;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>Account-bound web/native purchases and provider-authenticated callbacks.</summary>
    public class BillingRoutes : RouteBase
    {
        /// <summary>Construct billing route registration.</summary>
        public BillingRoutes(ServerContext server) : base(server) { }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            const string path = "/v1.0/api/account/billing";
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, path, StatusAsync, null, openApiMetadata: Doc("My billing offers, account binding and verified subscriptions", "Billing"));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, path + "/checkout", CheckoutAsync, null, openApiMetadata: Doc("Start or resume web subscription checkout", "Billing", "No trial. Checkout success alone never grants access. One pending checkout per account.").WithRequestBody(Body<BillingCheckoutRequest>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, path + "/portal", PortalAsync, null, openApiMetadata: Doc("Open management for my verified Stripe customer", "Billing"));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, path + "/purchases", PurchaseAsync, null, openApiMetadata: Doc("Verify or restore my native subscription", "Billing", "The verified purchase account binding must match the signed-in account.").WithRequestBody(Body<BillingPurchaseRequest>()));
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.POST, "/v1.0/public/billing/stripe", StripeAsync, null, openApiMetadata: Doc("Stripe signed callback", "Billing callbacks", "Requires the original body and a valid Stripe-Signature header. Authenticated events enter a durable idempotent inbox.").WithRequestBody(Body<Dictionary<string, object>>()).WithParameter(OpenApiParameterMetadata.Header("Stripe-Signature", "Provider signature for the original raw request bytes.", true, OpenApiSchemaFactory.For(typeof(string)))));
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.POST, "/v1.0/public/billing/apple", AppleAsync, null, openApiMetadata: Doc("Apple signed subscription notification", "Billing callbacks").WithRequestBody(Body<AppleNotificationRequest>()));
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.POST, "/v1.0/public/billing/google", GoogleAsync, null, openApiMetadata: Doc("Google authenticated RTDN push", "Billing callbacks", "Requires a verified Google OIDC bearer with the configured audience and service-account email, and the configured package name.").WithRequestBody(Body<Dictionary<string, object>>()).WithParameter(OpenApiParameterMetadata.Header("Authorization", "Bearer Google-signed OIDC push token.", true, OpenApiSchemaFactory.For(typeof(string)))));
        }

        private async Task StatusAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.PromoCodeRedeem).ConfigureAwait(false)) return;
            BillingAccount account = await Server.Billing.AccountAsync(Context(ctx).TenantId!, Context(ctx).UserId!, ctx.Token).ConfigureAwait(false);
            List<BillingOfferResponse> offers = new List<BillingOfferResponse>();
            foreach (MyMorningNews.Core.Settings.BillingOfferSettings offer in Server.Settings.Billing.Offers.Where(o => o.Active))
            {
                Plan? package = (await Server.Database.Plans.EnumerateAsync(new PlanFilter { Key = offer.PlanKey, Active = true, PageSize = 1 }, ctx.Token).ConfigureAwait(false)).Items.FirstOrDefault();
                if (package == null) continue;
                List<PlanFeature> features = (await Server.Database.PlanFeatures.EnumerateAsync(new PlanFeatureFilter { PlanId = package.Id, PageSize = 500 }, ctx.Token).ConfigureAwait(false)).Items;
                offers.Add(new BillingOfferResponse { Key = offer.Key, PlanKey = offer.PlanKey, Interval = offer.Interval, UsdCents = offer.UsdCents, AppleProductId = offer.AppleProductId, GoogleProductId = offer.GoogleProductId, GoogleBasePlanId = offer.GoogleBasePlanId,
                    PlanName = package.Name,
                    CustomTopics = features.FirstOrDefault(f => f.Key == "customTopics.max")?.Value,
                    SystemSubscriptions = features.FirstOrDefault(f => f.Key == "systemSubscriptions.max")?.Value,
                    DailyAudioSeconds = features.FirstOrDefault(f => f.Key == "audio.dailySeconds.max")?.Value,
                    WebAvailable = Server.Settings.Billing.Enabled && !String.IsNullOrWhiteSpace(offer.StripePriceId) && !String.IsNullOrWhiteSpace(Server.Settings.Billing.Stripe.SecretKey)
                });
            }
            await SendJsonAsync(ctx, 200, new BillingStatusResponse
            {
                Enabled = Server.Settings.Billing.Enabled, PurchaseAccountToken = account.PurchaseAccountToken,
                AppleAvailable = !String.IsNullOrWhiteSpace(Server.Settings.Billing.Apple.BundleId) && !String.IsNullOrWhiteSpace(Server.Settings.Billing.Apple.PrivateKeyPem),
                GoogleAvailable = !String.IsNullOrWhiteSpace(Server.Settings.Billing.Google.PackageName) && !String.IsNullOrWhiteSpace(Server.Settings.Billing.Google.ServiceAccountJson),
                Offers = offers,
                SignupBenefit = await Server.Billing.BenefitAsync(account.TenantId, account.UserId, ctx.Token).ConfigureAwait(false),
                Subscriptions = (await Server.Billing.SubscriptionsAsync(account.TenantId, account.UserId, ctx.Token).ConfigureAwait(false)).Select(s => new BillingSubscriptionResponse
                { Id = s.Id, Provider = s.Provider, Environment = s.Environment, PlanId = s.PlanId, Status = s.Status, EndsUtc = s.EndsUtc, CancelAtPeriodEnd = s.CancelAtPeriodEnd, LastVerifiedUtc = s.LastVerifiedUtc }).ToList(),
                Plan = await Server.Entitlements.GetEffectiveAsync(account.TenantId, account.UserId, ctx.Token).ConfigureAwait(false)
            }).ConfigureAwait(false);
        }

        private async Task CheckoutAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.PromoCodeRedeem).ConfigureAwait(false)) return;
            BillingCheckoutRequest? request = await ReadBodyAsync<BillingCheckoutRequest>(ctx).ConfigureAwait(false);
            if (request == null) return;
            await GuardAsync(ctx, async () =>
            {
                BillingCheckout operation = await Server.Billing.CheckoutAsync(Context(ctx).TenantId!, Context(ctx).UserId!, request.OfferKey, ctx.Token).ConfigureAwait(false);
                await SendJsonAsync(ctx, 200, new BillingRedirectResponse { Id = operation.Id, Url = operation.CheckoutUrl ?? String.Empty, ExpiresUtc = operation.ExpiresUtc }).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task PortalAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.PromoCodeRedeem).ConfigureAwait(false)) return;
            await GuardAsync(ctx, async () => await SendJsonAsync(ctx, 200, new BillingRedirectResponse { Url = await Server.Billing.PortalAsync(Context(ctx).TenantId!, Context(ctx).UserId!, ctx.Token).ConfigureAwait(false) }).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task PurchaseAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.PromoCodeRedeem).ConfigureAwait(false)) return;
            BillingPurchaseRequest? request = await ReadBodyAsync<BillingPurchaseRequest>(ctx).ConfigureAwait(false);
            if (request == null) return;
            await GuardAsync(ctx, async () =>
            {
                string tenantId = Context(ctx).TenantId!;
                string userId = Context(ctx).UserId!;
                if (request.Provider == "Apple" && !String.IsNullOrWhiteSpace(request.SignedTransaction) && request.SignedTransaction.Length <= 65536)
                    await Server.Billing.ApplePurchaseAsync(tenantId, userId, request.SignedTransaction, ctx.Token).ConfigureAwait(false);
                else if (request.Provider == "Google" && !String.IsNullOrWhiteSpace(request.PurchaseToken))
                    await Server.Billing.GooglePurchaseAsync(tenantId, userId, request.PurchaseToken, ctx.Token).ConfigureAwait(false);
                else throw new ArgumentException("Supply Apple signedTransaction or Google purchaseToken.");
                await SendJsonAsync(ctx, 200, await Server.Entitlements.GetEffectiveAsync(tenantId, userId, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private Task StripeAsync(HttpContextBase ctx) => GuardAsync(ctx, async () =>
        {
            await Server.Billing.StripeWebhookAsync(Raw(ctx), ctx.Request.RetrieveHeaderValue("Stripe-Signature"), ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, new { accepted = true }).ConfigureAwait(false);
        });

        private async Task AppleAsync(HttpContextBase ctx)
        {
            AppleNotificationRequest? request = await ReadBodyAsync<AppleNotificationRequest>(ctx).ConfigureAwait(false);
            if (request == null) return;
            await GuardAsync(ctx, async () =>
            {
                if (request.SignedPayload.Length > 131072) throw new ArgumentException("Notification is too large.");
                await Server.Billing.AppleWebhookAsync(request.SignedPayload, ctx.Token).ConfigureAwait(false);
                await SendJsonAsync(ctx, 200, new { accepted = true }).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private Task GoogleAsync(HttpContextBase ctx) => GuardAsync(ctx, async () =>
        {
            await Server.Billing.GoogleWebhookAsync(Raw(ctx), ctx.Request.RetrieveHeaderValue("Authorization"), ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, new { accepted = true }).ConfigureAwait(false);
        });

        private static string Raw(HttpContextBase ctx)
        {
            string body = ctx.Request.DataAsString ?? String.Empty;
            if (body.Length > 262144) throw new ArgumentException("Notification is too large.");
            return body;
        }

        private static async Task GuardAsync(HttpContextBase ctx, Func<Task> action)
        {
            try { await action().ConfigureAwait(false); }
            catch (WebhookSignatureException) { await SendErrorAsync(ctx, 400, "InvalidSignature", "The provider signature could not be verified.").ConfigureAwait(false); }
            catch (StoreVerificationException) { await SendErrorAsync(ctx, 502, "PurchaseVerificationFailed", "The purchase could not be verified. Check the signed-in account and retry verification to refresh its status.").ConfigureAwait(false); }
            catch (IntegrationNotConfiguredException) { await SendErrorAsync(ctx, 503, "BillingUnavailable", "This billing provider is not configured.").ConfigureAwait(false); }
            catch (ArgumentException ex) { await SendErrorAsync(ctx, 400, "ValidationFailed", ex.Message).ConfigureAwait(false); }
            catch (InvalidOperationException ex) { await SendErrorAsync(ctx, 409, "BillingConflict", ex.Message).ConfigureAwait(false); }
        }
    }
}
