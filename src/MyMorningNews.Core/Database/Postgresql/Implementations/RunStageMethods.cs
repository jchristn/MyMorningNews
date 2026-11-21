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
    /// PostgreSQL implementation of <see cref="IRunStageMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class RunStageMethods : PostgresqlMethodsBase, IRunStageMethods
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
        public RunStageMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<RunStage> CreateAsync(RunStage obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateRunStageId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(RunStageQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.RunId,
                obj.Stage,
                obj.Status.ToString(),
                obj.StartedUtc.HasValue ? DateTime.SpecifyKind(obj.StartedUtc.Value, DateTimeKind.Utc) : null,
                obj.CompletedUtc.HasValue ? DateTime.SpecifyKind(obj.CompletedUtc.Value, DateTimeKind.Utc) : null,
                obj.DurationMs,
                obj.ItemsIn,
                obj.ItemsOut,
                obj.ErrorMessage,
                obj.SortOrder,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc),
                obj.LlmCallCount,
                obj.UnpricedCallCount,
                obj.InputTokens,
                obj.OutputTokens,
                obj.CachedInputTokens,
                obj.ReasoningTokens,
                obj.EstimatedCostUsd).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<RunStage?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<RunStage> rows = await QueryAsync(RunStageQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<RunStage?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<RunStage> rows = await QueryAsync(RunStageQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<RunStage> UpdateAsync(RunStage obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(RunStageQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.RunId,
                obj.Stage,
                obj.Status.ToString(),
                obj.StartedUtc.HasValue ? DateTime.SpecifyKind(obj.StartedUtc.Value, DateTimeKind.Utc) : null,
                obj.CompletedUtc.HasValue ? DateTime.SpecifyKind(obj.CompletedUtc.Value, DateTimeKind.Utc) : null,
                obj.DurationMs,
                obj.ItemsIn,
                obj.ItemsOut,
                obj.ErrorMessage,
                obj.SortOrder,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc),
                obj.LlmCallCount,
                obj.UnpricedCallCount,
                obj.InputTokens,
                obj.OutputTokens,
                obj.CachedInputTokens,
                obj.ReasoningTokens,
                obj.EstimatedCostUsd).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("RunStage " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(RunStageQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(RunStageQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(RunStageQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<RunStage>> EnumerateAsync(RunStageFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "sortorder";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + RunStageQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + RunStageQueries.Columns + " FROM " + RunStageQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<RunStage> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<RunStage>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(RunStageFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + RunStageQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="RunStageQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static RunStage Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            RunStage obj = new RunStage();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.RunId = (reader.IsDBNull(2) ? String.Empty : reader.GetString(2));
            obj.Stage = (reader.IsDBNull(3) ? String.Empty : reader.GetString(3));
            obj.Status = Enum.Parse<StageStatusEnum>(reader.GetString(4));
            obj.StartedUtc = (reader.IsDBNull(5) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc));
            obj.CompletedUtc = (reader.IsDBNull(6) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(6), DateTimeKind.Utc));
            obj.DurationMs = reader.GetDouble(7);
            obj.ItemsIn = reader.GetInt32(8);
            obj.ItemsOut = reader.GetInt32(9);
            obj.ErrorMessage = (reader.IsDBNull(10) ? null : reader.GetString(10));
            obj.SortOrder = reader.GetInt32(11);
            obj.Active = reader.GetBoolean(12);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(13), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(14), DateTimeKind.Utc);
            obj.LlmCallCount = reader.GetInt32(15);
            obj.UnpricedCallCount = reader.GetInt32(16);
            obj.InputTokens = reader.GetInt32(17);
            obj.OutputTokens = reader.GetInt32(18);
            obj.CachedInputTokens = reader.GetInt32(19);
            obj.ReasoningTokens = reader.GetInt32(20);
            obj.EstimatedCostUsd = reader.IsDBNull(21) ? null : reader.GetDecimal(21);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(RunStageFilter filter, List<object?> args)
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
            if (!String.IsNullOrWhiteSpace(filter.Stage))
            {
                args.Add(filter.Stage);
                clauses.Add("stage = $" + args.Count);
            }
            if (filter.Status.HasValue)
            {
                args.Add(filter.Status.Value.ToString());
                clauses.Add("status = $" + args.Count);
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
