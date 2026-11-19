namespace Test.Shared
{
    using System;

    /// <summary>
    /// Test environment configuration: database provider and connection string.
    /// Set by the runner (Test.Automated strips --provider) or from environment variables (Test.Xunit3).
    /// </summary>
    public static class TestEnvironment
    {
        /// <summary>
        /// Database provider name. Only "postgresql" is supported.
        /// </summary>
        public static string Provider { get; set; } = Environment.GetEnvironmentVariable("MYMORNINGNEWS_TEST_PROVIDER") ?? "postgresql";

        /// <summary>
        /// Base connection string without a database name. Each suite creates and drops its own database.
        /// </summary>
        public static string ConnectionString { get; set; } =
            Environment.GetEnvironmentVariable("MYMORNINGNEWS_TEST_DB") ?? "Host=127.0.0.1;Port=5432;Username=postgres;Password=Test_Passw0rd";

        /// <summary>
        /// Whether the provider name is recognized.
        /// </summary>
        /// <param name="provider">Provider name.</param>
        /// <returns>True when supported.</returns>
        public static bool IsSupportedProvider(string? provider)
        {
            return String.Equals(provider, "postgresql", StringComparison.OrdinalIgnoreCase);
        }
    }
}
