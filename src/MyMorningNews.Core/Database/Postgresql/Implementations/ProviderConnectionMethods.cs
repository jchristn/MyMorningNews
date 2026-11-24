namespace MyMorningNews.Core.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database.Interfaces;
    using MyMorningNews.Core.Database.Postgresql.Queries;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using Npgsql;

    /// <summary>
    /// PostgreSQL implementation of <see cref="IProviderConnectionMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class ProviderConnectionMethods : PostgresqlMethodsBase, IProviderConnectionMethods
    {
        #region Private-Members

        private static readonly Dictionary<string, string> _SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "createdUtc", "createdutc" },
            { "lastUpdateUtc", "lastupdateutc" }
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL driver.</param>
        public ProviderConnectionMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<ProviderConnection> SaveAsync(ProviderConnection connection, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(connection);
            string sql = @"INSERT INTO providerconnections (" + ProviderConnectionQueries.Columns + @")
VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,NULL,TRUE,now(),now())
ON CONFLICT (tenantid,userid,kind) DO UPDATE SET provider=EXCLUDED.provider,endpoint=EXCLUDED.endpoint,model=EXCLUDED.model,voice=EXCLUDED.voice,
 secretencrypted=CASE WHEN EXCLUDED.provider='Local' THEN NULL ELSE COALESCE(EXCLUDED.secretencrypted,providerconnections.secretencrypted) END,
 secretlast4=CASE WHEN EXCLUDED.provider='Local' THEN NULL ELSE COALESCE(EXCLUDED.secretlast4,providerconnections.secretlast4) END,
 validatedutc=NULL,active=TRUE,lastupdateutc=now()
WHERE EXCLUDED.secretencrypted IS NOT NULL OR EXCLUDED.provider='Local'
 OR (providerconnections.provider=EXCLUDED.provider AND providerconnections.endpoint IS NOT DISTINCT FROM EXCLUDED.endpoint)
RETURNING " + ProviderConnectionQueries.Columns;
            List<ProviderConnection> rows = await QueryAsync(sql, Map, token, connection.Id, connection.TenantId, connection.UserId, connection.Kind,
                connection.Provider, connection.Endpoint, connection.Model, connection.Voice, connection.SecretEncrypted, connection.SecretLast4).ConfigureAwait(false);
            return rows.FirstOrDefault() ?? throw new InvalidOperationException("Enter a new key when changing the provider or its URL.");
        }

        /// <inheritdoc />
        public async Task<bool> MarkValidatedAsync(ProviderConnection connection, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(connection);
            return await ExecuteAsync("UPDATE providerconnections SET validatedutc=now() WHERE id=$1 AND tenantid=$2 AND userid=$3 AND lastupdateutc=$4 AND active", token,
                connection.Id, connection.TenantId, connection.UserId, connection.LastUpdateUtc).ConfigureAwait(false) > 0;
        }

        /// <inheritdoc />
        public async Task<ProviderConnection> CreateAsync(ProviderConnection obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateProviderConnectionId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(ProviderConnectionQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.UserId,
                obj.Kind,
                obj.Provider,
                obj.Endpoint,
                obj.Model,
                obj.Voice,
                obj.SecretEncrypted,
                obj.SecretLast4,
                obj.ValidatedUtc.HasValue ? DateTime.SpecifyKind(obj.ValidatedUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<ProviderConnection?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<ProviderConnection> rows = await QueryAsync(ProviderConnectionQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<ProviderConnection?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<ProviderConnection> rows = await QueryAsync(ProviderConnectionQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<ProviderConnection> UpdateAsync(ProviderConnection obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(ProviderConnectionQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.UserId,
                obj.Kind,
                obj.Provider,
                obj.Endpoint,
                obj.Model,
                obj.Voice,
                obj.SecretEncrypted,
                obj.SecretLast4,
                obj.ValidatedUtc.HasValue ? DateTime.SpecifyKind(obj.ValidatedUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("ProviderConnection " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(ProviderConnectionQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(ProviderConnectionQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(ProviderConnectionQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<ProviderConnection>> EnumerateAsync(ProviderConnectionFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + ProviderConnectionQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + ProviderConnectionQueries.Columns + " FROM " + ProviderConnectionQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<ProviderConnection> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<ProviderConnection>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(ProviderConnectionFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + ProviderConnectionQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="ProviderConnectionQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static ProviderConnection Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            ProviderConnection obj = new ProviderConnection();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.UserId = (reader.IsDBNull(2) ? String.Empty : reader.GetString(2));
            obj.Kind = (reader.IsDBNull(3) ? String.Empty : reader.GetString(3));
            obj.Provider = (reader.IsDBNull(4) ? String.Empty : reader.GetString(4));
            obj.Endpoint = (reader.IsDBNull(5) ? null : reader.GetString(5));
            obj.Model = (reader.IsDBNull(6) ? String.Empty : reader.GetString(6));
            obj.Voice = (reader.IsDBNull(7) ? null : reader.GetString(7));
            obj.SecretEncrypted = (reader.IsDBNull(8) ? null : reader.GetString(8));
            obj.SecretLast4 = (reader.IsDBNull(9) ? null : reader.GetString(9));
            obj.ValidatedUtc = (reader.IsDBNull(10) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(10), DateTimeKind.Utc));
            obj.Active = reader.GetBoolean(11);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(12), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(13), DateTimeKind.Utc);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(ProviderConnectionFilter filter, List<object?> args)
        {
            List<string> clauses = new List<string>();
            if (String.IsNullOrWhiteSpace(filter.TenantId)) throw new ArgumentNullException(nameof(filter) + ".TenantId");
            args.Add(filter.TenantId);
            clauses.Add("tenantid = $" + args.Count);
            if (!String.IsNullOrWhiteSpace(filter.UserId))
            {
                args.Add(filter.UserId);
                clauses.Add("userid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.Kind))
            {
                args.Add(filter.Kind);
                clauses.Add("kind = $" + args.Count);
            }
            if (filter.Active.HasValue)
            {
                args.Add(filter.Active.Value);
                clauses.Add("active = $" + args.Count);
            }
            if (filter.CreatedAfter.HasValue)
            {
                args.Add(DateTime.SpecifyKind(filter.CreatedAfter.Value, DateTimeKind.Utc));
                clauses.Add("createdutc > $" + args.Count);
            }
            if (filter.CreatedBefore.HasValue)
            {
                args.Add(DateTime.SpecifyKind(filter.CreatedBefore.Value, DateTimeKind.Utc));
                clauses.Add("createdutc < $" + args.Count);
            }
            if (filter.FromUtc.HasValue)
            {
                args.Add(DateTime.SpecifyKind(filter.FromUtc.Value, DateTimeKind.Utc));
                clauses.Add("createdutc >= $" + args.Count);
            }
            if (filter.ToUtc.HasValue)
            {
                args.Add(DateTime.SpecifyKind(filter.ToUtc.Value, DateTimeKind.Utc));
                clauses.Add("createdutc < $" + args.Count);
            }
            return clauses.Count > 0 ? " WHERE " + String.Join(" AND ", clauses) : String.Empty;
        }

        #endregion
    }
}
