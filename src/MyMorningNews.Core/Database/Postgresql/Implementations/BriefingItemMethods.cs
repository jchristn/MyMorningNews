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
    /// PostgreSQL implementation of <see cref="IBriefingItemMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class BriefingItemMethods : PostgresqlMethodsBase, IBriefingItemMethods
    {
        #region Private-Members

        private static readonly Dictionary<string, string> _SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "sortOrder", "sortorder" },
            { "createdUtc", "createdutc" },
            { "lastUpdateUtc", "lastupdateutc" }
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL driver.</param>
        public BriefingItemMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<BriefingItem> CreateAsync(BriefingItem obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateBriefingItemId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(BriefingItemQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.BriefingId,
                obj.SectionId,
                obj.ItemType.ToString(),
                obj.ItemId,
                obj.CitationNumber,
                obj.SortOrder,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<BriefingItem?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<BriefingItem> rows = await QueryAsync(BriefingItemQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<BriefingItem?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<BriefingItem> rows = await QueryAsync(BriefingItemQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<BriefingItem> UpdateAsync(BriefingItem obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(BriefingItemQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.BriefingId,
                obj.SectionId,
                obj.ItemType.ToString(),
                obj.ItemId,
                obj.CitationNumber,
                obj.SortOrder,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("BriefingItem " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(BriefingItemQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(BriefingItemQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(BriefingItemQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<BriefingItem>> EnumerateAsync(BriefingItemFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + BriefingItemQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + BriefingItemQueries.Columns + " FROM " + BriefingItemQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<BriefingItem> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<BriefingItem>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(BriefingItemFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + BriefingItemQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="BriefingItemQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static BriefingItem Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            BriefingItem obj = new BriefingItem();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.BriefingId = (reader.IsDBNull(2) ? String.Empty : reader.GetString(2));
            obj.SectionId = (reader.IsDBNull(3) ? String.Empty : reader.GetString(3));
            obj.ItemType = Enum.Parse<BriefingItemTypeEnum>(reader.GetString(4));
            obj.ItemId = (reader.IsDBNull(5) ? String.Empty : reader.GetString(5));
            obj.CitationNumber = (reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6));
            obj.SortOrder = reader.GetInt32(7);
            obj.Active = reader.GetBoolean(8);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(9), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(10), DateTimeKind.Utc);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(BriefingItemFilter filter, List<object?> args)
        {
            List<string> clauses = new List<string>();
            if (String.IsNullOrWhiteSpace(filter.TenantId)) throw new ArgumentNullException(nameof(filter) + ".TenantId");
            args.Add(filter.TenantId);
            clauses.Add("tenantid = $" + args.Count);
            if (!String.IsNullOrWhiteSpace(filter.BriefingId))
            {
                args.Add(filter.BriefingId);
                clauses.Add("briefingid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.SectionId))
            {
                args.Add(filter.SectionId);
                clauses.Add("sectionid = $" + args.Count);
            }
            if (filter.ItemType.HasValue)
            {
                args.Add(filter.ItemType.Value.ToString());
                clauses.Add("itemtype = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.ItemId))
            {
                args.Add(filter.ItemId);
                clauses.Add("itemid = $" + args.Count);
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
