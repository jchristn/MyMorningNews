namespace MyMorningNews.Core.Billing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Integrations.Interfaces;
    using MyMorningNews.Core.Integrations.Models;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Security;
    using MyMorningNews.Core.Settings;

    /// <summary>
    /// Verified subscription lifecycle. Redirects and client product IDs never grant access. Provider callbacks enter
    /// a durable deduplicated inbox; reconciliation reads current provider state and binds immutable account ownership.
    /// Thread safe; database uniqueness and monotonic verification timestamps protect concurrent callers.
    /// </summary>
    public class BillingService
    {
        private readonly DatabaseDriverBase _Database;
        private readonly Func<BillingSettings> _Settings;
        private readonly IStripeClient _Stripe;
        private readonly IAppStoreClient _Apple;
        private readonly IGooglePlayClient _Google;
        private readonly CryptoProtector _Protector;

        /// <summary>Construct with provider clients and the server's encryption root. Clients may be injected for protocol tests.</summary>
        public BillingService(DatabaseDriverBase database, Func<BillingSettings> settings, IStripeClient stripe, IAppStoreClient apple, IGooglePlayClient google, string signingKey)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Stripe = stripe ?? throw new ArgumentNullException(nameof(stripe));
            _Apple = apple ?? throw new ArgumentNullException(nameof(apple));
            _Google = google ?? throw new ArgumentNullException(nameof(google));
            _Protector = new CryptoProtector(signingKey, "billing-provider-token");
        }

        /// <summary>Whether verified state grants access at the supplied UTC instant. Unknown, pending, trial, and suspended states fail closed.</summary>
        public static bool Entitled(BillingSubscription subscription, DateTime utcNow)
        {
            return subscription.Active && subscription.EndsUtc > utcNow &&
                (subscription.Status == "Active" || subscription.Status == "Grace" || subscription.Status == "Canceled");
        }

        /// <summary>Get or create the authenticated account's immutable native purchase binding.</summary>
        public Task<BillingAccount> AccountAsync(string tenantId, string userId, CancellationToken token = default)
        {
            return _Database.BillingAccounts.EnsureAsync(new BillingAccount { TenantId = tenantId, UserId = userId, PurchaseAccountToken = Guid.NewGuid().ToString("D") }, token);
        }

        /// <summary>Read this account's verified subscriptions, including expired and revoked records.</summary>
        public async Task<List<BillingSubscription>> SubscriptionsAsync(string tenantId, string userId, CancellationToken token = default)
        {
            return (await _Database.BillingSubscriptions.EnumerateAsync(new BillingSubscriptionFilter { TenantId = tenantId, UserId = userId, PageSize = 1000 }, token).ConfigureAwait(false)).Objects;
        }

        /// <summary>Captured signup offer for the account. An offer is informational until its verified subscription is bound.</summary>
        public async Task<SignupBenefit?> BenefitAsync(string tenantId, string userId, CancellationToken token = default)
        {
            return (await _Database.SignupBenefits.EnumerateAsync(new SignupBenefitFilter { TenantId = tenantId, UserId = userId, Active = true, PageSize = 1 }, token).ConfigureAwait(false)).Objects.FirstOrDefault(b => !b.RevokedUtc.HasValue);
        }

        /// <summary>Create or reuse one allowlisted Stripe checkout operation. Retries share the server-generated idempotency key.</summary>
        /// <exception cref="InvalidOperationException">Purchases are disabled, another offer is pending, or an entitled subscription already exists.</exception>
        public async Task<BillingCheckout> CheckoutAsync(string tenantId, string userId, string offerKey, CancellationToken token = default)
        {
            BillingSettings settings = _Settings();
            if (!settings.Enabled || !_Stripe.IsConfigured) throw new InvalidOperationException("Web purchases are not enabled on this server.");
            BillingOfferSettings offer = settings.Offers.SingleOrDefault(o => o.Key == offerKey && o.Active && !String.IsNullOrWhiteSpace(o.StripePriceId))
                ?? throw new ArgumentException("Select an available subscription offer.", nameof(offerKey));
            await PlanAsync(offer.PlanKey, token).ConfigureAwait(false);
            List<BillingSubscription> subscriptions = await SubscriptionsAsync(tenantId, userId, token).ConfigureAwait(false);
            if (subscriptions.Any(s => Entitled(s, DateTime.UtcNow) || (s.Active && s.Status == "Suspended"))) throw new InvalidOperationException("Manage the existing subscription through its original payment provider before starting another.");
            BillingAccount account = await AccountAsync(tenantId, userId, token).ConfigureAwait(false);
            User user = await _Database.Users.ReadAsync(tenantId, userId, token).ConfigureAwait(false) ?? throw new KeyNotFoundException("Account not found.");
            SignupBenefit? benefit = await BenefitAsync(tenantId, userId, token).ConfigureAwait(false);
            if (benefit?.RedeemedSubscriptionId != null || benefit?.BillingPlanKey != offer.PlanKey) benefit = null;
            BillingCheckout operation = await _Database.BillingCheckouts.ReserveAsync(new BillingCheckout
            {
                TenantId = tenantId, UserId = userId, OfferKey = offerKey, ExpiresUtc = DateTime.UtcNow.AddHours(1),
                PriceId = offer.StripePriceId, CustomerEmail = user.Email, ReturnUrl = ReturnUrl(settings), SignupBenefitId = benefit?.Id
            }, token).ConfigureAwait(false);
            if (operation.OfferKey != offerKey) throw new InvalidOperationException("A checkout for another offer is already open. Complete it or wait until it expires before choosing a different offer.");
            if (!String.IsNullOrEmpty(operation.CheckoutUrl)) return operation;
            if (String.IsNullOrWhiteSpace(operation.PriceId) || String.IsNullOrWhiteSpace(operation.ReturnUrl)) throw new InvalidOperationException("This older checkout has expired configuration. Wait until it expires before starting another.");
            benefit = operation.SignupBenefitId == null ? null : await _Database.SignupBenefits.ReadAsync(tenantId, operation.SignupBenefitId, token).ConfigureAwait(false);
            if (benefit?.RevokedUtc.HasValue == true || benefit?.Active == false) throw new InvalidOperationException("This signup offer was revoked. Wait for the current checkout to expire before choosing a standard offer.");
            string? couponId = null;
            if (benefit?.DiscountPercent > 0)
                couponId = await _Stripe.CreateCouponAsync(new StripeCouponRequest
                {
                    Name = "My Morning News signup offer", PercentOff = benefit.DiscountPercent,
                    Forever = !benefit.DiscountMonths.HasValue, DurationMonths = benefit.DiscountMonths,
                    SignupCodeId = benefit.Id, IdempotencyKey = operation.Id + ":coupon"
                }, token).ConfigureAwait(false);
            string returnUrl = operation.ReturnUrl;
            StripeCheckoutSession checkout = await _Stripe.CreateCheckoutSessionAsync(new StripeCheckoutRequest
            {
                UserId = account.PurchaseAccountToken, Email = operation.CustomerEmail, PriceId = operation.PriceId,
                SignupCodeId = benefit?.Id, CouponId = couponId,
                SuccessUrl = returnUrl + "?checkout=returned", CancelUrl = returnUrl + "?checkout=canceled",
                IdempotencyKey = operation.Id, ExpiresUtc = operation.ExpiresUtc
            }, token).ConfigureAwait(false);
            operation.SessionId = checkout.Id;
            operation.CheckoutUrl = checkout.Url;
            return await _Database.BillingCheckouts.UpdateAsync(operation, token).ConfigureAwait(false);
        }

        /// <summary>Create a management portal for this account's verified Stripe customer; caller-supplied customers or return URLs are not accepted.</summary>
        public async Task<string> PortalAsync(string tenantId, string userId, CancellationToken token = default)
        {
            BillingSubscription subscription = (await SubscriptionsAsync(tenantId, userId, token).ConfigureAwait(false))
                .Where(s => s.Provider == "Stripe" && !String.IsNullOrWhiteSpace(s.CustomerId)).OrderByDescending(s => s.LastVerifiedUtc).FirstOrDefault()
                ?? throw new InvalidOperationException("This account has no Stripe subscription. Manage native purchases in the store where they were purchased.");
            return await _Stripe.CreatePortalSessionAsync(subscription.CustomerId!, ReturnUrl(_Settings()), token).ConfigureAwait(false);
        }

        /// <summary>Verify a StoreKit transaction, fetch authoritative renewal state, and bind it to the signed-in account. Restore follows the same ownership checks.</summary>
        public async Task<BillingSubscription?> ApplePurchaseAsync(string tenantId, string userId, string signedTransaction, CancellationToken token = default)
        {
            AppStoreTransaction purchase = await _Apple.VerifyTransactionAsync(signedTransaction, token).ConfigureAwait(false);
            BillingAccount account = await AccountAsync(tenantId, userId, token).ConfigureAwait(false);
            RequireOwner(account, purchase.AppAccountToken?.ToString("D"));
            return await ReconcileAppleAsync(purchase.OriginalTransactionId, purchase.Environment.ToString(), account, token).ConfigureAwait(false);
        }

        /// <summary>Verify a Google Play purchase and acknowledge it only after durable entitlement persistence. Tokens are encrypted at rest.</summary>
        public Task<BillingSubscription?> GooglePurchaseAsync(string tenantId, string userId, string purchaseToken, CancellationToken token = default)
        {
            return ReconcileGoogleAsync(purchaseToken, tenantId, userId, token);
        }

        /// <summary>Authenticate a raw Stripe event and enqueue its subscription reference once. Unrelated valid events are acknowledged without granting access.</summary>
        public async Task StripeWebhookAsync(string rawBody, string? signature, CancellationToken token = default)
        {
            StripeEvent item = _Stripe.ParseWebhook(rawBody, signature, DateTime.UtcNow);
            string environment = item.LiveMode ? "Production" : "Sandbox";
            RequireEnvironment(environment);
            if (!String.IsNullOrWhiteSpace(item.SubscriptionId))
                await EnqueueAsync("Stripe", environment, item.Id, item.SubscriptionId, token).ConfigureAwait(false);
            else if (!String.IsNullOrWhiteSpace(item.ChargeId))
                await EnqueueAsync("Stripe", environment, item.Id, "charge:" + item.ChargeId, token).ConfigureAwait(false);
        }

        /// <summary>Authenticate an Apple notification and enqueue current-state reconciliation for its verified transaction lineage.</summary>
        public async Task AppleWebhookAsync(string signedPayload, CancellationToken token = default)
        {
            AppStoreNotification item = await _Apple.DecodeNotificationAsync(signedPayload, token).ConfigureAwait(false);
            RequireEnvironment(item.Environment.ToString());
            if (item.Transaction != null)
                await EnqueueAsync("Apple", item.Environment.ToString(), item.NotificationUuid, item.Transaction.OriginalTransactionId, token).ConfigureAwait(false);
        }

        /// <summary>Verify Google's OIDC push identity and package. A first purchase is durably bound before its callback is accepted.</summary>
        public async Task GoogleWebhookAsync(string rawBody, string? authorization, CancellationToken token = default)
        {
            GooglePlayNotification item = await _Google.DecodeNotificationAsync(rawBody, authorization, token).ConfigureAwait(false);
            if (item.IsTest || String.IsNullOrWhiteSpace(item.PurchaseToken)) return;
            BillingSubscription? subscription = await ReconcileGoogleAsync(item.PurchaseToken, null, null, token).ConfigureAwait(false);
            if (subscription != null) await EnqueueAsync("Google", subscription.Environment, item.MessageId, subscription.ProviderId, token).ConfigureAwait(false);
        }

        /// <summary>Process up to 20 leased inbox events; retry failed provider reads with bounded exponential backoff. Returns completed events.</summary>
        public async Task<int> ProcessAsync(CancellationToken token = default)
        {
            int completed = 0;
            for (int i = 0; i < 20; i++)
            {
                BillingEvent? item = await _Database.BillingEvents.ClaimAsync(token).ConfigureAwait(false);
                if (item == null) break;
                try
                {
                    if (item.Provider == "Stripe")
                    {
                        if (item.ProviderId.StartsWith("charge:", StringComparison.Ordinal))
                        {
                            string? customer = await _Stripe.GetChargeCustomerAsync(item.ProviderId.Substring(7), item.Environment == "Production", token).ConfigureAwait(false);
                            if (customer != null)
                                foreach (BillingSubscription affected in await _Database.BillingSubscriptions.StripeCustomerAsync(item.Environment, customer, token).ConfigureAwait(false))
                                    await ReconcileStripeAsync(affected.ProviderId, item.Environment, token).ConfigureAwait(false);
                        }
                        else await ReconcileStripeAsync(item.ProviderId, item.Environment, token).ConfigureAwait(false);
                    }
                    else if (item.Provider == "Apple") await ReconcileAppleAsync(item.ProviderId, item.Environment, null, token).ConfigureAwait(false);
                    else if (item.Provider == "Google")
                    {
                        BillingSubscription existing = await _Database.BillingSubscriptions.ReadExternalAsync("Google", item.Environment, item.ProviderId, token).ConfigureAwait(false)
                            ?? throw new StoreVerificationException("No bound Google subscription was found.");
                        string secret = _Protector.Unprotect(existing.VerificationSecretEncrypted ?? String.Empty)
                            ?? throw new StoreVerificationException("The stored purchase credential cannot be decrypted.");
                        await ReconcileGoogleAsync(secret, existing.TenantId, existing.UserId, token).ConfigureAwait(false);
                    }
                    else throw new StoreVerificationException("Unsupported billing provider.");
                    item.Status = "Completed";
                    item.Error = null;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    item.Status = "Pending";
                    item.RetryUtc = DateTime.UtcNow.AddSeconds(Math.Min(3600, Math.Pow(2, Math.Min(10, item.Attempts)) * 15));
                    item.Error = ex.GetType().Name + ": reconciliation will retry; existing access expires at its verified period end.";
                }
                bool finished = await _Database.BillingEvents.FinishClaimAsync(item, token).ConfigureAwait(false);
                if (finished && item.Status == "Completed") completed++;
            }
            return completed;
        }

        /// <summary>Recover missed callbacks by enqueuing stale subscriptions for current provider verification.</summary>
        public async Task QueueRecoveryAsync(CancellationToken token = default)
        {
            List<BillingSubscription> due = await _Database.BillingSubscriptions.DueAsync(DateTime.UtcNow.AddMinutes(-10), token).ConfigureAwait(false);
            string bucket = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 600).ToString(System.Globalization.CultureInfo.InvariantCulture);
            foreach (BillingSubscription item in due)
                await EnqueueAsync(item.Provider, item.Environment, "recovery:" + item.Id + ":" + bucket, item.ProviderId, token).ConfigureAwait(false);
        }

        /// <summary>Stop Stripe and Google renewals before deleting ownership data. Apple subscriptions must be managed by their owner in App Store settings.</summary>
        public async Task PrepareDeletionAsync(string tenantId, string userId, CancellationToken token = default)
        {
            BillingAccount account = await AccountAsync(tenantId, userId, token).ConfigureAwait(false);
            List<BillingCheckout> checkouts = (await _Database.BillingCheckouts.EnumerateAsync(new BillingCheckoutFilter { TenantId = tenantId, UserId = userId, Status = "Pending", Active = true, PageSize = 1000 }, token).ConfigureAwait(false)).Objects;
            foreach (BillingCheckout checkout in checkouts)
            {
                if (String.IsNullOrWhiteSpace(checkout.SessionId) && checkout.ExpiresUtc > DateTime.UtcNow)
                    throw new InvalidOperationException("A checkout is being prepared. Wait for it to finish or expire before deleting the account.");
                if (!String.IsNullOrWhiteSpace(checkout.SessionId))
                {
                    string? purchased = await _Stripe.CloseCheckoutAsync(checkout.SessionId, account.PurchaseAccountToken, token).ConfigureAwait(false);
                    if (purchased != null) await ReconcileStripeAsync(purchased, _Settings().Stripe.SecretKey.StartsWith("sk_test_", StringComparison.Ordinal) ? "Sandbox" : "Production", token).ConfigureAwait(false);
                }
                checkout.Status = "Expired";
                checkout.Active = false;
                await _Database.BillingCheckouts.UpdateAsync(checkout, token).ConfigureAwait(false);
            }
            List<BillingSubscription> subscriptions = await SubscriptionsAsync(tenantId, userId, token).ConfigureAwait(false);
            foreach (BillingSubscription subscription in subscriptions.Where(s => s.Active && s.Status != "Expired"))
            {
                if (subscription.Provider == "Stripe")
                {
                    StripeSubscription current = await _Stripe.GetSubscriptionAsync(subscription.ProviderId, token).ConfigureAwait(false);
                    RequireOwner(account, current.UserId);
                    if (current.Status != "canceled" && !current.CancelAtPeriodEnd)
                        await _Stripe.CancelAtPeriodEndAsync(subscription.ProviderId, token).ConfigureAwait(false);
                    await ReconcileStripeAsync(subscription.ProviderId, subscription.Environment, token).ConfigureAwait(false);
                }
                else if (subscription.Provider == "Google")
                {
                    string secret = _Protector.Unprotect(subscription.VerificationSecretEncrypted ?? String.Empty)
                        ?? throw new StoreVerificationException("The stored purchase credential cannot be decrypted.");
                    GooglePlaySubscription current = await _Google.GetSubscriptionAsync(secret, token).ConfigureAwait(false);
                    RequireOwner(account, current.ObfuscatedAccountId);
                    await _Google.CancelAsync(secret, token).ConfigureAwait(false);
                    await ReconcileGoogleAsync(secret, tenantId, userId, token).ConfigureAwait(false);
                }
            }
        }

        private async Task<BillingSubscription?> ReconcileStripeAsync(string id, string environment, CancellationToken token)
        {
            DateTime started = DateTime.UtcNow;
            StripeSubscription verified = await _Stripe.GetSubscriptionAsync(id, token).ConfigureAwait(false);
            if ((verified.LiveMode ? "Production" : "Sandbox") != environment || verified.Id != id) throw new StoreVerificationException("Stripe subscription identity or environment mismatch.");
            RequireEnvironment(environment);
            BillingAccount? account = await FindBoundAccountAsync(verified.UserId, token).ConfigureAwait(false);
            if (account == null) return null;
            BillingOfferSettings offer = _Settings().Offers.SingleOrDefault(o => !String.IsNullOrWhiteSpace(o.StripePriceId) && o.StripePriceId == verified.PriceId)
                ?? throw new StoreVerificationException("Stripe price has no configured entitlement mapping.");
            string status = verified.Status == "active" ? (verified.CancelAtPeriodEnd ? "Canceled" : "Active")
                : verified.Status == "canceled" ? "Expired" : verified.Status == "past_due" || verified.Status == "unpaid" ? "Suspended" : "Pending";
            if (verified.Status == "active" && verified.PaymentState != "Paid") status = verified.PaymentState == "Refunded" ? "Revoked" : "Suspended";
            BillingSubscription? saved = await SaveAsync(account, offer.PlanKey, "Stripe", environment, id, status, verified.CurrentPeriodEndUtc,
                verified.CancelAtPeriodEnd, started, verified.CustomerId, null, token).ConfigureAwait(false);
            if (saved != null && Entitled(saved, DateTime.UtcNow) && !String.IsNullOrWhiteSpace(verified.SignupBenefitId))
                await _Database.SignupBenefits.BindAsync(account.TenantId, account.UserId, verified.SignupBenefitId, saved.Id, offer.PlanKey, token).ConfigureAwait(false);
            return saved;
        }

        private async Task<BillingSubscription?> ReconcileAppleAsync(string id, string environment, BillingAccount? expected, CancellationToken token)
        {
            RequireEnvironment(environment);
            DateTime started = DateTime.UtcNow;
            AppStoreTransaction verified = await _Apple.GetLatestTransactionAsync(id, Enum.Parse<StoreEnvironmentEnum>(environment), token).ConfigureAwait(false)
                ?? throw new StoreVerificationException("Apple did not return this subscription lineage.");
            BillingAccount? account = await FindBoundAccountAsync(verified.AppAccountToken?.ToString("D"), token).ConfigureAwait(false);
            if (account == null && expected == null) return null;
            if (account == null) throw new StoreVerificationException("This purchase has no recognized account binding.");
            if (expected != null) RequireOwner(expected, account.PurchaseAccountToken);
            BillingOfferSettings offer = _Settings().Offers.SingleOrDefault(o => !String.IsNullOrWhiteSpace(o.AppleProductId) && o.AppleProductId == verified.ProductId)
                ?? throw new StoreVerificationException("Apple product has no configured entitlement mapping.");
            string status = verified.RevocationUtc.HasValue || verified.Status == 5 ? "Revoked" : verified.Status == 4 ? "Grace"
                : verified.Status == 1 ? (verified.AutoRenewEnabled ? "Active" : "Canceled") : verified.Status == 3 ? "Suspended" : "Expired";
            BillingSubscription? saved = await SaveAsync(account, offer.PlanKey, "Apple", environment, id, status,
                verified.Status == 4 ? verified.GraceExpiresUtc : verified.ExpiresUtc, !verified.AutoRenewEnabled, started, null, null, token).ConfigureAwait(false);
            await BindNativeBenefitAsync(account, saved, offer.PlanKey, verified.OfferId, token).ConfigureAwait(false);
            return saved;
        }

        private async Task<BillingSubscription?> ReconcileGoogleAsync(string purchaseToken, string? tenantId, string? userId, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(purchaseToken) || purchaseToken.Length > 8192) throw new ArgumentException("A valid purchase token is required.");
            DateTime started = DateTime.UtcNow;
            GooglePlaySubscription verified = await _Google.GetSubscriptionAsync(purchaseToken, token).ConfigureAwait(false);
            string environment = verified.Environment.ToString();
            RequireEnvironment(environment);
            BillingAccount? account = await FindBoundAccountAsync(verified.ObfuscatedAccountId, token).ConfigureAwait(false);
            if (account == null && tenantId == null) return null;
            if (account == null) throw new StoreVerificationException("This purchase has no recognized account binding.");
            if (tenantId != null && (account.TenantId != tenantId || account.UserId != userId)) throw new StoreVerificationException("This purchase belongs to a different account.");
            BillingOfferSettings offer = _Settings().Offers.SingleOrDefault(o => !String.IsNullOrWhiteSpace(o.GoogleProductId)
                && o.GoogleProductId == verified.ProductId && o.GoogleBasePlanId == verified.BasePlanId)
                ?? throw new StoreVerificationException("Google product and base plan have no configured entitlement mapping.");
            string status = verified.State switch
            {
                GooglePlaySubscriptionStateEnum.Active => "Active", GooglePlaySubscriptionStateEnum.InGracePeriod => "Grace",
                GooglePlaySubscriptionStateEnum.Canceled => "Canceled", GooglePlaySubscriptionStateEnum.Expired => "Expired",
                GooglePlaySubscriptionStateEnum.Pending or GooglePlaySubscriptionStateEnum.PendingPurchaseCanceled => "Pending",
                _ => "Suspended"
            };
            string? replacedId = null;
            if (!String.IsNullOrWhiteSpace(verified.LinkedPurchaseToken))
            {
                replacedId = Hash(verified.LinkedPurchaseToken);
                BillingSubscription? previousBinding = await _Database.BillingSubscriptions.ReadExternalAsync("Google", environment, replacedId, token).ConfigureAwait(false);
                if (previousBinding != null)
                {
                    if (previousBinding.TenantId != account.TenantId || previousBinding.UserId != account.UserId) throw new StoreVerificationException("The replaced subscription belongs to another account.");
                }
                else
                {
                    GooglePlaySubscription previous = await _Google.GetSubscriptionAsync(verified.LinkedPurchaseToken, token).ConfigureAwait(false);
                    if (previous.Environment != verified.Environment) throw new StoreVerificationException("The replaced purchase environment does not match.");
                    RequireOwner(account, previous.ObfuscatedAccountId);
                }
            }
            Plan plan = await PlanAsync(offer.PlanKey, token).ConfigureAwait(false);
            BillingSubscription? saved = await _Database.BillingSubscriptions.SaveReplacementAsync(new BillingSubscription
            {
                TenantId = account.TenantId, UserId = account.UserId, PlanId = plan.Id, Provider = "Google", Environment = environment,
                ProviderId = Hash(purchaseToken), Status = status, EndsUtc = verified.ExpiryUtc, CancelAtPeriodEnd = !verified.AutoRenewing,
                LastVerifiedUtc = started, VerificationSecretEncrypted = _Protector.Protect(purchaseToken)
            }, replacedId, token).ConfigureAwait(false);
            await BindNativeBenefitAsync(account, saved, offer.PlanKey, verified.OfferId, token).ConfigureAwait(false);
            if (saved != null && !verified.Acknowledged && Entitled(saved, DateTime.UtcNow))
                await _Google.AcknowledgeAsync(verified.ProductId, purchaseToken, token).ConfigureAwait(false);
            return saved;
        }

        private async Task BindNativeBenefitAsync(BillingAccount account, BillingSubscription? subscription, string planKey, string? offerId, CancellationToken token)
        {
            if (subscription == null || !Entitled(subscription, DateTime.UtcNow)) return;
            SignupBenefit? benefit = await BenefitAsync(account.TenantId, account.UserId, token).ConfigureAwait(false);
            if (benefit == null || benefit.BillingPlanKey != planKey) return;
            string? expected = subscription.Provider == "Apple" ? benefit.AppleOfferId : benefit.GoogleOfferId;
            if (benefit.DiscountPercent > 0 && (String.IsNullOrWhiteSpace(expected) || expected != offerId)) return;
            await _Database.SignupBenefits.BindAsync(account.TenantId, account.UserId, benefit.Id, subscription.Id, planKey, token).ConfigureAwait(false);
        }

        private async Task<BillingSubscription?> SaveAsync(BillingAccount account, string planKey, string provider, string environment, string id,
            string status, DateTime? ends, bool canceled, DateTime started, string? customerId, string? encryptedSecret, CancellationToken token)
        {
            Plan plan = await PlanAsync(planKey, token).ConfigureAwait(false);
            return await _Database.BillingSubscriptions.SaveVerifiedAsync(new BillingSubscription
            {
                TenantId = account.TenantId, UserId = account.UserId, PlanId = plan.Id, Provider = provider, Environment = environment,
                ProviderId = id, Status = status, EndsUtc = ends, CancelAtPeriodEnd = canceled, LastVerifiedUtc = started,
                CustomerId = customerId, VerificationSecretEncrypted = encryptedSecret
            }, token).ConfigureAwait(false);
        }

        private async Task<BillingAccount?> FindBoundAccountAsync(string? binding, CancellationToken token)
        {
            if (!Guid.TryParse(binding, out Guid parsed)) return null;
            BillingAccount? account = await _Database.BillingAccounts.ReadByPurchaseTokenAsync(parsed.ToString("D"), token).ConfigureAwait(false);
            if (account == null) return null;
            User? user = await _Database.Users.ReadAsync(account.TenantId, account.UserId, token).ConfigureAwait(false);
            if (user == null || !user.Active) return null;
            return account;
        }

        private async Task<Plan> PlanAsync(string key, CancellationToken token)
        {
            return (await _Database.Plans.EnumerateAsync(new PlanFilter { Key = key, PageSize = 1, Active = true }, token).ConfigureAwait(false)).Objects.FirstOrDefault()
                ?? throw new InvalidOperationException("The purchased entitlement package is unavailable.");
        }

        private Task EnqueueAsync(string provider, string environment, string eventId, string providerId, CancellationToken token)
        {
            return _Database.BillingEvents.EnqueueAsync(new BillingEvent { Provider = provider, Environment = environment, EventId = eventId, ProviderId = providerId, RetryUtc = DateTime.UtcNow }, token);
        }

        private void RequireEnvironment(string environment)
        {
            if (environment != "Production" && (environment != "Sandbox" || !_Settings().AllowSandbox)) throw new StoreVerificationException("This billing environment is not accepted by the server.");
        }

        private static void RequireOwner(BillingAccount account, string? token)
        {
            if (!String.Equals(account.PurchaseAccountToken, token, StringComparison.OrdinalIgnoreCase)) throw new StoreVerificationException("This purchase belongs to a different account.");
        }

        private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

        private static string ReturnUrl(BillingSettings settings)
        {
            if (!Uri.TryCreate(settings.DashboardUrl, UriKind.Absolute, out Uri? url) || url.Scheme != "https" || !String.IsNullOrEmpty(url.UserInfo))
                throw new InvalidOperationException("Configure an HTTPS billing dashboard URL.");
            return settings.DashboardUrl.TrimEnd('/') + "/dashboard/account/billing";
        }
    }
}
