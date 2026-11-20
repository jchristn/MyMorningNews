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
    /// PostgreSQL implementation of <see cref="IRefreshTokenMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class RefreshTokenMethods : PostgresqlMethodsBase, IRefreshTokenMethods
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
        public RefreshTokenMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<RefreshToken> CreateAsync(RefreshToken obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateRefreshTokenId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(RefreshTokenQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.UserId,
                obj.SessionId,
                obj.FamilyId,
                obj.TokenHash,
                obj.Client,
                obj.SourceIp,
                obj.UserAgent,
                DateTime.SpecifyKind(obj.ExpiresUtc, DateTimeKind.Utc),
                obj.UsedUtc.HasValue ? DateTime.SpecifyKind(obj.UsedUtc.Value, DateTimeKind.Utc) : null,
                obj.RevokedUtc.HasValue ? DateTime.SpecifyKind(obj.RevokedUtc.Value, DateTimeKind.Utc) : null,
                obj.RevocationReason,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<RefreshToken?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<RefreshToken> rows = await QueryAsync(RefreshTokenQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<RefreshToken?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<RefreshToken> rows = await QueryAsync(RefreshTokenQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<RefreshToken> UpdateAsync(RefreshToken obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(RefreshTokenQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.UserId,
                obj.SessionId,
                obj.FamilyId,
                obj.TokenHash,
                obj.Client,
                obj.SourceIp,
                obj.UserAgent,
                DateTime.SpecifyKind(obj.ExpiresUtc, DateTimeKind.Utc),
                obj.UsedUtc.HasValue ? DateTime.SpecifyKind(obj.UsedUtc.Value, DateTimeKind.Utc) : null,
                obj.RevokedUtc.HasValue ? DateTime.SpecifyKind(obj.RevokedUtc.Value, DateTimeKind.Utc) : null,
                obj.RevocationReason,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("RefreshToken " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(RefreshTokenQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(RefreshTokenQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(RefreshTokenQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<RefreshToken>> EnumerateAsync(RefreshTokenFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + RefreshTokenQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + RefreshTokenQueries.Columns + " FROM " + RefreshTokenQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<RefreshToken> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<RefreshToken>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(RefreshTokenFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + RefreshTokenQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<RefreshToken?> ReadByHashAsync(string tokenHash, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tokenHash)) throw new ArgumentNullException(nameof(tokenHash));
            List<RefreshToken> rows = await QueryAsync("SELECT " + RefreshTokenQueries.Columns + " FROM refreshtokens WHERE tokenhash = $1", Map, token, tokenHash).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<int> RevokeFamilyAsync(string familyId, string reason, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(familyId)) throw new ArgumentNullException(nameof(familyId));
            return await ExecuteAsync("UPDATE refreshtokens SET revokedutc = $2, revocationreason = $3, lastupdateutc = $2 WHERE familyid = $1 AND revokedutc IS NULL",
                token, familyId, DateTime.UtcNow, reason).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<string>> ReadFamilySessionsAsync(string familyId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(familyId)) throw new ArgumentNullException(nameof(familyId));
            return await QueryAsync("SELECT DISTINCT sessionid FROM refreshtokens WHERE familyid = $1 AND sessionid IS NOT NULL", r => r.GetString(0), token, familyId).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<int> RevokeBySessionAsync(string sessionId, string reason, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(sessionId)) throw new ArgumentNullException(nameof(sessionId));
            return await ExecuteAsync("UPDATE refreshtokens SET revokedutc = $2, revocationreason = $3, lastupdateutc = $2 WHERE sessionid = $1 AND revokedutc IS NULL",
                token, sessionId, DateTime.UtcNow, reason).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<int> DeleteExpiredAsync(DateTime olderThanUtc, CancellationToken token = default)
        {
            DateTime cutoff = DateTime.SpecifyKind(olderThanUtc, DateTimeKind.Utc);
            return await ExecuteAsync("DELETE FROM refreshtokens WHERE expiresutc < $1 OR (revokedutc IS NOT NULL AND revokedutc < $1) OR (usedutc IS NOT NULL AND usedutc < $1)", token, cutoff).ConfigureAwait(false);
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="RefreshTokenQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static RefreshToken Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            RefreshToken obj = new RefreshToken();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.UserId = (reader.IsDBNull(2) ? String.Empty : reader.GetString(2));
            obj.SessionId = (reader.IsDBNull(3) ? null : reader.GetString(3));
            obj.FamilyId = (reader.IsDBNull(4) ? String.Empty : reader.GetString(4));
            obj.TokenHash = (reader.IsDBNull(5) ? String.Empty : reader.GetString(5));
            obj.Client = (reader.IsDBNull(6) ? String.Empty : reader.GetString(6));
            obj.SourceIp = (reader.IsDBNull(7) ? null : reader.GetString(7));
            obj.UserAgent = (reader.IsDBNull(8) ? null : reader.GetString(8));
            obj.ExpiresUtc = DateTime.SpecifyKind(reader.GetDateTime(9), DateTimeKind.Utc);
            obj.UsedUtc = (reader.IsDBNull(10) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(10), DateTimeKind.Utc));
            obj.RevokedUtc = (reader.IsDBNull(11) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(11), DateTimeKind.Utc));
            obj.RevocationReason = (reader.IsDBNull(12) ? null : reader.GetString(12));
            obj.Active = reader.GetBoolean(13);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(14), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(15), DateTimeKind.Utc);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(RefreshTokenFilter filter, List<object?> args)
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
            if (!String.IsNullOrWhiteSpace(filter.SessionId))
            {
                args.Add(filter.SessionId);
                clauses.Add("sessionid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.FamilyId))
            {
                args.Add(filter.FamilyId);
                clauses.Add("familyid = $" + args.Count);
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
