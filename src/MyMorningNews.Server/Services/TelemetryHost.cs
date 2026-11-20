namespace MyMorningNews.Server.Services
{
    using System;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Core.Telemetry;
    using Radiant;
    using SyslogLogging;

    /// <summary>
    /// In-process telemetry collector (Radiant) subscribed to the Watson, PolyPrompt, SendWithMailgun, CrawlSharp, Voltaic,
    /// and MyMorningNews meters and activity sources, plus the Npgsql meter (connection pool and command metrics; its
    /// per-command spans are left off to keep traces readable). Exports traces over OTLP, logs to Loki, and serves a
    /// Prometheus scrape endpoint on an internal port. Best effort: failure to start logs a warning and the server continues.
    /// </summary>
    public class TelemetryHost : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Whether the collector started.
        /// </summary>
        public bool Started
        {
            get { return _Host != null; }
        }

        #endregion

        #region Private-Members

        private static readonly string[] _Sources = new[] { "Watson", "PolyPrompt", "SendWithMailgun", "CrawlSharp", "Voltaic", MyMorningNewsTelemetry.Name };
        private static readonly string[] _MeterOnlySources = new[] { "Npgsql" };
        private RadiantHost? _Host = null;
        private bool _Disposed = false;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start the collector.
        /// </summary>
        /// <param name="settings">Telemetry settings.</param>
        /// <param name="logging">Logging.</param>
        public void Start(TelemetrySettings settings, LoggingModule logging)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(logging);
            if (!settings.Enabled) return;
            try
            {
                RadiantSettings radiant = new RadiantSettings(settings.ServiceName);
                foreach (string source in _Sources)
                {
                    radiant.Sources.AddMeter(source);
                    radiant.Sources.AddActivitySource(source);
                }

                foreach (string source in _MeterOnlySources) radiant.Sources.AddMeter(source);

                radiant.Otlp.Enable = !String.IsNullOrWhiteSpace(settings.OtlpEndpoint);
                if (radiant.Otlp.Enable) radiant.Otlp.Endpoint = settings.OtlpEndpoint;
                radiant.Prometheus.Enable = settings.PrometheusEnabled;
                radiant.Prometheus.Hostname = settings.PrometheusHostname;
                radiant.Prometheus.Port = settings.PrometheusPort;
                radiant.Loki.Enable = !String.IsNullOrWhiteSpace(settings.LokiEndpoint);
                if (radiant.Loki.Enable) radiant.Loki.Endpoint = settings.LokiEndpoint;
                radiant.DiagnosticCallback = message => logging.Debug("[TelemetryHost] " + message);
                _Host = RadiantHost.Start(radiant);
                logging.Info("[TelemetryHost] telemetry started (otlp " + (radiant.Otlp.Enable ? settings.OtlpEndpoint : "off") + ", prometheus port " + (settings.PrometheusEnabled ? settings.PrometheusPort.ToString() : "off") + ")");
            }
            catch (Exception ex)
            {
                _Host = null;
                logging.Warn("[TelemetryHost] telemetry disabled: " + ex.Message + (ex.InnerException != null ? " (" + ex.InnerException.Message + ")" : String.Empty));
            }
        }

        /// <summary>
        /// Dispose of resources, flushing pending telemetry.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        /// <param name="disposing">True when called from Dispose.</param>
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
