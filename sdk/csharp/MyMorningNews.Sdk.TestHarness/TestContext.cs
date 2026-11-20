namespace MyMorningNews.Sdk.TestHarness
{
    using System;

    /// <summary>
    /// State shared by the cases: settings, the signed-in administrator client, and the cleanup registry.
    /// </summary>
    public class TestContext
    {
        /// <summary>
        /// Settings.
        /// </summary>
        public TestEnvironment Environment { get; }

        /// <summary>
        /// Client signed in as the administrator (set by the login case). Null until then.
        /// </summary>
        public MyMorningNewsClient? Admin { get; set; } = null;

        /// <summary>
        /// The administrator's user identifier, set by the login case.
        /// </summary>
        public string? AdminUserId { get; set; } = null;

        /// <summary>
        /// Cleanup registry.
        /// </summary>
        public CleanupRegistry Cleanup { get; } = new CleanupRegistry();

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="environment">Settings.</param>
        /// <exception cref="ArgumentNullException">environment is null.</exception>
        public TestContext(TestEnvironment environment)
        {
            ArgumentNullException.ThrowIfNull(environment);
            Environment = environment;
        }

        /// <summary>
        /// A new, unauthenticated client for the same server. The caller disposes it.
        /// </summary>
        /// <returns>Client.</returns>
        public MyMorningNewsClient NewClient()
        {
            return new MyMorningNewsClient(Environment.BaseUrl);
        }

        /// <summary>
        /// The administrator client.
        /// </summary>
        /// <returns>Client.</returns>
        /// <exception cref="SkipTestException">The login case did not succeed.</exception>
        public MyMorningNewsClient RequireAdmin()
        {
            if (Admin == null || String.IsNullOrEmpty(Admin.Token)) throw new SkipTestException("Administrator login failed earlier.");
            return Admin;
        }

        /// <summary>
        /// A name with the harness prefix.
        /// </summary>
        /// <param name="what">Short label.</param>
        /// <returns>Name.</returns>
        public string Name(string what)
        {
            return Environment.Prefix + what + "-" + Helpers.Unique();
        }
    }
}
