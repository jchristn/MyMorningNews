namespace MyMorningNews.Core.Settings
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Hosted service settings (plan section 33). Off for self-hosted installs.
    /// </summary>
    public class HostingSettings
    {
        private List<string> _AllowedPrivateHosts = new List<string> { "searxng", "tts", "ollama" };

        /// <summary>
        /// Open public sign-up (Phase 14). Default false.
        /// </summary>
        [Setting(Description = "Open public sign-up.", EnvironmentVariable = "HOSTING_PUBLIC_SIGNUP_ENABLED")]
        public bool PublicSignupEnabled { get; set; } = false;

        /// <summary>
        /// Plan key for users without an assignment. Default unlimited (self-hosted); the hosted profile uses free.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Plan for users without an assignment (free or unlimited).", EnvironmentVariable = "HOSTING_DEFAULT_PLAN")]
        public string DefaultPlanKey
        {
            get { return _DefaultPlanKey; }
            set { _DefaultPlanKey = String.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(DefaultPlanKey)) : value.Trim().ToLowerInvariant(); }
        }

        /// <summary>
        /// Promotional code checks allowed per IP address per minute on the public route. Default 20, minimum 1, maximum 1000.
        /// </summary>
        [Setting(Description = "Public promotional code checks per IP per minute.", Minimum = 1, Maximum = 1000)]
        public int PromoChecksPerMinute
        {
            get { return _PromoChecksPerMinute; }
            set { _PromoChecksPerMinute = Math.Clamp(value, 1, 1000); }
        }

        /// <summary>
        /// Require a verified email before a self-signed-up user can sign in. Default true (applies only when public sign-up is on).
        /// </summary>
        [Setting(Description = "Require email verification for sign-up accounts.")]
        public bool RequireEmailVerification { get; set; } = true;

        /// <summary>
        /// Cloudflare Turnstile secret key for the sign-up form. Empty turns the check off. Default empty.
        /// </summary>
        [Setting(Description = "Cloudflare Turnstile secret key (empty turns the check off).", Secret = true, EnvironmentVariable = "HOSTING_TURNSTILE_SECRET_KEY")]
        public string TurnstileSecretKey { get; set; } = String.Empty;

        /// <summary>
        /// Cloudflare Turnstile site key (public), shown to the sign-up form. Default empty.
        /// </summary>
        [Setting(Description = "Cloudflare Turnstile site key (public).", EnvironmentVariable = "HOSTING_TURNSTILE_SITE_KEY")]
        public string TurnstileSiteKey { get; set; } = String.Empty;

        /// <summary>
        /// Refuse sign-ups from these email domains (disposable mailboxes). Default a short list of well-known ones.
        /// </summary>
        public List<string> BlockedEmailDomains
        {
            get { return _BlockedEmailDomains; }
            set { _BlockedEmailDomains = value ?? new List<string>(); }
        }

        /// <summary>
        /// Sign-ups allowed per IP address per hour. Default 5, minimum 1, maximum 1000.
        /// </summary>
        [Setting(Description = "Sign-ups per IP per hour.", Minimum = 1, Maximum = 1000)]
        public int SignupsPerIpPerHour
        {
            get { return _SignupsPerIpPerHour; }
            set { _SignupsPerIpPerHour = Math.Clamp(value, 1, 1000); }
        }

        /// <summary>
        /// Hours a verification link stays valid. Default 48, minimum 1, maximum 720.
        /// </summary>
        [Setting(Description = "Hours a verification link stays valid.", Minimum = 1, Maximum = 720)]
        public int VerificationHours
        {
            get { return _VerificationHours; }
            set { _VerificationHours = Math.Clamp(value, 1, 720); }
        }

        /// <summary>
        /// Minutes a password reset link stays valid. Default 60, minimum 5, maximum 1440.
        /// </summary>
        [Setting(Description = "Minutes a password reset link stays valid.", Minimum = 5, Maximum = 1440)]
        public int PasswordResetMinutes
        {
            get { return _PasswordResetMinutes; }
            set { _PasswordResetMinutes = Math.Clamp(value, 5, 1440); }
        }

        /// <summary>
        /// Days between an account deletion request and the purge of its data; signing in cancels it. Default 7, minimum 0, maximum 90.
        /// </summary>
        [Setting(Description = "Days before a deleted account is purged.", Minimum = 0, Maximum = 90)]
        public int DeletionGraceDays
        {
            get { return _DeletionGraceDays; }
            set { _DeletionGraceDays = Math.Clamp(value, 0, 90); }
        }

        /// <summary>
        /// Refuse loopback, link-local, private, and metadata addresses for fetches, model endpoints, MCP servers, and
        /// search providers (server-side request forgery protection). Default false; the hosted profile turns it on.
        /// </summary>
        [Setting(Description = "Block private and loopback network targets (SSRF protection).", EnvironmentVariable = "HOSTING_RESTRICT_PRIVATE_NETWORKS")]
        public bool RestrictPrivateNetworks { get; set; } = false;

        /// <summary>
        /// Give every new tenant (other than default) the operator's Anthropic key by reference instead of a copy in its
        /// own vault. Personal tenants created by sign-up always use it. Default false.
        /// </summary>
        [Setting(Description = "New tenants use the platform Anthropic key by reference.", EnvironmentVariable = "HOSTING_USE_PLATFORM_CREDENTIALS")]
        public bool UsePlatformCredentials { get; set; } = false;

        /// <summary>
        /// Host names the private network block allows anyway: the operator's own services on the Docker network.
        /// Matched case-insensitively against the host in the URL. Default searxng, tts, ollama.
        /// </summary>
        [Setting(Description = "Internal host names exempt from the private network block.")]
        public List<string> AllowedPrivateHosts
        {
            get { return _AllowedPrivateHosts; }
            set { _AllowedPrivateHosts = value ?? new List<string>(); }
        }

        /// <summary>
        /// Current terms of service version; sign-up records the version the user accepted. Default 2026-11-20.
        /// </summary>
        [Setting(Description = "Terms of service version accepted at sign-up.")]
        public string TermsVersion { get; set; } = "2026-11-20";

        /// <summary>
        /// Minimum age to sign up, confirmed at sign-up. Default 18, minimum 13, maximum 21.
        /// </summary>
        [Setting(Description = "Minimum age to sign up.", Minimum = 13, Maximum = 21)]
        public int MinimumAge
        {
            get { return _MinimumAge; }
            set { _MinimumAge = Math.Clamp(value, 13, 21); }
        }

        /// <summary>
        /// Apple Team ID for /.well-known/apple-app-site-association. Empty leaves the file out. Default empty.
        /// </summary>
        [Setting(Description = "Apple Team ID for universal links.", EnvironmentVariable = "HOSTING_APPLE_TEAM_ID")]
        public string AppleTeamId { get; set; } = String.Empty;

        /// <summary>
        /// Mobile bundle identifier (iOS) and application ID (Android). Default ai.latticetech.mymorningnews.
        /// </summary>
        [Setting(Description = "Mobile bundle and application identifier.")]
        public string MobileAppId { get; set; } = "ai.latticetech.mymorningnews";

        /// <summary>
        /// SHA-256 signing certificate fingerprints for /.well-known/assetlinks.json (Play App Signing). Empty leaves the file out.
        /// </summary>
        public List<string> AndroidCertificateFingerprints
        {
            get { return _AndroidCertificateFingerprints; }
            set { _AndroidCertificateFingerprints = value ?? new List<string>(); }
        }

        private string _DefaultPlanKey = "unlimited";
        private int _PromoChecksPerMinute = 20;
        private List<string> _BlockedEmailDomains = new List<string> { "mailinator.com", "guerrillamail.com", "10minutemail.com", "tempmail.com", "yopmail.com", "trashmail.com" };
        private int _SignupsPerIpPerHour = 5;
        private int _VerificationHours = 48;
        private int _PasswordResetMinutes = 60;
        private int _DeletionGraceDays = 7;
        private int _MinimumAge = 18;
        private List<string> _AndroidCertificateFingerprints = new List<string>();
    }
}
