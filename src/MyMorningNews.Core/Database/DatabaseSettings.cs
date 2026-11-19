namespace MyMorningNews.Core.Database
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Settings;
    using Npgsql;

    /// <summary>
    /// Database connection settings. Changes require a restart.
    /// </summary>
    public class DatabaseSettings
    {
        #region Public-Members

        /// <summary>
        /// Provider. Default Postgresql (the only supported provider).
        /// </summary>
        [Setting(Description = "Database provider.", RestartRequired = true, EnvironmentVariable = "DB_TYPE")]
        public DatabaseTypeEnum Type { get; set; } = DatabaseTypeEnum.Postgresql;

        /// <summary>
        /// Server hostname. Default 127.0.0.1.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Database server hostname.", RestartRequired = true, EnvironmentVariable = "DB_SERVER")]
        public string Server
        {
            get
            {
                return _Server;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Server));
                _Server = value.Trim();
            }
        }

        /// <summary>
        /// Server port. Default 5432. Minimum 1, maximum 65535.
        /// </summary>
        [Setting(Description = "Database server port.", RestartRequired = true, EnvironmentVariable = "DB_PORT", Minimum = 1, Maximum = 65535)]
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
        /// Database name. Default mymorningnews.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Database name.", RestartRequired = true, EnvironmentVariable = "DB_DATABASE")]
        public string DatabaseName
        {
            get
            {
                return _DatabaseName;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(DatabaseName));
                _DatabaseName = value.Trim();
            }
        }

        /// <summary>
        /// Username. Default postgres.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Database username.", RestartRequired = true, EnvironmentVariable = "DB_USERNAME")]
        public string Username
        {
            get
            {
                return _Username;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Username));
                _Username = value.Trim();
            }
        }

        /// <summary>
        /// Password. Override with MYMORNINGNEWS_DB_PASSWORD rather than committing it.
        /// </summary>
        [Setting(Description = "Database password.", Secret = true, RestartRequired = true, EnvironmentVariable = "DB_PASSWORD")]
        public string Password { get; set; } = String.Empty;

        /// <summary>
        /// Maximum pool size. Default 50. Minimum 1, maximum 1000.
        /// </summary>
        [Setting(Description = "Maximum connection pool size.", RestartRequired = true, Minimum = 1, Maximum = 1000)]
        public int MaxPoolSize
        {
            get
            {
                return _MaxPoolSize;
            }
            set
            {
                _MaxPoolSize = Math.Clamp(value, 1, 1000);
            }
        }

        /// <summary>
        /// Command timeout in seconds. Default 60. Minimum 1, maximum 3600.
        /// </summary>
        [Setting(Description = "Command timeout in seconds.", RestartRequired = true, Minimum = 1, Maximum = 3600)]
        public int CommandTimeoutSeconds
        {
            get
            {
                return _CommandTimeoutSeconds;
            }
            set
            {
                _CommandTimeoutSeconds = Math.Clamp(value, 1, 3600);
            }
        }

        #endregion

        #region Private-Members

        private string _Server = "127.0.0.1";
        private int _Port = 5432;
        private string _DatabaseName = "mymorningnews";
        private string _Username = "postgres";
        private int _MaxPoolSize = 50;
        private int _CommandTimeoutSeconds = 60;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build an Npgsql connection string.
        /// </summary>
        /// <param name="databaseOverride">Optional database name override (for example postgres when creating databases).</param>
        /// <returns>Connection string.</returns>
        public string ToConnectionString(string? databaseOverride = null)
        {
            NpgsqlConnectionStringBuilder builder = new NpgsqlConnectionStringBuilder
            {
                Host = Server,
                Port = Port,
                Database = String.IsNullOrWhiteSpace(databaseOverride) ? DatabaseName : databaseOverride,
                Username = Username,
                Password = Password,
                MaxPoolSize = MaxPoolSize,
                CommandTimeout = CommandTimeoutSeconds
            };
            return builder.ConnectionString;
        }

        #endregion
    }
}
