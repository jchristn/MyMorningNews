namespace MyMorningNews.Core.Settings
{
    using System;
    using MyMorningNews.Core.Database;

    /// <summary>
    /// Root settings object loaded from mymorningnews.json and overridden by environment variables.
    /// Thread safety: groups are read concurrently; writes happen only through the settings service.
    /// </summary>
    public class ServerSettings
    {
        #region Public-Members

        /// <summary>
        /// UTC time the settings file was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// REST listener settings.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null.</exception>
        public RestSettings Rest
        {
            get { return _Rest; }
            set { _Rest = value ?? throw new ArgumentNullException(nameof(Rest)); }
        }

        /// <summary>
        /// Database settings.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null.</exception>
        public DatabaseSettings Database
        {
            get { return _Database; }
            set { _Database = value ?? throw new ArgumentNullException(nameof(Database)); }
        }

        /// <summary>
        /// Logging settings.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null.</exception>
        public LoggingSettings Logging
        {
            get { return _Logging; }
            set { _Logging = value ?? throw new ArgumentNullException(nameof(Logging)); }
        }

        /// <summary>
        /// Authentication settings.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null.</exception>
        public AuthSettings Auth
        {
            get { return _Auth; }
            set { _Auth = value ?? throw new ArgumentNullException(nameof(Auth)); }
        }

        /// <summary>
        /// Request history settings.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null.</exception>
        public RequestHistorySettings RequestHistory
        {
            get { return _RequestHistory; }
            set { _RequestHistory = value ?? throw new ArgumentNullException(nameof(RequestHistory)); }
        }

        /// <summary>
        /// Telemetry settings.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null.</exception>
        public TelemetrySettings Telemetry
        {
            get { return _Telemetry; }
            set { _Telemetry = value ?? throw new ArgumentNullException(nameof(Telemetry)); }
        }

        #endregion

        #region Private-Members

        private RestSettings _Rest = new RestSettings();
        private DatabaseSettings _Database = new DatabaseSettings();
        private LoggingSettings _Logging = new LoggingSettings();
        private AuthSettings _Auth = new AuthSettings();
        private RequestHistorySettings _RequestHistory = new RequestHistorySettings();
        private TelemetrySettings _Telemetry = new TelemetrySettings();

        #endregion
    }
}
