namespace Test.Shared.Helpers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using Npgsql;

    /// <summary>
    /// Creates uniquely named test databases from <see cref="TestEnvironment.ConnectionString"/> and drops them afterwards.
    /// </summary>
    public static class TestDatabase
    {
        /// <summary>
        /// Database settings for a new, uniquely named database (created on first initialization by the driver).
        /// </summary>
        /// <param name="prefix">Name prefix.</param>
        /// <returns>Settings.</returns>
        public static DatabaseSettings NewSettings(string prefix = "mmn_test")
        {
            NpgsqlConnectionStringBuilder b = new NpgsqlConnectionStringBuilder(TestEnvironment.ConnectionString);
            return new DatabaseSettings
            {
                Server = String.IsNullOrEmpty(b.Host) ? "127.0.0.1" : b.Host,
                Port = b.Port > 0 ? b.Port : 5432,
                Username = String.IsNullOrEmpty(b.Username) ? "postgres" : b.Username,
                Password = b.Password ?? String.Empty,
                DatabaseName = prefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 12),
                MaxPoolSize = 20
            };
        }

        /// <summary>
        /// Drop a database, terminating open connections.
        /// </summary>
        /// <param name="settings">Settings naming the database.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public static async Task DropAsync(DatabaseSettings settings, CancellationToken token = default)
        {
            NpgsqlConnection.ClearAllPools();
            using (NpgsqlConnection conn = new NpgsqlConnection(settings.ToConnectionString("postgres")))
            {
                await conn.OpenAsync(token).ConfigureAwait(false);
                string name = settings.DatabaseName.Replace("\"", "\"\"", StringComparison.Ordinal);
                using (NpgsqlCommand cmd = new NpgsqlCommand("DROP DATABASE IF EXISTS \"" + name + "\" WITH (FORCE)", conn))
                {
                    await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }
    }
}
