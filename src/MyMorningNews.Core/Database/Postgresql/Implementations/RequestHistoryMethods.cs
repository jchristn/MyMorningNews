namespace MyMorningNews.Core.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database.Interfaces;
    using MyMorningNews.Core.Database.Postgresql.Queries;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Serialization;
    using Npgsql;

    /// <summary>
    /// PostgreSQL implementation of <see cref="IRequestHistoryMethods"/>.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class RequestHistoryMethods : PostgresqlMethodsBase, IRequestHistoryMethods
    {
        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">Driver.</param>
        public RequestHistoryMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task CreateAsync(RequestHistoryEntry entry, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(entry);
            await ExecuteAsync(RequestHistoryQueries.Insert, token,
                entry.Id, entry.TenantId, entry.UserId, entry.PrincipalName, entry.Method, entry.Path, entry.RouteTemplate, entry.Url,
                entry.StatusCode, entry.DurationMs, entry.SourceIp, entry.RequestBodyBytes, entry.RequestBodyTruncated,
                entry.ResponseBodyBytes, entry.ResponseBodyTruncated,
                DateTime.SpecifyKind(entry.CreatedUtc, DateTimeKind.Utc),
                entry.CompletedUtc.HasValue ? DateTime.SpecifyKind(entry.CompletedUtc.Value, DateTimeKind.Utc) : null,
                Serializer.Serialize(entry.RequestHeaders), entry.RequestBody,
                Serializer.Serialize(entry.ResponseHeaders), entry.ResponseBody).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<RequestHistoryEntry?> ReadAsync(string? tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            string sql = "SELECT " + RequestHistoryQueries.AllColumns + " FROM requesthistory WHERE id = $1" + (tenantId != null ? " AND tenantid = $2" : String.Empty);
            object?[] args = tenantId != null ? new object?[] { id, tenantId } : new object?[] { id };
            List<RequestHistoryEntry> rows = await QueryAsync(sql, MapFull, token, args).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<RequestHistoryEntry>> EnumerateAsync(RequestHistoryFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM requesthistory" + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            string sortColumn = SortColumn(filter.SortBy);
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + RequestHistoryQueries.SummaryColumns + " FROM requesthistory" + where +
                " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<RequestHistoryEntry> items = await QueryAsync(sql, MapSummary, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<RequestHistoryEntry>.Create(items, filter, counts.FirstOrDefault(), sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<RequestHistorySummary> SummarizeAsync(RequestHistoryFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            if (!filter.FromUtc.HasValue || !filter.ToUtc.HasValue) throw new ArgumentException("FromUtc and ToUtc are required for summaries.", nameof(filter));
            DateTime from = DateTime.SpecifyKind(filter.FromUtc.Value, DateTimeKind.Utc);
            DateTime to = DateTime.SpecifyKind(filter.ToUtc.Value, DateTimeKind.Utc);
            if (to <= from) throw new ArgumentException("ToUtc must be after FromUtc.", nameof(filter));
            long bucketSeconds = filter.BucketMinutes * 60L;
            if ((to - from).TotalSeconds / bucketSeconds > 2000) throw new ArgumentException("The range produces more than 2000 buckets; increase BucketMinutes.", nameof(filter));

            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            args.Add(from);
            int fromIndex = args.Count;
            args.Add(bucketSeconds);
            int secondsIndex = args.Count;
            string sql =
                "SELECT FLOOR(EXTRACT(EPOCH FROM (createdutc - $" + fromIndex + ")) / $" + secondsIndex + ")::bigint AS bucket, " +
                "SUM(CASE WHEN statuscode < 400 THEN 1 ELSE 0 END)::bigint, SUM(CASE WHEN statuscode >= 400 THEN 1 ELSE 0 END)::bigint, " +
                "COALESCE(AVG(durationms), 0) FROM requesthistory" + where + " GROUP BY bucket";
            List<TimeBucketRow> rows = await QueryAsync(sql, r => new TimeBucketRow { Index = r.GetInt64(0), Success = r.GetInt64(1), Failure = r.GetInt64(2), Average = r.GetDouble(3) }, token, args.ToArray()).ConfigureAwait(false);
            Dictionary<long, TimeBucketRow> byIndex = rows.ToDictionary(r => r.Index, r => r);

            RequestHistorySummary summary = new RequestHistorySummary();
            double weighted = 0;
            for (DateTime start = from; start < to; start = start.AddSeconds(bucketSeconds))
            {
                long index = (long)((start - from).TotalSeconds / bucketSeconds);
                RequestHistoryBucket bucket = new RequestHistoryBucket { BucketStartUtc = start, BucketEndUtc = start.AddSeconds(bucketSeconds) };
                if (byIndex.TryGetValue(index, out TimeBucketRow? row))
                {
                    bucket.SuccessCount = row.Success;
                    bucket.FailureCount = row.Failure;
                    bucket.AverageDurationMs = Math.Round(row.Average, 2);
                    weighted += row.Average * (row.Success + row.Failure);
                }

                summary.TotalSuccess += bucket.SuccessCount;
                summary.TotalFailure += bucket.FailureCount;
                summary.Buckets.Add(bucket);
            }

            summary.TotalCount = summary.TotalSuccess + summary.TotalFailure;
            summary.AverageDurationMs = summary.TotalCount > 0 ? Math.Round(weighted / summary.TotalCount, 2) : 0;
            return summary;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string? tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            string sql = "DELETE FROM requesthistory WHERE id = $1" + (tenantId != null ? " AND tenantid = $2" : String.Empty);
            object?[] args = tenantId != null ? new object?[] { id, tenantId } : new object?[] { id };
            int affected = await ExecuteAsync(sql, token, args).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<int> DeleteManyAsync(RequestHistoryFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            return await ExecuteAsync("DELETE FROM requesthistory" + where, token, args.ToArray()).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<int> PruneAsync(DateTime olderThanUtc, CancellationToken token = default)
        {
            return await ExecuteAsync("DELETE FROM requesthistory WHERE createdutc < $1", token, DateTime.SpecifyKind(olderThanUtc, DateTimeKind.Utc)).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private static string SortColumn(string? sortBy)
        {
            switch ((sortBy ?? String.Empty).ToLowerInvariant())
            {
                case "durationms": return "durationms";
                case "statuscode": return "statuscode";
                case "method": return "method";
                case "path": return "path";
                default: return "createdutc";
            }
        }

        private static string BuildWhere(RequestHistoryFilter filter, List<object?> args)
        {
            List<string> clauses = new List<string>();
            if (!String.IsNullOrWhiteSpace(filter.TenantId))
            {
                args.Add(filter.TenantId);
                clauses.Add("tenantid = $" + args.Count);
            }

            if (!String.IsNullOrWhiteSpace(filter.UserId))
            {
                args.Add(filter.UserId);
                clauses.Add("userid = $" + args.Count);
            }

            if (!String.IsNullOrWhiteSpace(filter.Method))
            {
                args.Add(filter.Method.Trim().ToUpperInvariant());
                clauses.Add("method = $" + args.Count);
            }

            if (filter.StatusCode.HasValue)
            {
                args.Add(filter.StatusCode.Value);
                clauses.Add("statuscode = $" + args.Count);
            }

            if (filter.MinStatusCode.HasValue)
            {
                args.Add(filter.MinStatusCode.Value);
                clauses.Add("statuscode >= $" + args.Count);
            }

            if (filter.MaxStatusCode.HasValue)
            {
                args.Add(filter.MaxStatusCode.Value);
                clauses.Add("statuscode <= $" + args.Count);
            }

            if (!String.IsNullOrWhiteSpace(filter.PathContains))
            {
                args.Add("%" + EscapeLike(filter.PathContains.Trim()) + "%");
                clauses.Add("path ILIKE $" + args.Count);
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

        private static RequestHistoryEntry MapSummary(NpgsqlDataReader reader)
        {
            RequestHistoryEntry entry = new RequestHistoryEntry();
            entry.Id = reader.GetString(0);
            entry.TenantId = reader.IsDBNull(1) ? null : reader.GetString(1);
            entry.UserId = reader.IsDBNull(2) ? null : reader.GetString(2);
            entry.PrincipalName = reader.IsDBNull(3) ? null : reader.GetString(3);
            entry.Method = reader.GetString(4);
            entry.Path = reader.GetString(5);
            entry.RouteTemplate = reader.IsDBNull(6) ? null : reader.GetString(6);
            entry.Url = reader.GetString(7);
            entry.StatusCode = reader.GetInt32(8);
            entry.DurationMs = reader.GetDouble(9);
            entry.SourceIp = reader.IsDBNull(10) ? null : reader.GetString(10);
            entry.RequestBodyBytes = reader.GetInt64(11);
            entry.RequestBodyTruncated = reader.GetBoolean(12);
            entry.ResponseBodyBytes = reader.GetInt64(13);
            entry.ResponseBodyTruncated = reader.GetBoolean(14);
            entry.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(15), DateTimeKind.Utc);
            entry.CompletedUtc = reader.IsDBNull(16) ? null : DateTime.SpecifyKind(reader.GetDateTime(16), DateTimeKind.Utc);
            return entry;
        }

        private static RequestHistoryEntry MapFull(NpgsqlDataReader reader)
        {
            RequestHistoryEntry entry = MapSummary(reader);
            entry.RequestHeaders = ReadHeaders(reader, 17);
            entry.RequestBody = reader.IsDBNull(18) ? null : reader.GetString(18);
            entry.ResponseHeaders = ReadHeaders(reader, 19);
            entry.ResponseBody = reader.IsDBNull(20) ? null : reader.GetString(20);
            return entry;
        }

        private static Dictionary<string, string> ReadHeaders(NpgsqlDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return new Dictionary<string, string>();
            string json = reader.GetString(ordinal);
            if (String.IsNullOrWhiteSpace(json) || json == "null") return new Dictionary<string, string>();
            return Serializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }

        #endregion
    }
}
