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
    /// PostgreSQL implementation of <see cref="IRunMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class RunMethods : PostgresqlMethodsBase, IRunMethods
    {
        #region Private-Members

        private static readonly Dictionary<string, string> _SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "status", "status" },
            { "queuedUtc", "queuedutc" },
            { "completedUtc", "completedutc" },
            { "createdUtc", "createdutc" },
            { "lastUpdateUtc", "lastupdateutc" }
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL driver.</param>
        public RunMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<Run> CreateAsync(Run obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateRunId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(RunQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.TopicId,
                obj.UserId,
                obj.ScheduleId,
                obj.ResearchRequestId,
                obj.Trigger.ToString(),
                obj.Status.ToString(),
                obj.IsDryRun,
                obj.CancelRequested,
                DateTime.SpecifyKind(obj.QueuedUtc, DateTimeKind.Utc),
                obj.StartedUtc.HasValue ? DateTime.SpecifyKind(obj.StartedUtc.Value, DateTimeKind.Utc) : null,
                obj.CompletedUtc.HasValue ? DateTime.SpecifyKind(obj.CompletedUtc.Value, DateTimeKind.Utc) : null,
                obj.LockedUntilUtc.HasValue ? DateTime.SpecifyKind(obj.LockedUntilUtc.Value, DateTimeKind.Utc) : null,
                obj.WorkerId,
                obj.SkippedReason,
                obj.ErrorMessage,
                obj.Summary,
                obj.DurationMs,
                obj.NewFindingCount,
                obj.DuplicateCount,
                obj.SourceCount,
                obj.InputTokens,
                obj.OutputTokens,
                obj.EstimatedCostUsd,
                obj.ContextSnapshotHash,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<Run?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<Run> rows = await QueryAsync(RunQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<Run?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<Run> rows = await QueryAsync(RunQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<Run> UpdateAsync(Run obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(RunQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.TopicId,
                obj.UserId,
                obj.ScheduleId,
                obj.ResearchRequestId,
                obj.Trigger.ToString(),
                obj.Status.ToString(),
                obj.IsDryRun,
                obj.CancelRequested,
                DateTime.SpecifyKind(obj.QueuedUtc, DateTimeKind.Utc),
                obj.StartedUtc.HasValue ? DateTime.SpecifyKind(obj.StartedUtc.Value, DateTimeKind.Utc) : null,
                obj.CompletedUtc.HasValue ? DateTime.SpecifyKind(obj.CompletedUtc.Value, DateTimeKind.Utc) : null,
                obj.LockedUntilUtc.HasValue ? DateTime.SpecifyKind(obj.LockedUntilUtc.Value, DateTimeKind.Utc) : null,
                obj.WorkerId,
                obj.SkippedReason,
                obj.ErrorMessage,
                obj.Summary,
                obj.DurationMs,
                obj.NewFindingCount,
                obj.DuplicateCount,
                obj.SourceCount,
                obj.InputTokens,
                obj.OutputTokens,
                obj.EstimatedCostUsd,
                obj.ContextSnapshotHash,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("Run " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(RunQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(RunQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(RunQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<Run>> EnumerateAsync(RunFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + RunQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + RunQueries.Columns + " FROM " + RunQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<Run> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<Run>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(RunFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + RunQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<List<Run>> ClaimQueuedAsync(DateTime nowUtc, DateTime lockedUntilUtc, int limit, string workerId, IReadOnlyCollection<string>? tenantIds = null, CancellationToken token = default)
        {
            bool scoped = tenantIds != null;
            string sql = "UPDATE runs SET status = 'Running', startedutc = $1, lockeduntilutc = $2, workerid = $4, lastupdateutc = $1 WHERE id IN (SELECT id FROM runs WHERE status = 'Queued' " +
                (scoped ? "AND tenantid = ANY($5) " : String.Empty) + "ORDER BY queuedutc FOR UPDATE SKIP LOCKED LIMIT $3) RETURNING " + RunQueries.Columns;
            List<object?> args = new List<object?> { DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), DateTime.SpecifyKind(lockedUntilUtc, DateTimeKind.Utc), Math.Clamp(limit, 1, 100), workerId ?? String.Empty };
            if (scoped) args.Add(tenantIds!.ToArray());
            return await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<long> CountRunningAsync(CancellationToken token = default)
        {
            List<long> rows = await QueryAsync("SELECT COUNT(*) FROM runs WHERE status = 'Running'", r => r.GetInt64(0), token).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<bool> HasActiveRunAsync(string tenantId, string topicId, CancellationToken token = default)
        {
            List<long> rows = await QueryAsync("SELECT COUNT(*) FROM runs WHERE tenantid = $1 AND topicid = $2 AND status IN ('Queued', 'Running')", r => r.GetInt64(0), token, tenantId, topicId).ConfigureAwait(false);
            return rows.FirstOrDefault() > 0;
        }

        /// <inheritdoc />
        public async Task<int> FailAbandonedAsync(DateTime nowUtc, CancellationToken token = default)
        {
            return await ExecuteAsync("UPDATE runs SET status = 'Failed', errormessage = 'Interrupted: the worker stopped before the run finished.', completedutc = $1, lastupdateutc = $1 " +
                "WHERE status = 'Running' AND lockeduntilutc IS NOT NULL AND lockeduntilutc < $1", token, DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc)).ConfigureAwait(false);
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="RunQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static Run Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            Run obj = new Run();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.TopicId = (reader.IsDBNull(2) ? String.Empty : reader.GetString(2));
            obj.UserId = (reader.IsDBNull(3) ? String.Empty : reader.GetString(3));
            obj.ScheduleId = (reader.IsDBNull(4) ? null : reader.GetString(4));
            obj.ResearchRequestId = (reader.IsDBNull(5) ? null : reader.GetString(5));
            obj.Trigger = Enum.Parse<RunTriggerEnum>(reader.GetString(6));
            obj.Status = Enum.Parse<RunStatusEnum>(reader.GetString(7));
            obj.IsDryRun = reader.GetBoolean(8);
            obj.CancelRequested = reader.GetBoolean(9);
            obj.QueuedUtc = DateTime.SpecifyKind(reader.GetDateTime(10), DateTimeKind.Utc);
            obj.StartedUtc = (reader.IsDBNull(11) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(11), DateTimeKind.Utc));
            obj.CompletedUtc = (reader.IsDBNull(12) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(12), DateTimeKind.Utc));
            obj.LockedUntilUtc = (reader.IsDBNull(13) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(13), DateTimeKind.Utc));
            obj.WorkerId = (reader.IsDBNull(14) ? null : reader.GetString(14));
            obj.SkippedReason = (reader.IsDBNull(15) ? null : reader.GetString(15));
            obj.ErrorMessage = (reader.IsDBNull(16) ? null : reader.GetString(16));
            obj.Summary = (reader.IsDBNull(17) ? null : reader.GetString(17));
            obj.DurationMs = reader.GetDouble(18);
            obj.NewFindingCount = reader.GetInt32(19);
            obj.DuplicateCount = reader.GetInt32(20);
            obj.SourceCount = reader.GetInt32(21);
            obj.InputTokens = reader.GetInt32(22);
            obj.OutputTokens = reader.GetInt32(23);
            obj.EstimatedCostUsd = (reader.IsDBNull(24) ? (decimal?)null : reader.GetDecimal(24));
            obj.ContextSnapshotHash = (reader.IsDBNull(25) ? null : reader.GetString(25));
            obj.Active = reader.GetBoolean(26);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(27), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(28), DateTimeKind.Utc);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(RunFilter filter, List<object?> args)
        {
            List<string> clauses = new List<string>();
            if (String.IsNullOrWhiteSpace(filter.TenantId)) throw new ArgumentNullException(nameof(filter) + ".TenantId");
            args.Add(filter.TenantId);
            clauses.Add("tenantid = $" + args.Count);
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
            if (!String.IsNullOrWhiteSpace(filter.ScheduleId))
            {
                args.Add(filter.ScheduleId);
                clauses.Add("scheduleid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.ResearchRequestId))
            {
                args.Add(filter.ResearchRequestId);
                clauses.Add("researchrequestid = $" + args.Count);
            }
            if (filter.Trigger.HasValue)
            {
                args.Add(filter.Trigger.Value.ToString());
                clauses.Add("trigger = $" + args.Count);
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
