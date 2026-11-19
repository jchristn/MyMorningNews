namespace MyMorningNews.Core.Database.Postgresql
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database.Postgresql.Implementations;
    using MyMorningNews.Core.Enums;
    using Npgsql;

    /// <summary>
    /// PostgreSQL driver. Creates the database when missing, enables pgvector, and applies tracked migrations under an
    /// advisory lock so concurrent processes never race.
    /// Thread safety: safe for concurrent use after initialization.
    /// </summary>
    public class PostgresqlDatabaseDriver : DatabaseDriverBase
    {
        #region Public-Members

        /// <inheritdoc />
        public override DatabaseTypeEnum DatabaseType
        {
            get { return DatabaseTypeEnum.Postgresql; }
        }

        /// <summary>
        /// Whether the pgvector extension is available.
        /// </summary>
        public bool VectorAvailable { get; private set; } = false;

        /// <summary>
        /// Optional log callback for initialization messages.
        /// </summary>
        public Action<string>? Logger { get; set; } = null;

        #endregion

        #region Private-Members

        private const long _MigrationLockKey = 7231959011L;
        private NpgsqlDataSource? _DataSource = null;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        public PostgresqlDatabaseDriver(DatabaseSettings settings) : base(settings)
        {
            Tenants = new TenantMethods(this);
            Users = new UserMethods(this);
            Credentials = new CredentialMethods(this);
            AuthSessions = new AuthSessionMethods(this);
            Roles = new UserRoleMethods(this);
            Permissions = new PermissionMethods(this);
            RolePermissionMaps = new RolePermissionMapMethods(this);
            UserRoleAssignments = new UserRoleAssignmentMethods(this);
            CredentialScopeAssignments = new CredentialScopeAssignmentMethods(this);
            AuditRecords = new AuditRecordMethods(this);
            RequestHistory = new RequestHistoryMethods(this);
            VaultCredentials = new VaultCredentialMethods(this);
            ModelEndpoints = new ModelEndpointMethods(this);
            ModelAssignments = new ModelAssignmentMethods(this);
            LlmCalls = new LlmCallMethods(this);
            PromptTemplates = new PromptTemplateMethods(this);
            PromptTemplateVersions = new PromptTemplateVersionMethods(this);
            UsageAnalytics = new UsageAnalyticsMethods(this);
            RefreshTokens = new RefreshTokenMethods(this);
            Devices = new DeviceMethods(this);
            Topics = new TopicMethods(this);
            TopicDimensions = new TopicDimensionMethods(this);
            TopicConstraints = new TopicConstraintMethods(this);
            TopicRecipients = new TopicRecipientMethods(this);
            RefinementSessions = new RefinementSessionMethods(this);
            RefinementMessages = new RefinementMessageMethods(this);
            RefinementQuestions = new RefinementQuestionMethods(this);
            RefinementQuestionOptions = new RefinementQuestionOptionMethods(this);
            ResearchRequests = new ResearchRequestMethods(this);
            Schedules = new ScheduleMethods(this);
            Runs = new RunMethods(this);
            SearchProviders = new SearchProviderMethods(this);
            TopicSearchProviders = new TopicSearchProviderMethods(this);
            RunStages = new RunStageMethods(this);
            SearchQueries = new SearchQueryMethods(this);
            SearchResults = new SearchResultMethods(this);
            SourceItems = new SourceItemMethods(this);
            Evidence = new EvidenceMethods(this);
            Findings = new FindingMethods(this);
            FindingEvidence = new FindingEvidenceMethods(this);
            Predictions = new PredictionMethods(this);
            PredictionReviews = new PredictionReviewMethods(this);
            Suggestions = new SuggestionMethods(this);
            Embeddings = new EmbeddingMethods(this);
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        /// <exception cref="NpgsqlException">The server is unreachable or a migration fails.</exception>
        public override async Task InitializeAsync(CancellationToken token = default)
        {
            await EnsureDatabaseAsync(token).ConfigureAwait(false);

            NpgsqlDataSourceBuilder builder = new NpgsqlDataSourceBuilder(Settings.ToConnectionString());
            _DataSource = builder.Build();

            VectorAvailable = await TryEnableVectorAsync(token).ConfigureAwait(false);
            if (VectorAvailable)
            {
                await _DataSource.DisposeAsync().ConfigureAwait(false);
                NpgsqlDataSourceBuilder vectorBuilder = new NpgsqlDataSourceBuilder(Settings.ToConnectionString());
                vectorBuilder.UseVector();
                _DataSource = vectorBuilder.Build();
            }

            await ApplyMigrationsAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Execute a statement with positional parameters.
        /// </summary>
        /// <param name="sql">SQL.</param>
        /// <param name="args">Parameter values.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rows affected.</returns>
        public async Task<int> ExecuteNonQueryAsync(string sql, object?[] args, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(sql);
            ArgumentNullException.ThrowIfNull(args);
            NpgsqlDataSource source = RequireDataSource();
            using (NpgsqlCommand cmd = source.CreateCommand(sql))
            {
                AddParameters(cmd, args);
                return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Run a query and map each row.
        /// </summary>
        /// <typeparam name="T">Row type.</typeparam>
        /// <param name="sql">SQL.</param>
        /// <param name="map">Row mapper.</param>
        /// <param name="args">Parameter values.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Mapped rows.</returns>
        public async Task<List<T>> QueryAsync<T>(string sql, Func<NpgsqlDataReader, T> map, object?[] args, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(sql);
            ArgumentNullException.ThrowIfNull(map);
            ArgumentNullException.ThrowIfNull(args);
            NpgsqlDataSource source = RequireDataSource();
            List<T> rows = new List<T>();
            using (NpgsqlCommand cmd = source.CreateCommand(sql))
            {
                AddParameters(cmd, args);
                using (NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        rows.Add(map(reader));
                    }
                }
            }

            return rows;
        }

        /// <summary>
        /// Run several parameterized statements in one transaction.
        /// </summary>
        /// <param name="statements">Statements with their parameter values.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Total rows affected.</returns>
        public async Task<int> ExecuteTransactionAsync(IEnumerable<KeyValuePair<string, object?[]>> statements, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(statements);
            NpgsqlDataSource source = RequireDataSource();
            int total = 0;
            using (NpgsqlConnection conn = await source.OpenConnectionAsync(token).ConfigureAwait(false))
            using (NpgsqlTransaction tx = await conn.BeginTransactionAsync(token).ConfigureAwait(false))
            {
                foreach (KeyValuePair<string, object?[]> statement in statements)
                {
                    using (NpgsqlCommand cmd = new NpgsqlCommand(statement.Key, conn, tx))
                    {
                        AddParameters(cmd, statement.Value);
                        total += await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }
                }

                await tx.CommitAsync(token).ConfigureAwait(false);
            }

            return total;
        }

        /// <inheritdoc />
        public override async Task<DataTable> ExecuteQueryAsync(string query, bool isTransaction = false, CancellationToken token = default)
        {
            return await ExecuteQueriesAsync(new[] { query }, isTransaction, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task<DataTable> ExecuteQueriesAsync(IEnumerable<string> queries, bool isTransaction = false, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(queries);
            NpgsqlDataSource source = RequireDataSource();
            DataTable result = new DataTable();
            using (NpgsqlConnection conn = await source.OpenConnectionAsync(token).ConfigureAwait(false))
            {
                NpgsqlTransaction? tx = isTransaction ? await conn.BeginTransactionAsync(token).ConfigureAwait(false) : null;
                try
                {
                    foreach (string query in queries)
                    {
                        token.ThrowIfCancellationRequested();
                        using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn, tx))
                        using (NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
                        {
                            result = new DataTable();
                            result.Load(reader);
                        }
                    }

                    if (tx != null) await tx.CommitAsync(token).ConfigureAwait(false);
                }
                finally
                {
                    if (tx != null) await tx.DisposeAsync().ConfigureAwait(false);
                }
            }

            return result;
        }

        /// <inheritdoc />
        public override async Task CloseAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (_DataSource != null)
            {
                await _DataSource.DisposeAsync().ConfigureAwait(false);
                _DataSource = null;
            }
        }

        #endregion

        #region Protected-Methods

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing && _DataSource != null)
            {
                _DataSource.Dispose();
                _DataSource = null;
            }

            _Disposed = true;
            base.Dispose(disposing);
        }

        /// <inheritdoc />
        protected override async ValueTask DisposeAsyncCore()
        {
            if (_DataSource != null)
            {
                await _DataSource.DisposeAsync().ConfigureAwait(false);
                _DataSource = null;
            }
        }

        #endregion

        #region Private-Methods

        private NpgsqlDataSource RequireDataSource()
        {
            if (_DataSource == null) throw new InvalidOperationException("The database driver has not been initialized. Call InitializeAsync first.");
            return _DataSource;
        }

        private static void AddParameters(NpgsqlCommand cmd, object?[] args)
        {
            foreach (object? arg in args)
            {
                cmd.Parameters.Add(new NpgsqlParameter { Value = arg ?? DBNull.Value });
            }
        }

        private async Task EnsureDatabaseAsync(CancellationToken token)
        {
            using (NpgsqlConnection conn = new NpgsqlConnection(Settings.ToConnectionString("postgres")))
            {
                await conn.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlCommand check = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = $1", conn))
                {
                    check.Parameters.Add(new NpgsqlParameter { Value = Settings.DatabaseName });
                    object? exists = await check.ExecuteScalarAsync(token).ConfigureAwait(false);
                    if (exists != null) return;
                }

                string quoted = "\"" + Settings.DatabaseName.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
                using (NpgsqlCommand create = new NpgsqlCommand("CREATE DATABASE " + quoted, conn))
                {
                    try
                    {
                        await create.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                        Logger?.Invoke("created database " + Settings.DatabaseName);
                    }
                    catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.DuplicateDatabase)
                    {
                    }
                }
            }
        }

        private async Task<bool> TryEnableVectorAsync(CancellationToken token)
        {
            try
            {
                await ExecuteNonQueryAsync("CREATE EXTENSION IF NOT EXISTS vector", Array.Empty<object?>(), token).ConfigureAwait(false);
                return true;
            }
            catch (PostgresException ex)
            {
                Logger?.Invoke("pgvector is not available (" + ex.SqlState + "); vector features are disabled");
                return false;
            }
        }

        private async Task ApplyMigrationsAsync(CancellationToken token)
        {
            NpgsqlDataSource source = RequireDataSource();
            using (NpgsqlConnection conn = await source.OpenConnectionAsync(token).ConfigureAwait(false))
            {
                using (NpgsqlCommand lockCmd = new NpgsqlCommand("SELECT pg_advisory_lock(" + _MigrationLockKey + ")", conn))
                {
                    await lockCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                try
                {
                    using (NpgsqlCommand table = new NpgsqlCommand(
                        "CREATE TABLE IF NOT EXISTS schema_migrations (version INTEGER PRIMARY KEY, description TEXT NOT NULL, appliedutc TIMESTAMPTZ NOT NULL)", conn))
                    {
                        await table.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    HashSet<int> applied = new HashSet<int>();
                    using (NpgsqlCommand read = new NpgsqlCommand("SELECT version FROM schema_migrations", conn))
                    using (NpgsqlDataReader reader = await read.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false)) applied.Add(reader.GetInt32(0));
                    }

                    foreach (SchemaMigration migration in PostgresqlMigrations.All.OrderBy(m => m.Version))
                    {
                        if (applied.Contains(migration.Version)) continue;
                        using (NpgsqlTransaction tx = await conn.BeginTransactionAsync(token).ConfigureAwait(false))
                        {
                            foreach (string statement in migration.Statements)
                            {
                                using (NpgsqlCommand cmd = new NpgsqlCommand(statement, conn, tx))
                                {
                                    await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                                }
                            }

                            using (NpgsqlCommand record = new NpgsqlCommand("INSERT INTO schema_migrations (version, description, appliedutc) VALUES ($1, $2, $3)", conn, tx))
                            {
                                record.Parameters.Add(new NpgsqlParameter { Value = migration.Version });
                                record.Parameters.Add(new NpgsqlParameter { Value = migration.Description });
                                record.Parameters.Add(new NpgsqlParameter { Value = DateTime.UtcNow });
                                await record.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                            }

                            await tx.CommitAsync(token).ConfigureAwait(false);
                        }

                        Logger?.Invoke("applied schema migration " + migration.Version + ": " + migration.Description);
                        applied.Add(migration.Version);
                    }

                    SchemaVersion = applied.Count > 0 ? applied.Max() : 0;
                }
                finally
                {
                    using (NpgsqlCommand unlock = new NpgsqlCommand("SELECT pg_advisory_unlock(" + _MigrationLockKey + ")", conn))
                    {
                        await unlock.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                }
            }
        }

        #endregion
    }
}
