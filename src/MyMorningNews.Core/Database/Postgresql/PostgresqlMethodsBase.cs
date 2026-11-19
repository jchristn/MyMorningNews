namespace MyMorningNews.Core.Database.Postgresql
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Npgsql;

    /// <summary>
    /// Base class for PostgreSQL method implementations: parameterized execution and mapping helpers.
    /// Thread safety: safe for concurrent use; each call opens a pooled connection.
    /// </summary>
    public abstract class PostgresqlMethodsBase
    {
        #region Public-Members

        /// <summary>
        /// Owning driver.
        /// </summary>
        public PostgresqlDatabaseDriver Driver { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">Driver.</param>
        /// <exception cref="ArgumentNullException">driver is null.</exception>
        protected PostgresqlMethodsBase(PostgresqlDatabaseDriver driver)
        {
            ArgumentNullException.ThrowIfNull(driver);
            Driver = driver;
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Execute a statement with positional parameters ($1, $2, ...).
        /// </summary>
        /// <param name="sql">SQL.</param>
        /// <param name="token">Cancellation token.</param>
        /// <param name="args">Parameter values in order; null becomes DBNull.</param>
        /// <returns>Rows affected.</returns>
        protected Task<int> ExecuteAsync(string sql, CancellationToken token, params object?[] args)
        {
            return Driver.ExecuteNonQueryAsync(sql, args, token);
        }

        /// <summary>
        /// Run a query and map each row.
        /// </summary>
        /// <typeparam name="T">Row type.</typeparam>
        /// <param name="sql">SQL.</param>
        /// <param name="map">Row mapper.</param>
        /// <param name="token">Cancellation token.</param>
        /// <param name="args">Parameter values in order; null becomes DBNull.</param>
        /// <returns>Mapped rows.</returns>
        protected Task<List<T>> QueryAsync<T>(string sql, Func<NpgsqlDataReader, T> map, CancellationToken token, params object?[] args)
        {
            return Driver.QueryAsync(sql, map, args, token);
        }

        /// <summary>
        /// Escape LIKE wildcards in a search term.
        /// </summary>
        /// <param name="value">Search term.</param>
        /// <returns>Escaped term.</returns>
        protected static string EscapeLike(string value)
        {
            if (String.IsNullOrEmpty(value)) return String.Empty;
            return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
        }

        #endregion
    }
}
