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
    /// PostgreSQL implementation of <see cref="IPredictionMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class PredictionMethods : PostgresqlMethodsBase, IPredictionMethods
    {
        #region Private-Members

        private static readonly Dictionary<string, string> _SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "horizonUtc", "horizonutc" },
            { "createdUtc", "createdutc" },
            { "lastUpdateUtc", "lastupdateutc" },
            { "confidence", "confidence" },
            { "status", "status" },
            { "brierScore", "brierscore" }
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL driver.</param>
        public PredictionMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<Prediction> CreateAsync(Prediction obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GeneratePredictionId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(PredictionQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.RunId,
                obj.TopicId,
                obj.UserId,
                obj.DimensionId,
                obj.Statement,
                obj.Rationale,
                obj.Confidence,
                DateTime.SpecifyKind(obj.HorizonUtc, DateTimeKind.Utc),
                obj.Status.ToString(),
                obj.ResolvedUtc.HasValue ? DateTime.SpecifyKind(obj.ResolvedUtc.Value, DateTimeKind.Utc) : null,
                obj.ResolvedBy,
                obj.ResolutionNote,
                obj.BrierScore,
                obj.IsDryRun,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc),
                obj.DueNotifiedUtc.HasValue ? DateTime.SpecifyKind(obj.DueNotifiedUtc.Value, DateTimeKind.Utc) : null).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<Prediction?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<Prediction> rows = await QueryAsync(PredictionQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<Prediction?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<Prediction> rows = await QueryAsync(PredictionQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<Prediction> UpdateAsync(Prediction obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(PredictionQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.RunId,
                obj.TopicId,
                obj.UserId,
                obj.DimensionId,
                obj.Statement,
                obj.Rationale,
                obj.Confidence,
                DateTime.SpecifyKind(obj.HorizonUtc, DateTimeKind.Utc),
                obj.Status.ToString(),
                obj.ResolvedUtc.HasValue ? DateTime.SpecifyKind(obj.ResolvedUtc.Value, DateTimeKind.Utc) : null,
                obj.ResolvedBy,
                obj.ResolutionNote,
                obj.BrierScore,
                obj.IsDryRun,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc),
                obj.DueNotifiedUtc.HasValue ? DateTime.SpecifyKind(obj.DueNotifiedUtc.Value, DateTimeKind.Utc) : null).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("Prediction " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(PredictionQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(PredictionQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(PredictionQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<Prediction>> EnumerateAsync(PredictionFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + PredictionQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + PredictionQueries.Columns + " FROM " + PredictionQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<Prediction> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<Prediction>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(PredictionFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + PredictionQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<List<Prediction>> ListDueForNoticeAsync(DateTime nowUtc, CancellationToken token = default)
        {
            string sql = "SELECT " + PredictionQueries.Columns + " FROM predictions WHERE status = 'Open' AND isdryrun = FALSE AND active = TRUE AND duenotifiedutc IS NULL AND horizonutc <= $1 ORDER BY horizonutc LIMIT 200";
            return await QueryAsync(sql, Map, token, DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc)).ConfigureAwait(false);
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="PredictionQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static Prediction Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            Prediction obj = new Prediction();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.RunId = (reader.IsDBNull(2) ? String.Empty : reader.GetString(2));
            obj.TopicId = (reader.IsDBNull(3) ? String.Empty : reader.GetString(3));
            obj.UserId = (reader.IsDBNull(4) ? String.Empty : reader.GetString(4));
            obj.DimensionId = (reader.IsDBNull(5) ? null : reader.GetString(5));
            obj.Statement = (reader.IsDBNull(6) ? String.Empty : reader.GetString(6));
            obj.Rationale = (reader.IsDBNull(7) ? null : reader.GetString(7));
            obj.Confidence = reader.GetDouble(8);
            obj.HorizonUtc = DateTime.SpecifyKind(reader.GetDateTime(9), DateTimeKind.Utc);
            obj.Status = Enum.Parse<PredictionStatusEnum>(reader.GetString(10));
            obj.ResolvedUtc = (reader.IsDBNull(11) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(11), DateTimeKind.Utc));
            obj.ResolvedBy = (reader.IsDBNull(12) ? null : reader.GetString(12));
            obj.ResolutionNote = (reader.IsDBNull(13) ? null : reader.GetString(13));
            obj.BrierScore = (reader.IsDBNull(14) ? (double?)null : reader.GetDouble(14));
            obj.IsDryRun = reader.GetBoolean(15);
            obj.Active = reader.GetBoolean(16);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(17), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(18), DateTimeKind.Utc);
            obj.DueNotifiedUtc = (reader.IsDBNull(19) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(19), DateTimeKind.Utc));
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(PredictionFilter filter, List<object?> args)
        {
            List<string> clauses = new List<string>();
            if (String.IsNullOrWhiteSpace(filter.TenantId)) throw new ArgumentNullException(nameof(filter) + ".TenantId");
            args.Add(filter.TenantId);
            clauses.Add("tenantid = $" + args.Count);
            if (!String.IsNullOrWhiteSpace(filter.RunId))
            {
                args.Add(filter.RunId);
                clauses.Add("runid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.TopicId))
            {
                args.Add(filter.TopicId);
                clauses.Add("topicid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.UserId))
            {
                args.Add(filter.UserId);
                clauses.Add("userid = $" + args.Count);
            }
            if (filter.Status.HasValue)
            {
                args.Add(filter.Status.Value.ToString());
                clauses.Add("status = $" + args.Count);
            }
            if (filter.IsDryRun.HasValue)
            {
                args.Add(filter.IsDryRun.Value);
                clauses.Add("isdryrun = $" + args.Count);
            }
            if (filter.Active.HasValue)
            {
                args.Add(filter.Active.Value);
                clauses.Add("active = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.Search))
            {
                args.Add("%" + EscapeLike(filter.Search.Trim()) + "%");
                clauses.Add("(statement ILIKE $" + args.Count + ")");
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
