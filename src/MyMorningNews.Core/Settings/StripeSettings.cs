namespace MyMorningNews.Core.Settings
{
    using System;

    /// <summary>
    /// Stripe settings for web subscriptions. An empty secret key disables Stripe.
    /// </summary>
    public class StripeSettings
    {
        #region Public-Members

        /// <summary>
        /// Restricted or secret API key. Secret.
        /// </summary>
        [Setting(Secret = true, EnvironmentVariable = "BILLING_STRIPE_SECRET_KEY")]
        public string SecretKey { get; set; } = String.Empty;

        /// <summary>
        /// Webhook signing secret (whsec_...). Secret.
        /// </summary>
        [Setting(Secret = true, EnvironmentVariable = "BILLING_STRIPE_WEBHOOK_SECRET")]
        public string WebhookSecret { get; set; } = String.Empty;

        /// <summary>
        /// Price id of the membership.
        /// </summary>
        public string PriceId { get; set; } = String.Empty;

        /// <summary>Application-specific customer portal configuration ID. Empty uses Stripe's account default.</summary>
        [Setting(Description = "Stripe portal configuration for My Morning News, isolated from other applications in the Stripe account.")]
        public string PortalConfigurationId { get; set; } = String.Empty;

        /// <summary>
        /// Stripe API base URL. Override only for tests.
        /// </summary>
        public string ApiBaseUrl { get; set; } = "https://api.stripe.com";

        #endregion
    }
}
