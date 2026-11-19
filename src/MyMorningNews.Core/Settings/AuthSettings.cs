namespace MyMorningNews.Core.Settings
{
    using System;

    /// <summary>
    /// Authentication settings.
    /// </summary>
    public class AuthSettings
    {
        #region Public-Members

        /// <summary>
        /// Key material for token and secret encryption. Any string; a 256-bit key is derived with SHA-256.
        /// Override with MYMORNINGNEWS_AUTH_SIGNING_KEY. Changing it invalidates every session and stored credential secret.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Key used to encrypt session tokens and credential secrets.", Secret = true, RestartRequired = true, EnvironmentVariable = "AUTH_SIGNING_KEY")]
        public string SigningKey
        {
            get
            {
                return _SigningKey;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(SigningKey));
                _SigningKey = value;
            }
        }

        /// <summary>
        /// Interactive session lifetime in minutes. Default 480. Minimum 15, maximum 10080.
        /// </summary>
        [Setting(Description = "Session lifetime in minutes.", Minimum = 15, Maximum = 10080)]
        public int SessionLifetimeMinutes
        {
            get
            {
                return _SessionLifetimeMinutes;
            }
            set
            {
                _SessionLifetimeMinutes = Math.Clamp(value, 15, 10080);
            }
        }

        /// <summary>
        /// System administration API key accepted in x-api-key. Empty disables admin key authentication.
        /// </summary>
        [Setting(Description = "System administration API key (x-api-key). Empty disables it.", Secret = true, EnvironmentVariable = "AUTH_ADMIN_API_KEY")]
        public string AdminApiKey { get; set; } = "mymorningnewsadmin";

        /// <summary>
        /// Maximum login attempts per minute per source IP and email. Default 10. Minimum 1, maximum 1000.
        /// </summary>
        [Setting(Description = "Login attempts allowed per minute per IP and email.", Minimum = 1, Maximum = 1000)]
        public int LoginRateLimitPerMinute
        {
            get
            {
                return _LoginRateLimitPerMinute;
            }
            set
            {
                _LoginRateLimitPerMinute = Math.Clamp(value, 1, 1000);
            }
        }

        /// <summary>
        /// Whether sessions can be refreshed. Default true.
        /// </summary>
        [Setting(Description = "Allow session refresh.")]
        public bool AllowRefresh { get; set; } = true;

        #endregion

        #region Private-Members

        private string _SigningKey = "mymorningnews-change-this-signing-key";
        private int _SessionLifetimeMinutes = 480;
        private int _LoginRateLimitPerMinute = 10;

        #endregion
    }
}
