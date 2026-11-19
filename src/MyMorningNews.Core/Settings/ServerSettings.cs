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

        /// <summary>
        /// Pipeline and model gateway settings.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null.</exception>
        public PipelineSettings Pipeline
        {
            get { return _Pipeline; }
            set { _Pipeline = value ?? throw new ArgumentNullException(nameof(Pipeline)); }
        }

        /// <summary>
        /// Diagnostics settings.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null.</exception>
        public DiagnosticsSettings Diagnostics
        {
            get { return _Diagnostics; }
            set { _Diagnostics = value ?? throw new ArgumentNullException(nameof(Diagnostics)); }
        }

        /// <summary>
        /// Model seeding defaults.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null.</exception>
        public ModelSettings Models
        {
            get { return _Models; }
            set { _Models = value ?? throw new ArgumentNullException(nameof(Models)); }
        }

        /// <summary>
        /// Mobile push notifications.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null.</exception>
        public PushSettings Push
        {
            get { return _Push; }
            set { _Push = value ?? throw new ArgumentNullException(nameof(Push)); }
        }

        /// <summary>
        /// Artifact storage (local or S3) and retention.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null.</exception>
        public StorageSettings Storage
        {
            get { return _Storage; }
            set { _Storage = value ?? throw new ArgumentNullException(nameof(Storage)); }
        }

        /// <summary>
        /// Scheduler and run workers.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null.</exception>
        public SchedulerSettings Scheduler
        {
            get { return _Scheduler; }
            set { _Scheduler = value ?? throw new ArgumentNullException(nameof(Scheduler)); }
        }

        /// <summary>
        /// Search and fetch.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null.</exception>
        public SearchSettings Search
        {
            get { return _Search; }
            set { _Search = value ?? throw new ArgumentNullException(nameof(Search)); }
        }

        #endregion

        #region Private-Members

        private RestSettings _Rest = new RestSettings();
        private DatabaseSettings _Database = new DatabaseSettings();
        private LoggingSettings _Logging = new LoggingSettings();
        private AuthSettings _Auth = new AuthSettings();
        private RequestHistorySettings _RequestHistory = new RequestHistorySettings();
        private TelemetrySettings _Telemetry = new TelemetrySettings();
        private PipelineSettings _Pipeline = new PipelineSettings();
        private DiagnosticsSettings _Diagnostics = new DiagnosticsSettings();
        private ModelSettings _Models = new ModelSettings();
        private PushSettings _Push = new PushSettings();
        private StorageSettings _Storage = new StorageSettings();
        private SchedulerSettings _Scheduler = new SchedulerSettings();
        private SearchSettings _Search = new SearchSettings();

        #endregion
    }
}
