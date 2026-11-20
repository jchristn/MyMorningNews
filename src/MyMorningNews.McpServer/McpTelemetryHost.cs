namespace MyMorningNews.McpServer
{
    using System;
    using Radiant;

    /// <summary>
    /// Radiant telemetry host for the MCP server. Subscribes the Voltaic meter and activity source (tool calls, sessions,
    /// and transport metrics) and exports to the same OTLP collector and Prometheus as the REST server. Best effort:
    /// a failure to start leaves telemetry off. Not thread safe; start once.
    /// </summary>
    public class McpTelemetryHost : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// True when telemetry is running.
        /// </summary>
        public bool Started
        {
            get { return _Host != null; }
        }

        #endregion

        #region Private-Members

        private static readonly string[] _Sources = new[] { "Voltaic" };
        private RadiantHost? _Host = null;
        private bool _Disposed = false;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start exporting when enabled and an exporter is configured.
        /// </summary>
        /// <param name="settings">Settings.</param>
        /// <param name="log">Diagnostic writer.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public void Start(McpServerSettings settings, Action<string> log)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(log);
            if (!settings.TelemetryEnabled) return;
            if (String.IsNullOrWhiteSpace(settings.OtlpEndpoint) && settings.PrometheusPort == 0) return;
            try
            {
                RadiantSettings radiant = new RadiantSettings("mymorningnews-mcp");
                foreach (string source in _Sources)
                {
                    radiant.Sources.AddMeter(source);
                    radiant.Sources.AddActivitySource(source);
                }

                radiant.Otlp.Enable = !String.IsNullOrWhiteSpace(settings.OtlpEndpoint);
                if (radiant.Otlp.Enable) radiant.Otlp.Endpoint = settings.OtlpEndpoint!;
                radiant.Prometheus.Enable = settings.PrometheusPort > 0;
                if (radiant.Prometheus.Enable)
                {
                    radiant.Prometheus.Hostname = settings.PrometheusHostname;
                    radiant.Prometheus.Port = settings.PrometheusPort;
                }

                radiant.DiagnosticCallback = message => log("[telemetry] " + message);
                _Host = RadiantHost.Start(radiant);
                log("[telemetry] started (otlp " + (radiant.Otlp.Enable ? settings.OtlpEndpoint : "off") + ", prometheus " + (radiant.Prometheus.Enable ? settings.PrometheusHostname + ":" + settings.PrometheusPort.ToString() : "off") + ")");
            }
            catch (Exception ex)
            {
                _Host = null;
                log("[telemetry] disabled: " + ex.Message);
            }
        }

        /// <summary>
        /// Flush and stop telemetry.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Flush and stop telemetry.
        /// </summary>
        /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing && _Host != null)
            {
                try
                {
                    _Host.ForceFlush(3000);
                    _Host.Dispose();
                }
                catch (Exception)
                {
                }

                _Host = null;
            }

            _Disposed = true;
        }

        #endregion
    }
}
