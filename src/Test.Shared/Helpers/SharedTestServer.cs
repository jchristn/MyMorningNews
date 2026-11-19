namespace Test.Shared.Helpers
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Server;
    using SyslogLogging;

    /// <summary>
    /// One in-process server per test process, on its own throwaway database and a free loopback port. Cases create
    /// their own tenants and users so they never depend on each other. The database is dropped at process exit.
    /// Thread safety: initialization is serialized.
    /// </summary>
    public static class SharedTestServer
    {
        #region Public-Members

        /// <summary>
        /// Default administrator email.
        /// </summary>
        public const string AdminEmail = "admin@mymorningnews";

        /// <summary>
        /// Default administrator password.
        /// </summary>
        public const string AdminPassword = "password";

        /// <summary>
        /// System administration key used by tests.
        /// </summary>
        public const string AdminApiKey = "mymorningnews-test-admin-key-not-for-production";

        #endregion

        #region Private-Members

        private static readonly SemaphoreSlim _Lock = new SemaphoreSlim(1, 1);
        private static MyMorningNewsServer? _Server = null;
        private static DatabaseSettings? _Database = null;
        private static string _BaseUrl = String.Empty;
        private static LoggingModule? _Logging = null;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start the shared server if needed and return its base URL.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Base URL such as http://127.0.0.1:54321.</returns>
        public static async Task<string> GetBaseUrlAsync(CancellationToken token)
        {
            await GetServerAsync(token).ConfigureAwait(false);
            return _BaseUrl;
        }

        /// <summary>
        /// Start the shared server if needed and return it.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Server.</returns>
        public static async Task<MyMorningNewsServer> GetServerAsync(CancellationToken token)
        {
            if (_Server != null) return _Server;
            await _Lock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (_Server != null) return _Server;
                _Database = TestDatabase.NewSettings();
                ServerSettings settings = new ServerSettings { Database = _Database };
                settings.Rest.Hostname = "127.0.0.1";
                settings.Rest.Port = PortAllocator.GetFreePort();
                settings.Auth.AdminApiKey = AdminApiKey;
                settings.Auth.SigningKey = "mymorningnews-test-signing-key-not-for-production";
                settings.Auth.LoginRateLimitPerMinute = 1000;
                settings.Telemetry.Enabled = false;
                string file = Path.Combine(Path.GetTempPath(), "mmn-test-" + Guid.NewGuid().ToString("N") + ".json");
                SettingsService service = SettingsService.FromSettings(settings, file);

                _Logging = new LoggingModule();
                _Logging.Settings.DetectConsole = false;
                _Logging.Settings.EnableConsole = false;
                MyMorningNewsServer server = new MyMorningNewsServer(service, _Logging) { StartWorkers = false, DatabaseConnectAttempts = 3 };
                await server.StartAsync(token).ConfigureAwait(false);
                _BaseUrl = "http://127.0.0.1:" + settings.Rest.Port;
                AppDomain.CurrentDomain.ProcessExit += (s, e) => Shutdown(file);
                _Server = server;
                return server;
            }
            finally
            {
                _Lock.Release();
            }
        }

        /// <summary>
        /// A client logged in as the default administrator.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Client.</returns>
        public static async Task<TestApiClient> AdminClientAsync(CancellationToken token)
        {
            TestApiClient client = new TestApiClient(await GetBaseUrlAsync(token).ConfigureAwait(false));
            await client.LoginAsync("default", AdminEmail, AdminPassword, token).ConfigureAwait(false);
            return client;
        }

        /// <summary>
        /// An anonymous client.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Client.</returns>
        public static async Task<TestApiClient> AnonymousClientAsync(CancellationToken token)
        {
            return new TestApiClient(await GetBaseUrlAsync(token).ConfigureAwait(false));
        }

        #endregion

        #region Private-Methods

        private static void Shutdown(string settingsFile)
        {
            try
            {
                _Server?.Dispose();
                if (_Database != null) TestDatabase.DropAsync(_Database).GetAwaiter().GetResult();
                if (File.Exists(settingsFile)) File.Delete(settingsFile);
                _Logging?.Dispose();
            }
            catch (Exception)
            {
            }
        }

        #endregion
    }
}
