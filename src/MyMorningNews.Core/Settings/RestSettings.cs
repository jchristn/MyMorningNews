namespace MyMorningNews.Core.Settings
{
    using System;

    /// <summary>
    /// REST listener settings. Changes require a restart.
    /// </summary>
    public class RestSettings
    {
        #region Public-Members

        /// <summary>
        /// Hostname or address to bind. Default 127.0.0.1. Use 0.0.0.0 inside containers.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Hostname or address to bind.", RestartRequired = true, EnvironmentVariable = "REST_HOSTNAME")]
        public string Hostname
        {
            get
            {
                return _Hostname;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Hostname));
                _Hostname = value.Trim();
            }
        }

        /// <summary>
        /// TCP port. Default 8000. Minimum 1, maximum 65535.
        /// </summary>
        [Setting(Description = "TCP port.", RestartRequired = true, EnvironmentVariable = "REST_PORT", Minimum = 1, Maximum = 65535)]
        public int Port
        {
            get
            {
                return _Port;
            }
            set
            {
                _Port = Math.Clamp(value, 1, 65535);
            }
        }

        /// <summary>
        /// Whether TLS is enabled. Default false.
        /// </summary>
        [Setting(Description = "Enable TLS.", RestartRequired = true)]
        public bool Ssl { get; set; } = false;

        /// <summary>
        /// Value of the Access-Control-Allow-Origin response header. Default "*".
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Access-Control-Allow-Origin value.")]
        public string CorsAllowedOrigins
        {
            get
            {
                return _CorsAllowedOrigins;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(CorsAllowedOrigins));
                _CorsAllowedOrigins = value.Trim();
            }
        }

        /// <summary>
        /// Public base URL of this server, used in podcast feeds, email links, and public pages. Default http://127.0.0.1:12100.
        /// No trailing slash.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Public base URL of the server (feeds, email links).", EnvironmentVariable = "PUBLIC_BASE_URL")]
        public string PublicBaseUrl
        {
            get { return _PublicBaseUrl; }
            set { _PublicBaseUrl = String.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(PublicBaseUrl)) : value.Trim().TrimEnd('/'); }
        }

        /// <summary>
        /// Public base URL of the web app (dashboard), used for deep links. Default http://127.0.0.1:12101. No trailing slash.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Public base URL of the web app (deep links).", EnvironmentVariable = "APP_BASE_URL")]
        public string AppBaseUrl
        {
            get { return _AppBaseUrl; }
            set { _AppBaseUrl = String.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(AppBaseUrl)) : value.Trim().TrimEnd('/'); }
        }

        #endregion

        #region Private-Members

        private string _Hostname = "127.0.0.1";
        private int _Port = 8000;
        private string _PublicBaseUrl = "http://127.0.0.1:12100";
        private string _AppBaseUrl = "http://127.0.0.1:12101";
        private string _CorsAllowedOrigins = "*";

        #endregion
    }
}
