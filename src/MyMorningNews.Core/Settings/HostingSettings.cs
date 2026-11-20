namespace MyMorningNews.Core.Settings
{
    using System;

    /// <summary>
    /// Hosted service settings (plan section 33). Off for self-hosted installs.
    /// </summary>
    public class HostingSettings
    {
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

        private string _DefaultPlanKey = "unlimited";
        private int _PromoChecksPerMinute = 20;
    }
}
