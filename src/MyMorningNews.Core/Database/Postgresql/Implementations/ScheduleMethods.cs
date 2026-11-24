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
    /// PostgreSQL implementation of <see cref="IScheduleMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class ScheduleMethods : PostgresqlMethodsBase, IScheduleMethods
    {
        #region Private-Members

        private static readonly Dictionary<string, string> _SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "nextRunUtc", "nextrunutc" },
            { "createdUtc", "createdutc" },
            { "lastUpdateUtc", "lastupdateutc" }
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL driver.</param>
        public ScheduleMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<Schedule> CreateAsync(Schedule obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateScheduleId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(ScheduleQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.OwnerType.ToString(),
                obj.OwnerId,
                obj.UserId,
                obj.Name,
                obj.IntervalType.ToString(),
                obj.IntervalValue,
                obj.MinuteOffset,
                obj.DaysOfWeekMask,
                obj.DaysOfMonth.ToArray(),
                obj.TimesOfDay.ToArray(),
                obj.CronExpression,
                obj.TimeZone,
                obj.StartUtc.HasValue ? DateTime.SpecifyKind(obj.StartUtc.Value, DateTimeKind.Utc) : null,
                obj.EndUtc.HasValue ? DateTime.SpecifyKind(obj.EndUtc.Value, DateTimeKind.Utc) : null,
                obj.RunOnceUtc.HasValue ? DateTime.SpecifyKind(obj.RunOnceUtc.Value, DateTimeKind.Utc) : null,
                obj.Enabled,
                obj.MissedRunPolicy.ToString(),
                obj.NextRunUtc.HasValue ? DateTime.SpecifyKind(obj.NextRunUtc.Value, DateTimeKind.Utc) : null,
                obj.LastRunUtc.HasValue ? DateTime.SpecifyKind(obj.LastRunUtc.Value, DateTimeKind.Utc) : null,
                obj.LockedUntilUtc.HasValue ? DateTime.SpecifyKind(obj.LockedUntilUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<Schedule?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<Schedule> rows = await QueryAsync(ScheduleQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<Schedule?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<Schedule> rows = await QueryAsync(ScheduleQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<Schedule> UpdateAsync(Schedule obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(ScheduleQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.OwnerType.ToString(),
                obj.OwnerId,
                obj.UserId,
                obj.Name,
                obj.IntervalType.ToString(),
                obj.IntervalValue,
                obj.MinuteOffset,
                obj.DaysOfWeekMask,
                obj.DaysOfMonth.ToArray(),
                obj.TimesOfDay.ToArray(),
                obj.CronExpression,
                obj.TimeZone,
                obj.StartUtc.HasValue ? DateTime.SpecifyKind(obj.StartUtc.Value, DateTimeKind.Utc) : null,
                obj.EndUtc.HasValue ? DateTime.SpecifyKind(obj.EndUtc.Value, DateTimeKind.Utc) : null,
                obj.RunOnceUtc.HasValue ? DateTime.SpecifyKind(obj.RunOnceUtc.Value, DateTimeKind.Utc) : null,
                obj.Enabled,
                obj.MissedRunPolicy.ToString(),
                obj.NextRunUtc.HasValue ? DateTime.SpecifyKind(obj.NextRunUtc.Value, DateTimeKind.Utc) : null,
                obj.LastRunUtc.HasValue ? DateTime.SpecifyKind(obj.LastRunUtc.Value, DateTimeKind.Utc) : null,
                obj.LockedUntilUtc.HasValue ? DateTime.SpecifyKind(obj.LockedUntilUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("Schedule " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(ScheduleQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(ScheduleQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(ScheduleQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<Schedule>> EnumerateAsync(ScheduleFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + ScheduleQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + ScheduleQueries.Columns + " FROM " + ScheduleQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<Schedule> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<Schedule>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(ScheduleFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + ScheduleQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<List<Schedule>> ClaimDueAsync(DateTime nowUtc, DateTime lockedUntilUtc, int limit, CancellationToken token = default)
        {
            string sql = "UPDATE schedules SET lockeduntilutc = $2 WHERE id IN (SELECT id FROM schedules WHERE enabled = TRUE AND nextrunutc IS NOT NULL AND nextrunutc <= $1 " +
                "AND (lockeduntilutc IS NULL OR lockeduntilutc < $1) ORDER BY nextrunutc FOR UPDATE SKIP LOCKED LIMIT $3) RETURNING " + ScheduleQueries.Columns;
            return await QueryAsync(sql, Map, token, DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), DateTime.SpecifyKind(lockedUntilUtc, DateTimeKind.Utc), Math.Clamp(limit, 1, 1000)).ConfigureAwait(false);
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="ScheduleQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static Schedule Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            Schedule obj = new Schedule();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.OwnerType = Enum.Parse<ScheduleOwnerTypeEnum>(reader.GetString(2));
            obj.OwnerId = (reader.IsDBNull(3) ? String.Empty : reader.GetString(3));
            obj.UserId = (reader.IsDBNull(4) ? String.Empty : reader.GetString(4));
            obj.Name = (reader.IsDBNull(5) ? null : reader.GetString(5));
            obj.IntervalType = Enum.Parse<IntervalTypeEnum>(reader.GetString(6));
            obj.IntervalValue = reader.GetInt32(7);
            obj.MinuteOffset = reader.GetInt32(8);
            obj.DaysOfWeekMask = reader.GetInt32(9);
            obj.DaysOfMonth = (reader.IsDBNull(10) ? new List<int>() : new List<int>(reader.GetFieldValue<int[]>(10)));
            obj.TimesOfDay = (reader.IsDBNull(11) ? new List<string>() : new List<string>(reader.GetFieldValue<string[]>(11)));
            obj.CronExpression = (reader.IsDBNull(12) ? null : reader.GetString(12));
            obj.TimeZone = (reader.IsDBNull(13) ? String.Empty : reader.GetString(13));
            obj.StartUtc = (reader.IsDBNull(14) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(14), DateTimeKind.Utc));
            obj.EndUtc = (reader.IsDBNull(15) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(15), DateTimeKind.Utc));
            obj.RunOnceUtc = (reader.IsDBNull(16) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(16), DateTimeKind.Utc));
            obj.Enabled = reader.GetBoolean(17);
            obj.MissedRunPolicy = Enum.Parse<MissedRunPolicyEnum>(reader.GetString(18));
            obj.NextRunUtc = (reader.IsDBNull(19) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(19), DateTimeKind.Utc));
            obj.LastRunUtc = (reader.IsDBNull(20) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(20), DateTimeKind.Utc));
            obj.LockedUntilUtc = (reader.IsDBNull(21) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(21), DateTimeKind.Utc));
            obj.Active = reader.GetBoolean(22);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(23), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(24), DateTimeKind.Utc);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(ScheduleFilter filter, List<object?> args)
        {
            List<string> clauses = new List<string>();
            if (String.IsNullOrWhiteSpace(filter.TenantId)) throw new ArgumentNullException(nameof(filter) + ".TenantId");
            args.Add(filter.TenantId);
            clauses.Add("tenantid = $" + args.Count);
            if (filter.OwnerType.HasValue)
            {
                args.Add(filter.OwnerType.Value.ToString());
                clauses.Add("ownertype = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.OwnerId))
            {
                args.Add(filter.OwnerId);
                clauses.Add("ownerid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.UserId))
            {
                args.Add(filter.UserId);
                clauses.Add("userid = $" + args.Count);
            }
            if (filter.Enabled.HasValue)
            {
                args.Add(filter.Enabled.Value);
                clauses.Add("enabled = $" + args.Count);
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
