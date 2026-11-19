namespace MyMorningNews.Core.Database
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database.Postgresql;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Composition root for the data layer.
    /// </summary>
    public static class DatabaseDriverFactory
    {
        /// <summary>
        /// Create a driver for the configured provider.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <returns>Driver (not yet initialized).</returns>
        /// <exception cref="ArgumentNullException">settings is null.</exception>
        /// <exception cref="ArgumentException">The provider is not supported.</exception>
        public static DatabaseDriverBase Create(DatabaseSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            switch (settings.Type)
            {
                case DatabaseTypeEnum.Postgresql:
                    return new PostgresqlDatabaseDriver(settings);
                default:
                    throw new ArgumentException("Unsupported database type: " + settings.Type.ToString() + ". Only Postgresql is supported.", nameof(settings));
            }
        }

        /// <summary>
        /// Create and initialize a driver.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Initialized driver.</returns>
        public static async Task<DatabaseDriverBase> CreateAndInitializeAsync(DatabaseSettings settings, CancellationToken token = default)
        {
            DatabaseDriverBase driver = Create(settings);
            await driver.InitializeAsync(token).ConfigureAwait(false);
            return driver;
        }
    }
}
