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
    /// PostgreSQL implementation of <see cref="IPromoCodeMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class PromoCodeMethods : PostgresqlMethodsBase, IPromoCodeMethods
    {
        #region Private-Members

        private static readonly Dictionary<string, string> _SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "redemptionCount", "redemptioncount" },
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
        public PromoCodeMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<PromoCode> CreateAsync(PromoCode obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GeneratePromoCodeId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(PromoCodeQueries.Insert, token,
                obj.Id,
                obj.Code,
                obj.Description,
                obj.PlanId,
                obj.DurationDays,
                obj.MaxRedemptions,
                obj.RedemptionCount,
                obj.PerUserLimit,
                obj.StartsUtc.HasValue ? DateTime.SpecifyKind(obj.StartsUtc.Value, DateTimeKind.Utc) : null,
                obj.ExpiresUtc.HasValue ? DateTime.SpecifyKind(obj.ExpiresUtc.Value, DateTimeKind.Utc) : null,
                obj.Enabled,
                obj.NewAccountsOnly,
                obj.BatchId,
                obj.CreatedByUserId,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<PromoCode?> ReadAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<PromoCode> rows = await QueryAsync(PromoCodeQueries.SelectById, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<PromoCode> UpdateAsync(PromoCode obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(PromoCodeQueries.Update, token,
                obj.Id,
                obj.Code,
                obj.Description,
                obj.PlanId,
                obj.DurationDays,
                obj.MaxRedemptions,
                obj.RedemptionCount,
                obj.PerUserLimit,
                obj.StartsUtc.HasValue ? DateTime.SpecifyKind(obj.StartsUtc.Value, DateTimeKind.Utc) : null,
                obj.ExpiresUtc.HasValue ? DateTime.SpecifyKind(obj.ExpiresUtc.Value, DateTimeKind.Utc) : null,
                obj.Enabled,
                obj.NewAccountsOnly,
                obj.BatchId,
                obj.CreatedByUserId,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("PromoCode " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(PromoCodeQueries.Delete, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(PromoCodeQueries.Exists, r => 1, token, id).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<PromoCode>> EnumerateAsync(PromoCodeFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + PromoCodeQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + PromoCodeQueries.Columns + " FROM " + PromoCodeQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<PromoCode> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<PromoCode>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(PromoCodeFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + PromoCodeQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<PromoCode?> ReadByCodeAsync(string code, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(code)) return null;
            List<PromoCode> rows = await QueryAsync("SELECT " + PromoCodeQueries.Columns + " FROM promocodes WHERE code = $1 LIMIT 1", Map, token, code.Trim().ToUpperInvariant()).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<PromoCode?> TryClaimAsync(string id, DateTime nowUtc, CancellationToken token = default)
        {
            string sql = "UPDATE promocodes SET redemptioncount = redemptioncount + 1, lastupdateutc = $2 WHERE id = $1 AND enabled = TRUE AND active = TRUE " +
                "AND (startsutc IS NULL OR startsutc <= $2) AND (expiresutc IS NULL OR expiresutc > $2) AND (maxredemptions IS NULL OR redemptioncount < maxredemptions) RETURNING " + PromoCodeQueries.Columns;
            List<PromoCode> rows = await QueryAsync(sql, Map, token, id, DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task ReleaseAsync(string id, CancellationToken token = default)
        {
            await ExecuteAsync("UPDATE promocodes SET redemptioncount = GREATEST(0, redemptioncount - 1), lastupdateutc = $2 WHERE id = $1", token, id, DateTime.UtcNow).ConfigureAwait(false);
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="PromoCodeQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static PromoCode Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            PromoCode obj = new PromoCode();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.Code = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.Description = (reader.IsDBNull(2) ? null : reader.GetString(2));
            obj.PlanId = (reader.IsDBNull(3) ? String.Empty : reader.GetString(3));
            obj.DurationDays = (reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4));
            obj.MaxRedemptions = (reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5));
            obj.RedemptionCount = reader.GetInt32(6);
            obj.PerUserLimit = reader.GetInt32(7);
            obj.StartsUtc = (reader.IsDBNull(8) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(8), DateTimeKind.Utc));
            obj.ExpiresUtc = (reader.IsDBNull(9) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(9), DateTimeKind.Utc));
            obj.Enabled = reader.GetBoolean(10);
            obj.NewAccountsOnly = reader.GetBoolean(11);
            obj.BatchId = (reader.IsDBNull(12) ? null : reader.GetString(12));
            obj.CreatedByUserId = (reader.IsDBNull(13) ? null : reader.GetString(13));
            obj.Active = reader.GetBoolean(14);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(15), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(16), DateTimeKind.Utc);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(PromoCodeFilter filter, List<object?> args)
        {
            List<string> clauses = new List<string>();
            if (!String.IsNullOrWhiteSpace(filter.Code))
            {
                args.Add(filter.Code);
                clauses.Add("code = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.PlanId))
            {
                args.Add(filter.PlanId);
                clauses.Add("planid = $" + args.Count);
            }
            if (filter.Enabled.HasValue)
            {
                args.Add(filter.Enabled.Value);
                clauses.Add("enabled = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.BatchId))
            {
                args.Add(filter.BatchId);
                clauses.Add("batchid = $" + args.Count);
            }
            if (filter.Active.HasValue)
            {
                args.Add(filter.Active.Value);
                clauses.Add("active = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.Search))
            {
                args.Add("%" + EscapeLike(filter.Search.Trim()) + "%");
                clauses.Add("(code ILIKE $" + args.Count + " OR description ILIKE $" + args.Count + ")");
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
