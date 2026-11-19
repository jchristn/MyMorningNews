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
    /// PostgreSQL implementation of <see cref="IVaultCredentialMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class VaultCredentialMethods : PostgresqlMethodsBase, IVaultCredentialMethods
    {
        #region Private-Members

        private static readonly Dictionary<string, string> _SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "name", "name" },
            { "createdUtc", "createdutc" },
            { "lastUpdateUtc", "lastupdateutc" }
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL driver.</param>
        public VaultCredentialMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<VaultCredential> CreateAsync(VaultCredential obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateVaultCredentialId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(VaultCredentialQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.Name,
                obj.Kind.ToString(),
                obj.Provider.ToString(),
                obj.HeaderName,
                obj.Username,
                obj.SecretEncrypted,
                obj.SecretLast4,
                obj.Description,
                obj.CreatedByUserId,
                obj.LastUsedUtc.HasValue ? DateTime.SpecifyKind(obj.LastUsedUtc.Value, DateTimeKind.Utc) : null,
                obj.LastRevealedUtc.HasValue ? DateTime.SpecifyKind(obj.LastRevealedUtc.Value, DateTimeKind.Utc) : null,
                obj.ExpiresUtc.HasValue ? DateTime.SpecifyKind(obj.ExpiresUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<VaultCredential?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<VaultCredential> rows = await QueryAsync(VaultCredentialQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<VaultCredential?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<VaultCredential> rows = await QueryAsync(VaultCredentialQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<VaultCredential> UpdateAsync(VaultCredential obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(VaultCredentialQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.Name,
                obj.Kind.ToString(),
                obj.Provider.ToString(),
                obj.HeaderName,
                obj.Username,
                obj.SecretEncrypted,
                obj.SecretLast4,
                obj.Description,
                obj.CreatedByUserId,
                obj.LastUsedUtc.HasValue ? DateTime.SpecifyKind(obj.LastUsedUtc.Value, DateTimeKind.Utc) : null,
                obj.LastRevealedUtc.HasValue ? DateTime.SpecifyKind(obj.LastRevealedUtc.Value, DateTimeKind.Utc) : null,
                obj.ExpiresUtc.HasValue ? DateTime.SpecifyKind(obj.ExpiresUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("VaultCredential " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(VaultCredentialQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(VaultCredentialQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(VaultCredentialQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<VaultCredential>> EnumerateAsync(VaultCredentialFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + VaultCredentialQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + VaultCredentialQueries.Columns + " FROM " + VaultCredentialQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<VaultCredential> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<VaultCredential>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(VaultCredentialFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + VaultCredentialQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="VaultCredentialQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static VaultCredential Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            VaultCredential obj = new VaultCredential();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.Name = (reader.IsDBNull(2) ? String.Empty : reader.GetString(2));
            obj.Kind = Enum.Parse<VaultCredentialKindEnum>(reader.GetString(3));
            obj.Provider = Enum.Parse<VaultProviderEnum>(reader.GetString(4));
            obj.HeaderName = (reader.IsDBNull(5) ? null : reader.GetString(5));
            obj.Username = (reader.IsDBNull(6) ? null : reader.GetString(6));
            obj.SecretEncrypted = (reader.IsDBNull(7) ? String.Empty : reader.GetString(7));
            obj.SecretLast4 = (reader.IsDBNull(8) ? String.Empty : reader.GetString(8));
            obj.Description = (reader.IsDBNull(9) ? null : reader.GetString(9));
            obj.CreatedByUserId = (reader.IsDBNull(10) ? null : reader.GetString(10));
            obj.LastUsedUtc = (reader.IsDBNull(11) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(11), DateTimeKind.Utc));
            obj.LastRevealedUtc = (reader.IsDBNull(12) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(12), DateTimeKind.Utc));
            obj.ExpiresUtc = (reader.IsDBNull(13) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(13), DateTimeKind.Utc));
            obj.Active = reader.GetBoolean(14);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(15), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(16), DateTimeKind.Utc);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(VaultCredentialFilter filter, List<object?> args)
        {
            List<string> clauses = new List<string>();
            if (String.IsNullOrWhiteSpace(filter.TenantId)) throw new ArgumentNullException(nameof(filter) + ".TenantId");
            args.Add(filter.TenantId);
            clauses.Add("tenantid = $" + args.Count);
            if (!String.IsNullOrWhiteSpace(filter.Name))
            {
                args.Add(filter.Name);
                clauses.Add("name = $" + args.Count);
            }
            if (filter.Kind.HasValue)
            {
                args.Add(filter.Kind.Value.ToString());
                clauses.Add("kind = $" + args.Count);
            }
            if (filter.Provider.HasValue)
            {
                args.Add(filter.Provider.Value.ToString());
                clauses.Add("provider = $" + args.Count);
            }
            if (filter.Active.HasValue)
            {
                args.Add(filter.Active.Value);
                clauses.Add("active = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.Search))
            {
                args.Add("%" + EscapeLike(filter.Search.Trim()) + "%");
                clauses.Add("(name ILIKE $" + args.Count + ")");
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
