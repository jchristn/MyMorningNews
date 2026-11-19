namespace MyMorningNews.Server
{
    using System;
    using System.IO;
    using System.Runtime.Loader;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core;
    using MyMorningNews.Core.Serialization;
    using MyMorningNews.Core.Settings;
    using SyslogLogging;

    /// <summary>
    /// Composes the application: settings, logging, and the server host, then waits for shutdown.
    /// </summary>
    public static class Bootstrapper
    {
        #region Public-Methods

        /// <summary>
        /// Run the server until a shutdown signal is received.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Process exit code.</returns>
        public static async Task<int> RunAsync(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);

            string settingsFile = Environment.GetEnvironmentVariable(Constants.EnvironmentPrefix + "SETTINGS_FILE") ?? Constants.DefaultSettingsFile;
            ServerSettings settings = LoadSettings(settingsFile);

            using (LoggingModule logging = CreateLogging())
            using (CancellationTokenSource shutdown = new CancellationTokenSource())
            {
                AppDomain.CurrentDomain.ProcessExit += (sender, e) => shutdown.Cancel();
                AssemblyLoadContext.Default.Unloading += (ctx) => shutdown.Cancel();
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    shutdown.Cancel();
                };

                logging.Info("[Bootstrapper] " + Constants.ProductName + " " + Constants.ProductVersion + " starting with settings file " + settingsFile);

                using (MyMorningNewsServer server = new MyMorningNewsServer(settings, logging))
                {
                    await server.StartAsync(shutdown.Token).ConfigureAwait(false);
                    logging.Info("[Bootstrapper] listening on " + (settings.Rest.Ssl ? "https" : "http") + "://" + settings.Rest.Hostname + ":" + settings.Rest.Port);

                    try
                    {
                        await Task.Delay(Timeout.Infinite, shutdown.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    logging.Info("[Bootstrapper] shutting down");
                    server.Stop();
                }
            }

            return 0;
        }

        #endregion

        #region Private-Methods

        private static ServerSettings LoadSettings(string settingsFile)
        {
            ServerSettings settings = new ServerSettings();

            if (File.Exists(settingsFile))
            {
                ServerSettings? loaded = Serializer.Deserialize<ServerSettings>(File.ReadAllText(settingsFile));
                if (loaded != null) settings = loaded;
            }
            else
            {
                File.WriteAllText(settingsFile, Serializer.Serialize(settings, true));
            }

            string? hostname = Environment.GetEnvironmentVariable(Constants.EnvironmentPrefix + "REST_HOSTNAME");
            if (!String.IsNullOrWhiteSpace(hostname)) settings.Rest.Hostname = hostname;

            string? port = Environment.GetEnvironmentVariable(Constants.EnvironmentPrefix + "REST_PORT");
            if (!String.IsNullOrWhiteSpace(port) && Int32.TryParse(port, out int portValue)) settings.Rest.Port = portValue;

            return settings;
        }

        private static LoggingModule CreateLogging()
        {
            LoggingModule logging = new LoggingModule();
            logging.Settings.DetectConsole = false;
            logging.Settings.EnableConsole = true;
            return logging;
        }

        #endregion
    }
}
