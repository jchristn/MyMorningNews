namespace MyMorningNews.Core.Settings
{
    using System;

    /// <summary>
    /// Telemetry settings. The server exports traces over OTLP and exposes Prometheus metrics on its own port.
    /// </summary>
    public class TelemetrySettings
    {
        #region Public-Members

        /// <summary>
        /// Whether telemetry is enabled. Default true.
        /// </summary>
        [Setting(Description = "Enable metrics and traces.", RestartRequired = true)]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Service name reported to the collector. Default mymorningnews-server.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Service name reported in traces.", RestartRequired = true)]
        public string ServiceName
        {
            get
            {
                return _ServiceName;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(ServiceName));
                _ServiceName = value.Trim();
            }
        }

        /// <summary>
        /// OTLP endpoint for traces, for example http://tempo:4317. Empty disables trace export.
        /// </summary>
        [Setting(Description = "OTLP endpoint for traces. Empty disables export.", RestartRequired = true, EnvironmentVariable = "TELEMETRY_OTLP_ENDPOINT")]
        public string OtlpEndpoint { get; set; } = String.Empty;

        /// <summary>
        /// Loki push endpoint for logs, for example http://loki:3100. Empty disables log export.
        /// </summary>
        [Setting(Description = "Loki endpoint for logs. Empty disables export.", RestartRequired = true, EnvironmentVariable = "TELEMETRY_LOKI_ENDPOINT")]
        public string LokiEndpoint { get; set; } = String.Empty;

        /// <summary>
        /// Whether the Prometheus scrape endpoint is served at /metrics. Default true.
        /// </summary>
        [Setting(Description = "Serve Prometheus metrics at /metrics.", RestartRequired = true)]
        public bool PrometheusEnabled { get; set; } = true;

        /// <summary>
        /// Hostname the Prometheus scrape endpoint binds and answers to. Wildcards are not supported; inside Docker Compose
        /// use the service name (server) so Prometheus can scrape server:9464. Default 127.0.0.1.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Hostname for the Prometheus scrape endpoint (use the compose service name in Docker).", RestartRequired = true, EnvironmentVariable = "TELEMETRY_PROMETHEUS_HOSTNAME")]
        public string PrometheusHostname
        {
            get
            {
                return _PrometheusHostname;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(PrometheusHostname));
                _PrometheusHostname = value.Trim();
            }
        }

        /// <summary>
        /// Port for the Prometheus scrape endpoint served by the in-process collector. Default 9464. Minimum 1, maximum 65535.
        /// Keep it internal to the container network.
        /// </summary>
        [Setting(Description = "Prometheus scrape port (internal).", RestartRequired = true, EnvironmentVariable = "TELEMETRY_PROMETHEUS_PORT", Minimum = 1, Maximum = 65535)]
        public int PrometheusPort
        {
            get
            {
                return _PrometheusPort;
            }
            set
            {
                _PrometheusPort = Math.Clamp(value, 1, 65535);
            }
        }

        /// <summary>
        /// Browser-reachable links shown on the dashboard's External Services card.
        /// </summary>
        [Setting(Description = "Grafana URL shown on the dashboard.")]
        public string GrafanaUrl { get; set; } = "http://127.0.0.1:12110";

        /// <summary>
        /// Prometheus URL shown on the dashboard.
        /// </summary>
        [Setting(Description = "Prometheus URL shown on the dashboard.")]
        public string PrometheusUrl { get; set; } = "http://127.0.0.1:12111";

        /// <summary>
        /// Tempo URL shown on the dashboard.
        /// </summary>
        [Setting(Description = "Tempo URL shown on the dashboard.")]
        public string TempoUrl { get; set; } = "http://127.0.0.1:12112";

        /// <summary>
        /// Loki URL shown on the dashboard.
        /// </summary>
        [Setting(Description = "Loki URL shown on the dashboard.")]
        public string LokiUrl { get; set; } = "http://127.0.0.1:12115";

        #endregion

        #region Private-Members

        private string _ServiceName = "mymorningnews-server";
        private int _PrometheusPort = 9464;
        private string _PrometheusHostname = "127.0.0.1";

        #endregion
    }
}
