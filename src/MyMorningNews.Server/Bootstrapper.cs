namespace MyMorningNews.Server
{
    using System;
    using System.IO;
    using System.Runtime.Loader;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Server.Services;
    using SyslogLogging;

    /// <summary>
    /// Composes the process: settings, logging, telemetry, and the server host; waits for shutdown or restart.
    /// </summary>
    public static class Bootstrapper
    {
        #region Public-Methods

        /// <summary>
        /// Run until shutdown. Returns 0 on shutdown, the restart exit code after an operator restart, or 1 on startup failure.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Exit code.</returns>
        public static async Task<int> RunAsync(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);
            string settingsFile = Environment.GetEnvironmentVariable(Constants.EnvironmentPrefix + "SETTINGS_FILE") ?? Constants.DefaultSettingsFile;
            SettingsService settings = new SettingsService(settingsFile);

            using (LoggingModule logging = CreateLogging(settings.Current.Logging))
            using (TelemetryHost telemetry = new TelemetryHost())
            using (ServerLifetime lifetime = new ServerLifetime())
            {
                AppDomain.CurrentDomain.ProcessExit += (sender, e) => lifetime.RequestShutdown();
                AssemblyLoadContext.Default.Unloading += ctx => lifetime.RequestShutdown();
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    lifetime.RequestShutdown();
                };

                logging.Info("[Bootstrapper] " + Constants.ProductName + " " + Constants.ProductVersion + " starting; settings " + Path.GetFullPath(settingsFile) +
                    (settings.OverriddenPaths.Count > 0 ? "; environment overrides: " + String.Join(", ", settings.OverriddenPaths) : String.Empty));
                telemetry.Start(settings.Current.Telemetry, logging);

                using (MyMorningNewsServer server = new MyMorningNewsServer(settings, logging, lifetime))
                {
                    try
                    {
                        await server.StartAsync(lifetime.Token).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        logging.Exception(ex, "Bootstrapper", "startup failed");
                        return 1;
                    }

                    try
                    {
                        await Task.Delay(Timeout.Infinite, lifetime.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    logging.Info("[Bootstrapper] " + (lifetime.RestartRequested ? "restarting" : "shutting down"));
                    server.Stop();
                }

                return lifetime.RestartRequested ? lifetime.RestartExitCode : 0;
            }
        }

        #endregion

        #region Private-Methods

        private static LoggingModule CreateLogging(MyMorningNews.Core.Settings.LoggingSettings settings)
        {
            LoggingModule logging = settings.FileLogging
                ? new LoggingModule(Path.Combine(settings.LogDirectory, settings.LogFilename), FileLoggingMode.FileWithDate, settings.ConsoleLogging)
                : new LoggingModule();
            if (settings.FileLogging) Directory.CreateDirectory(settings.LogDirectory);
            logging.Settings.DetectConsole = false;
            logging.Settings.EnableConsole = settings.ConsoleLogging;
            logging.Settings.MinimumSeverity = settings.MinimumSeverity;
            return logging;
        }

        #endregion
    }
}
