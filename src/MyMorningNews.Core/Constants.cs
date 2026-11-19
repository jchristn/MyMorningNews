namespace MyMorningNews.Core
{
    /// <summary>
    /// Application-wide constant values that are part of the public contract and must not change at runtime.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Product name.
        /// </summary>
        public const string ProductName = "My Morning News";

        /// <summary>
        /// Product version.
        /// </summary>
        public const string ProductVersion = "0.1.0-alpha";

        /// <summary>
        /// Environment variable prefix.
        /// </summary>
        public const string EnvironmentPrefix = "MYMORNINGNEWS_";

        /// <summary>
        /// Default settings filename.
        /// </summary>
        public const string DefaultSettingsFile = "mymorningnews.json";

        /// <summary>
        /// Seeded default tenant identifier.
        /// </summary>
        public const string DefaultTenantId = "default";

        /// <summary>
        /// Health route path.
        /// </summary>
        public const string HealthPath = "/v1.0/api/health";
    }
}
