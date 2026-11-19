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
    /// PostgreSQL implementation of <see cref="IAuditRecordMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class AuditRecordMethods : PostgresqlMethodsBase, IAuditRecordMethods
    {
        #region Private-Members

        private static readonly Dictionary<string, string> _SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "eventType", "eventtype" },
            { "createdUtc", "createdutc" },
            { "lastUpdateUtc", "lastupdateutc" }
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL driver.</param>
        public AuditRecordMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<AuditRecord> CreateAsync(AuditRecord obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateAuditRecordId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(AuditRecordQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.EventType.ToString(),
                obj.RequestId,
                obj.CorrelationId,
                obj.TraceId,
                obj.AuthScheme,
                obj.PrincipalType,
                obj.PrincipalId,
                obj.SessionId,
                obj.UserId,
                obj.CredentialId,
                obj.ResourceType,
                obj.ResourceId,
                obj.RequestType,
                obj.Method,
                obj.Path,
                obj.SourceIp,
                obj.AuthenticationResult,
                obj.AuthorizationResult,
                obj.Reason,
                obj.BypassReason,
                obj.RequiredPermission,
                obj.StatusCode,
                obj.Description,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<AuditRecord?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<AuditRecord> rows = await QueryAsync(AuditRecordQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<AuditRecord?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<AuditRecord> rows = await QueryAsync(AuditRecordQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<AuditRecord> UpdateAsync(AuditRecord obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(AuditRecordQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.EventType.ToString(),
                obj.RequestId,
                obj.CorrelationId,
                obj.TraceId,
                obj.AuthScheme,
                obj.PrincipalType,
                obj.PrincipalId,
                obj.SessionId,
                obj.UserId,
                obj.CredentialId,
                obj.ResourceType,
                obj.ResourceId,
                obj.RequestType,
                obj.Method,
                obj.Path,
                obj.SourceIp,
                obj.AuthenticationResult,
                obj.AuthorizationResult,
                obj.Reason,
                obj.BypassReason,
                obj.RequiredPermission,
                obj.StatusCode,
                obj.Description,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("AuditRecord " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(AuditRecordQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(AuditRecordQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(AuditRecordQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<AuditRecord>> EnumerateAsync(AuditRecordFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + AuditRecordQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + AuditRecordQueries.Columns + " FROM " + AuditRecordQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<AuditRecord> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<AuditRecord>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(AuditRecordFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + AuditRecordQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<int> DeleteOlderThanAsync(string? tenantId, DateTime olderThanUtc, CancellationToken token = default)
        {
            DateTime cutoff = DateTime.SpecifyKind(olderThanUtc, DateTimeKind.Utc);
            if (tenantId == null) return await ExecuteAsync("DELETE FROM auditrecords WHERE createdutc < $1", token, cutoff).ConfigureAwait(false);
            return await ExecuteAsync("DELETE FROM auditrecords WHERE createdutc < $1 AND tenantid = $2", token, cutoff, tenantId).ConfigureAwait(false);
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="AuditRecordQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static AuditRecord Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            AuditRecord obj = new AuditRecord();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? null : reader.GetString(1));
            obj.EventType = Enum.Parse<AuditEventTypeEnum>(reader.GetString(2));
            obj.RequestId = (reader.IsDBNull(3) ? null : reader.GetString(3));
            obj.CorrelationId = (reader.IsDBNull(4) ? null : reader.GetString(4));
            obj.TraceId = (reader.IsDBNull(5) ? null : reader.GetString(5));
            obj.AuthScheme = (reader.IsDBNull(6) ? null : reader.GetString(6));
            obj.PrincipalType = (reader.IsDBNull(7) ? null : reader.GetString(7));
            obj.PrincipalId = (reader.IsDBNull(8) ? null : reader.GetString(8));
            obj.SessionId = (reader.IsDBNull(9) ? null : reader.GetString(9));
            obj.UserId = (reader.IsDBNull(10) ? null : reader.GetString(10));
            obj.CredentialId = (reader.IsDBNull(11) ? null : reader.GetString(11));
            obj.ResourceType = (reader.IsDBNull(12) ? null : reader.GetString(12));
            obj.ResourceId = (reader.IsDBNull(13) ? null : reader.GetString(13));
            obj.RequestType = (reader.IsDBNull(14) ? null : reader.GetString(14));
            obj.Method = (reader.IsDBNull(15) ? null : reader.GetString(15));
            obj.Path = (reader.IsDBNull(16) ? null : reader.GetString(16));
            obj.SourceIp = (reader.IsDBNull(17) ? null : reader.GetString(17));
            obj.AuthenticationResult = (reader.IsDBNull(18) ? null : reader.GetString(18));
            obj.AuthorizationResult = (reader.IsDBNull(19) ? null : reader.GetString(19));
            obj.Reason = (reader.IsDBNull(20) ? null : reader.GetString(20));
            obj.BypassReason = (reader.IsDBNull(21) ? null : reader.GetString(21));
            obj.RequiredPermission = (reader.IsDBNull(22) ? null : reader.GetString(22));
            obj.StatusCode = reader.GetInt32(23);
            obj.Description = (reader.IsDBNull(24) ? null : reader.GetString(24));
            obj.Active = reader.GetBoolean(25);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(26), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(27), DateTimeKind.Utc);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(AuditRecordFilter filter, List<object?> args)
        {
            List<string> clauses = new List<string>();
            if (filter.AllTenants)
            {
            }
            else if (String.IsNullOrWhiteSpace(filter.TenantId))
            {
                clauses.Add("tenantid IS NULL");
            }
            else
            {
                args.Add(filter.TenantId);
                clauses.Add(filter.IncludeGlobal ? "(tenantid = $" + args.Count + " OR tenantid IS NULL)" : "tenantid = $" + args.Count);
            }
            if (filter.EventType.HasValue)
            {
                args.Add(filter.EventType.Value.ToString());
                clauses.Add("eventtype = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.PrincipalId))
            {
                args.Add(filter.PrincipalId);
                clauses.Add("principalid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.UserId))
            {
                args.Add(filter.UserId);
                clauses.Add("userid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.AuthorizationResult))
            {
                args.Add(filter.AuthorizationResult);
                clauses.Add("authorizationresult = $" + args.Count);
            }
            if (filter.Active.HasValue)
            {
                args.Add(filter.Active.Value);
                clauses.Add("active = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.Search))
            {
                args.Add("%" + EscapeLike(filter.Search.Trim()) + "%");
                clauses.Add("(path ILIKE $" + args.Count + " OR reason ILIKE $" + args.Count + ")");
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
