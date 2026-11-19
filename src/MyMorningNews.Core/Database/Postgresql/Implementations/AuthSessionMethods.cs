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
    /// PostgreSQL implementation of <see cref="IAuthSessionMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class AuthSessionMethods : PostgresqlMethodsBase, IAuthSessionMethods
    {
        #region Private-Members

        private static readonly Dictionary<string, string> _SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "expiresUtc", "expiresutc" },
            { "createdUtc", "createdutc" },
            { "lastUpdateUtc", "lastupdateutc" }
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL driver.</param>
        public AuthSessionMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<AuthSession> CreateAsync(AuthSession obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateAuthSessionId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(AuthSessionQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.UserId,
                obj.CredentialId,
                obj.PrincipalType.ToString(),
                obj.AuthScheme.ToString(),
                obj.TokenId,
                obj.SourceIp,
                obj.UserAgent,
                DateTime.SpecifyKind(obj.ExpiresUtc, DateTimeKind.Utc),
                obj.LastUsedUtc.HasValue ? DateTime.SpecifyKind(obj.LastUsedUtc.Value, DateTimeKind.Utc) : null,
                obj.RevokedUtc.HasValue ? DateTime.SpecifyKind(obj.RevokedUtc.Value, DateTimeKind.Utc) : null,
                obj.RevocationReason,
                obj.IsProtected,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<AuthSession?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<AuthSession> rows = await QueryAsync(AuthSessionQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<AuthSession?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<AuthSession> rows = await QueryAsync(AuthSessionQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<AuthSession> UpdateAsync(AuthSession obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(AuthSessionQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.UserId,
                obj.CredentialId,
                obj.PrincipalType.ToString(),
                obj.AuthScheme.ToString(),
                obj.TokenId,
                obj.SourceIp,
                obj.UserAgent,
                DateTime.SpecifyKind(obj.ExpiresUtc, DateTimeKind.Utc),
                obj.LastUsedUtc.HasValue ? DateTime.SpecifyKind(obj.LastUsedUtc.Value, DateTimeKind.Utc) : null,
                obj.RevokedUtc.HasValue ? DateTime.SpecifyKind(obj.RevokedUtc.Value, DateTimeKind.Utc) : null,
                obj.RevocationReason,
                obj.IsProtected,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("AuthSession " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(AuthSessionQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(AuthSessionQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(AuthSessionQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<AuthSession>> EnumerateAsync(AuthSessionFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + AuthSessionQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + AuthSessionQueries.Columns + " FROM " + AuthSessionQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<AuthSession> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<AuthSession>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(AuthSessionFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + AuthSessionQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<int> DeleteExpiredAsync(DateTime olderThanUtc, CancellationToken token = default)
        {
            DateTime cutoff = DateTime.SpecifyKind(olderThanUtc, DateTimeKind.Utc);
            return await ExecuteAsync("DELETE FROM authsessions WHERE expiresutc < $1 OR (revokedutc IS NOT NULL AND revokedutc < $1)", token, cutoff).ConfigureAwait(false);
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="AuthSessionQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static AuthSession Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            AuthSession obj = new AuthSession();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.UserId = (reader.IsDBNull(2) ? null : reader.GetString(2));
            obj.CredentialId = (reader.IsDBNull(3) ? null : reader.GetString(3));
            obj.PrincipalType = Enum.Parse<PrincipalTypeEnum>(reader.GetString(4));
            obj.AuthScheme = Enum.Parse<AuthSchemeEnum>(reader.GetString(5));
            obj.TokenId = (reader.IsDBNull(6) ? String.Empty : reader.GetString(6));
            obj.SourceIp = (reader.IsDBNull(7) ? null : reader.GetString(7));
            obj.UserAgent = (reader.IsDBNull(8) ? null : reader.GetString(8));
            obj.ExpiresUtc = DateTime.SpecifyKind(reader.GetDateTime(9), DateTimeKind.Utc);
            obj.LastUsedUtc = (reader.IsDBNull(10) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(10), DateTimeKind.Utc));
            obj.RevokedUtc = (reader.IsDBNull(11) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(11), DateTimeKind.Utc));
            obj.RevocationReason = (reader.IsDBNull(12) ? null : reader.GetString(12));
            obj.IsProtected = reader.GetBoolean(13);
            obj.Active = reader.GetBoolean(14);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(15), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(16), DateTimeKind.Utc);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(AuthSessionFilter filter, List<object?> args)
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
            if (!String.IsNullOrWhiteSpace(filter.CredentialId))
            {
                args.Add(filter.CredentialId);
                clauses.Add("credentialid = $" + args.Count);
            }
            if (filter.PrincipalType.HasValue)
            {
                args.Add(filter.PrincipalType.Value.ToString());
                clauses.Add("principaltype = $" + args.Count);
            }
            if (filter.Active.HasValue)
            {
                args.Add(filter.Active.Value);
                clauses.Add("active = $" + args.Count);
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
