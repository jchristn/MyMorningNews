namespace MyMorningNews.Core.Settings
{
    using System;
    using SyslogLogging;

    /// <summary>
    /// Logging settings.
    /// </summary>
    public class LoggingSettings
    {
        #region Public-Members

        /// <summary>
        /// Write logs to the console. Default true.
        /// </summary>
        [Setting(Description = "Write logs to the console.")]
        public bool ConsoleLogging { get; set; } = true;

        /// <summary>
        /// Write logs to a file. Default false.
        /// </summary>
        [Setting(Description = "Write logs to a file.", RestartRequired = true)]
        public bool FileLogging { get; set; } = false;

        /// <summary>
        /// Log directory. Default logs.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Directory for log files.", RestartRequired = true)]
        public string LogDirectory
        {
            get
            {
                return _LogDirectory;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(LogDirectory));
                _LogDirectory = value.Trim();
            }
        }

        /// <summary>
        /// Log filename. Default mymorningnews.log.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Log filename.", RestartRequired = true)]
        public string LogFilename
        {
            get
            {
                return _LogFilename;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(LogFilename));
                _LogFilename = value.Trim();
            }
        }

        /// <summary>
        /// Minimum severity written. Default Info.
        /// </summary>
        [Setting(Description = "Minimum severity written.")]
        public Severity MinimumSeverity { get; set; } = Severity.Info;

        #endregion

        #region Private-Members

        private string _LogDirectory = "logs";
        private string _LogFilename = "mymorningnews.log";

        #endregion
    }
}
