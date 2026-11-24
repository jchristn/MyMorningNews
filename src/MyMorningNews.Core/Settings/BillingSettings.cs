namespace MyMorningNews.Core.Settings
{
    using System;
    using System.Collections.Generic;

    /// <summary>Optional hosted billing. Self-hosted deployments require no provider configuration.</summary>
    public class BillingSettings
    {
        /// <summary>Enable new paid purchases. Default false; existing purchases still reconcile when provider credentials are configured.</summary>
        [Setting(Description = "Enable new paid purchases. Existing subscriptions still reconcile when disabled.")]
        public bool Enabled { get; set; } = false;
        /// <summary>Accept sandbox purchases. Default false. Enable on test deployments only.</summary>
        [Setting(Description = "Accept test purchases. Disable for production billing.")]
        public bool AllowSandbox { get; set; } = false;
        /// <summary>Fixed web return origin. No caller-supplied redirects are accepted.</summary>
        [Setting(Description = "Dashboard HTTPS origin for checkout and portal returns.")]
        public string DashboardUrl { get; set; } = "https://app.mymorningnews.ai";
        /// <summary>Stripe web integration.</summary>
        public StripeSettings Stripe { get { return _Stripe; } set { _Stripe = value ?? throw new ArgumentNullException(nameof(Stripe)); } }
        private StripeSettings _Stripe = new StripeSettings();
        /// <summary>Apple native integration.</summary>
        public AppStoreSettings Apple { get { return _Apple; } set { _Apple = value ?? throw new ArgumentNullException(nameof(Apple)); } }
        private AppStoreSettings _Apple = new AppStoreSettings();
        /// <summary>Google native integration.</summary>
        public GooglePlaySettings Google { get { return _Google; } set { _Google = value ?? throw new ArgumentNullException(nameof(Google)); } }
        private GooglePlaySettings _Google = new GooglePlaySettings();
        /// <summary>Purchasable offers. No introductory trial is configured.</summary>
        public List<BillingOfferSettings> Offers { get { return _Offers; } set { _Offers = value ?? throw new ArgumentNullException(nameof(Offers)); } }
        private List<BillingOfferSettings> _Offers = new List<BillingOfferSettings>
        {
            new BillingOfferSettings { Key = "paid-5-month", PlanKey = "paid-5", Interval = "Month", UsdCents = 1000 },
            new BillingOfferSettings { Key = "paid-5-year", PlanKey = "paid-5", Interval = "Year", UsdCents = 10000 },
            new BillingOfferSettings { Key = "paid-20-month", PlanKey = "paid-20", Interval = "Month", UsdCents = 3000 },
            new BillingOfferSettings { Key = "paid-20-year", PlanKey = "paid-20", Interval = "Year", UsdCents = 30000 }
        };
    }
}
