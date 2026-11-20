namespace MyMorningNews.McpServer
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// MCP server settings, read from command-line arguments and then environment variables (arguments win).
    /// Not thread safe; build once at startup.
    /// </summary>
    public class McpServerSettings
    {
        #region Public-Members

        /// <summary>
        /// Base URL of the My Morning News REST API. Default <c>http://127.0.0.1:12100</c>.
        /// Argument <c>--api-url</c>, environment <c>MYMORNINGNEWS_MCP_SERVER_URL</c> or <c>MYMORNINGNEWS_BASE_URL</c>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string ApiUrl
        {
            get
            {
                return _ApiUrl;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(ApiUrl));
                _ApiUrl = value.Trim().TrimEnd('/');
            }
        }

        /// <summary>
        /// Serve over stdio instead of Streamable HTTP. Default false. Argument <c>--stdio</c>.
        /// </summary>
        public bool Stdio { get; set; } = false;

        /// <summary>
        /// Hostname for Streamable HTTP. Default <c>127.0.0.1</c> (loopback clients only). Use <c>*</c> in a container.
        /// Argument <c>--host</c>, environment <c>MYMORNINGNEWS_MCP_HOSTNAME</c>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
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
        /// Port for Streamable HTTP. Default 12105, minimum 1, maximum 65535.
        /// Argument <c>--port</c>, environment <c>MYMORNINGNEWS_MCP_PORT</c>.
        /// </summary>
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
        /// Session token for stdio mode. Environment <c>MYMORNINGNEWS_TOKEN</c>. Null when not set.
        /// </summary>
        public string? Token { get; set; } = null;

        /// <summary>
        /// Access key for stdio mode. Environment <c>MYMORNINGNEWS_ACCESS_KEY</c>. Null when not set.
        /// </summary>
        public string? AccessKey { get; set; } = null;

        /// <summary>
        /// Secret key for stdio mode. Environment <c>MYMORNINGNEWS_SECRET_KEY</c>. Null when not set.
        /// </summary>
        public string? SecretKey { get; set; } = null;

        /// <summary>
        /// Tenant for stdio mode. Environment <c>MYMORNINGNEWS_TENANT_ID</c>. Null when not set.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Email for stdio mode, used to sign in when no token or access key is set. Environment <c>MYMORNINGNEWS_EMAIL</c>.
        /// </summary>
        public string? Email { get; set; } = null;

        /// <summary>
        /// Password for stdio mode. Environment <c>MYMORNINGNEWS_PASSWORD</c>.
        /// </summary>
        public string? Password { get; set; } = null;

        /// <summary>
        /// How long a validated HTTP credential is trusted before it is checked again, in seconds. Default 60, minimum 0
        /// (check every request), maximum 600.
        /// </summary>
        public int CredentialCacheSeconds
        {
            get
            {
                return _CredentialCacheSeconds;
            }
            set
            {
                _CredentialCacheSeconds = Math.Clamp(value, 0, 600);
            }
        }

        /// <summary>
        /// REST request timeout in seconds. Default 120, minimum 5, maximum 600.
        /// </summary>
        public int RequestTimeoutSeconds
        {
            get
            {
                return _RequestTimeoutSeconds;
            }
            set
            {
                _RequestTimeoutSeconds = Math.Clamp(value, 5, 600);
            }
        }

        /// <summary>
        /// Enable telemetry export. Default true. Environment <c>MYMORNINGNEWS_MCP_TELEMETRY</c>.
        /// </summary>
        public bool TelemetryEnabled { get; set; } = true;

        /// <summary>
        /// OTLP endpoint for traces, metrics, and logs. Null disables OTLP export. Environment <c>MYMORNINGNEWS_OTLP_ENDPOINT</c>.
        /// </summary>
        public string? OtlpEndpoint { get; set; } = null;

        /// <summary>
        /// Prometheus scrape port, or 0 to disable. Default 0. Environment <c>MYMORNINGNEWS_MCP_PROMETHEUS_PORT</c>.
        /// </summary>
        public int PrometheusPort
        {
            get
            {
                return _PrometheusPort;
            }
            set
            {
                _PrometheusPort = Math.Clamp(value, 0, 65535);
            }
        }

        /// <summary>
        /// Write diagnostic messages to standard error. Default false. Argument <c>--verbose</c>.
        /// </summary>
        public bool Verbose { get; set; } = false;

        /// <summary>
        /// Print the tool, resource, and prompt definitions as JSON and exit. Default false. Argument <c>--describe</c>.
        /// </summary>
        public bool Describe { get; set; } = false;

        #endregion

        #region Private-Members

        private string _ApiUrl = "http://127.0.0.1:12100";
        private string _Hostname = "127.0.0.1";
        private int _Port = 12105;
        private int _CredentialCacheSeconds = 60;
        private int _RequestTimeoutSeconds = 120;
        private int _PrometheusPort = 0;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build settings from environment variables, then command-line arguments.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <param name="environment">Environment lookup; null reads the process environment.</param>
        /// <returns>Settings.</returns>
        /// <exception cref="ArgumentNullException">Arguments are null.</exception>
        /// <exception cref="ArgumentException">An argument is unknown or lacks a value.</exception>
        public static McpServerSettings FromArguments(string[] args, Func<string, string?>? environment = null)
        {
            ArgumentNullException.ThrowIfNull(args);
            Func<string, string?> env = environment ?? Environment.GetEnvironmentVariable;
            McpServerSettings s = new McpServerSettings();
            string? url = Clean(env("MYMORNINGNEWS_MCP_SERVER_URL")) ?? Clean(env("MYMORNINGNEWS_BASE_URL"));
            if (url != null) s.ApiUrl = url;
            string? host = Clean(env("MYMORNINGNEWS_MCP_HOSTNAME"));
            if (host != null) s.Hostname = host;
            int? port = Int(env("MYMORNINGNEWS_MCP_PORT"));
            if (port.HasValue) s.Port = port.Value;
            s.Token = Clean(env("MYMORNINGNEWS_TOKEN"));
            s.AccessKey = Clean(env("MYMORNINGNEWS_ACCESS_KEY"));
            s.SecretKey = Clean(env("MYMORNINGNEWS_SECRET_KEY"));
            s.TenantId = Clean(env("MYMORNINGNEWS_TENANT_ID"));
            s.Email = Clean(env("MYMORNINGNEWS_EMAIL"));
            s.Password = env("MYMORNINGNEWS_PASSWORD");
            string? telemetry = Clean(env("MYMORNINGNEWS_MCP_TELEMETRY"));
            if (telemetry != null && Boolean.TryParse(telemetry, out bool on)) s.TelemetryEnabled = on;
            s.OtlpEndpoint = Clean(env("MYMORNINGNEWS_OTLP_ENDPOINT"));
            int? prometheus = Int(env("MYMORNINGNEWS_MCP_PROMETHEUS_PORT"));
            if (prometheus.HasValue) s.PrometheusPort = prometheus.Value;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                switch (a.ToLowerInvariant())
                {
                    case "--stdio":
                        s.Stdio = true;
                        break;
                    case "--verbose":
                        s.Verbose = true;
                        break;
                    case "--describe":
                        s.Describe = true;
                        break;
                    case "--api-url":
                        s.ApiUrl = Next(args, ref i, a);
                        break;
                    case "--host":
                        s.Hostname = Next(args, ref i, a);
                        break;
                    case "--port":
                        s.Port = Int(Next(args, ref i, a)) ?? throw new ArgumentException("--port needs a number.");
                        break;
                    default:
                        throw new ArgumentException("Unknown argument " + a + ". Use --stdio, --api-url, --host, --port, --verbose, or --describe.");
                }
            }

            return s;
        }

        /// <summary>
        /// Names of the settings that came from secrets, for diagnostics without values.
        /// </summary>
        /// <returns>Names of the credential settings that are set.</returns>
        public List<string> CredentialSources()
        {
            List<string> names = new List<string>();
            if (Token != null) names.Add("MYMORNINGNEWS_TOKEN");
            if (AccessKey != null) names.Add("MYMORNINGNEWS_ACCESS_KEY");
            if (SecretKey != null) names.Add("MYMORNINGNEWS_SECRET_KEY");
            if (Email != null) names.Add("MYMORNINGNEWS_EMAIL");
            if (Password != null) names.Add("MYMORNINGNEWS_PASSWORD");
            return names;
        }

        #endregion

        #region Private-Methods

        private static string Next(string[] args, ref int i, string name)
        {
            if (i + 1 >= args.Length || String.IsNullOrWhiteSpace(args[i + 1])) throw new ArgumentException(name + " needs a value.");
            i++;
            return args[i];
        }

        private static string? Clean(string? value)
        {
            return String.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static int? Int(string? value)
        {
            if (String.IsNullOrWhiteSpace(value)) return null;
            return Int32.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : null;
        }

        #endregion
    }
}
