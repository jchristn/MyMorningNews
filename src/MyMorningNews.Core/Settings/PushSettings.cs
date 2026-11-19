namespace MyMorningNews.Core.Settings
{
    using System;

    /// <summary>
    /// Mobile push notification settings (Expo Push API).
    /// </summary>
    public class PushSettings
    {
        #region Public-Members

        /// <summary>
        /// Send push notifications to registered devices. Default true.
        /// </summary>
        [Setting(Description = "Send push notifications to registered mobile devices.", EnvironmentVariable = "PUSH_ENABLED")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Expo Push API send URL. Default https://exp.host/--/api/v2/push/send.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Expo Push API send URL.", EnvironmentVariable = "PUSH_EXPO_URL")]
        public string ExpoPushUrl
        {
            get { return _ExpoPushUrl; }
            set { _ExpoPushUrl = String.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(ExpoPushUrl)) : value.Trim(); }
        }

        /// <summary>
        /// Expo access token, required only when the Expo project enforces authenticated push. Empty by default.
        /// </summary>
        [Setting(Description = "Expo access token for authenticated push (optional).", Secret = true, EnvironmentVariable = "PUSH_EXPO_ACCESS_TOKEN")]
        public string ExpoAccessToken { get; set; } = String.Empty;

        /// <summary>
        /// HTTP timeout for push requests in milliseconds. Default 10000. Minimum 1000, maximum 60000.
        /// </summary>
        [Setting(Description = "Push request timeout in milliseconds.", Minimum = 1000, Maximum = 60000)]
        public int TimeoutMs
        {
            get { return _TimeoutMs; }
            set { _TimeoutMs = Math.Clamp(value, 1000, 60000); }
        }

        #endregion

        #region Private-Members

        private string _ExpoPushUrl = "https://exp.host/--/api/v2/push/send";
        private int _TimeoutMs = 10000;

        #endregion
    }
}
