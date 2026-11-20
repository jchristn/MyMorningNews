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

        /// <summary>
        /// Public URL of the MCP server's Streamable HTTP endpoint, shown to users who connect an MCP client. Default
        /// <c>http://127.0.0.1:12105/mcp</c>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Public URL of the MCP server endpoint (shown in the app).", EnvironmentVariable = "MCP_PUBLIC_URL")]
        public string McpPublicUrl
        {
            get { return _McpPublicUrl; }
            set { _McpPublicUrl = String.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(McpPublicUrl)) : value.Trim(); }
        }

        /// <summary>
        /// How often an event stream (for example run progress) checks for changes, in milliseconds. Default 1000.
        /// Minimum 200, maximum 10000.
        /// </summary>
        [Setting(Description = "How often event streams check for changes (ms).", Minimum = 200, Maximum = 10000)]
        public int EventStreamPollMs
        {
            get { return _EventStreamPollMs; }
            set { _EventStreamPollMs = Math.Clamp(value, 200, 10000); }
        }

        /// <summary>
        /// Longest an event stream stays open, in minutes; clients reconnect after it closes. Default 30. Minimum 1,
        /// maximum 240.
        /// </summary>
        [Setting(Description = "Longest an event stream stays open (minutes).", Minimum = 1, Maximum = 240)]
        public int EventStreamMaxMinutes
        {
            get { return _EventStreamMaxMinutes; }
            set { _EventStreamMaxMinutes = Math.Clamp(value, 1, 240); }
        }

        #endregion

        #region Private-Members

        private string _Hostname = "127.0.0.1";
        private int _Port = 8000;
        private string _PublicBaseUrl = "http://127.0.0.1:12100";
        private string _AppBaseUrl = "http://127.0.0.1:12101";
        private string _CorsAllowedOrigins = "*";
        private string _McpPublicUrl = "http://127.0.0.1:12105/mcp";
        private int _EventStreamPollMs = 1000;
        private int _EventStreamMaxMinutes = 30;

        #endregion
    }
}
