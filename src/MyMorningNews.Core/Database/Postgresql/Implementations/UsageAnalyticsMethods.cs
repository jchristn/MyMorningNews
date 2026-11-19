namespace MyMorningNews.Core.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database.Interfaces;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using Npgsql;

    /// <summary>
    /// PostgreSQL implementation of <see cref="IUsageAnalyticsMethods"/>.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class UsageAnalyticsMethods : PostgresqlMethodsBase, IUsageAnalyticsMethods
    {
        #region Private-Members

        private const string _Aggregates =
            "COUNT(*)::bigint, SUM(CASE WHEN success THEN 0 ELSE 1 END)::bigint, COALESCE(SUM(inputtokens),0)::bigint, COALESCE(SUM(cachedinputtokens),0)::bigint, " +
            "COALESCE(SUM(cachewritetokens),0)::bigint, COALESCE(SUM(outputtokens),0)::bigint, COALESCE(SUM(reasoningtokens),0)::bigint, COALESCE(SUM(totaltokens),0)::bigint, " +
            "COALESCE(SUM(toolcallcount),0)::bigint, COALESCE(SUM(estimatedcostusd),0)::numeric, COALESCE(AVG(durationms),0)::double precision, " +
            "AVG(timetofirsttokenms)::double precision, percentile_cont(0.95) WITHIN GROUP (ORDER BY timetofirsttokenms)::double precision";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">Driver.</param>
        public UsageAnalyticsMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<UsageReport> ReportAsync(UsageReportFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<UsageRow> totals = await QueryAsync("SELECT 'total', " + _Aggregates + " FROM llmcalls" + where, MapRow, token, args.ToArray()).ConfigureAwait(false);
            args.Add(Math.Clamp(filter.Top, 1, 1000));
            string sql = "SELECT " + KeyExpression(filter.GroupBy) + " AS k, " + _Aggregates + " FROM llmcalls" + where +
                " GROUP BY k ORDER BY 11 DESC, 2 DESC LIMIT $" + args.Count;
            List<UsageRow> rows = await QueryAsync(sql, MapRow, token, args.ToArray()).ConfigureAwait(false);

            UsageRow total = totals.FirstOrDefault() ?? new UsageRow { Key = "total" };
            foreach (UsageRow r in rows) r.CostShare = total.CostUsd > 0 ? Math.Round((double)(r.CostUsd / total.CostUsd), 4) : 0;
            await LabelAsync(filter.GroupBy, rows, token).ConfigureAwait(false);
            return new UsageReport { FromUtc = filter.FromUtc, ToUtc = filter.ToUtc, GroupBy = filter.GroupBy, Totals = total, Rows = rows };
        }

        /// <inheritdoc />
        public async Task<UsageTimeseries> TimeseriesAsync(UsageReportFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            DateTime from = DateTime.SpecifyKind(filter.FromUtc, DateTimeKind.Utc);
            DateTime to = DateTime.SpecifyKind(filter.ToUtc, DateTimeKind.Utc);
            if (to <= from) throw new ArgumentException("ToUtc must be after FromUtc.", nameof(filter));
            int minutes = Math.Clamp(filter.BucketMinutes, 1, 43200);
            long seconds = minutes * 60L;
            if ((to - from).TotalSeconds / seconds > 2000) throw new ArgumentException("The range produces more than 2000 buckets; increase BucketMinutes.", nameof(filter));

            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            args.Add(from);
            int fromIndex = args.Count;
            args.Add(seconds);
            int secIndex = args.Count;
            string sql = "SELECT FLOOR(EXTRACT(EPOCH FROM (createdutc - $" + fromIndex + ")) / $" + secIndex + ")::bigint AS b, COUNT(*)::bigint, SUM(CASE WHEN success THEN 0 ELSE 1 END)::bigint, " +
                "COALESCE(SUM(inputtokens),0)::bigint, COALESCE(SUM(outputtokens),0)::bigint, COALESCE(SUM(cachedinputtokens),0)::bigint, COALESCE(SUM(estimatedcostusd),0)::numeric, " +
                "AVG(timetofirsttokenms)::double precision FROM llmcalls" + where + " GROUP BY b";
            List<UsageBucket> raw = await QueryAsync(sql, r => new UsageBucket
            {
                BucketStartUtc = from.AddSeconds(r.GetInt64(0) * seconds),
                Calls = r.GetInt64(1),
                Failures = r.GetInt64(2),
                InputTokens = r.GetInt64(3),
                OutputTokens = r.GetInt64(4),
                CachedInputTokens = r.GetInt64(5),
                CostUsd = r.GetDecimal(6),
                AverageTimeToFirstTokenMs = r.IsDBNull(7) ? null : Math.Round(r.GetDouble(7), 1)
            }, token, args.ToArray()).ConfigureAwait(false);
            Dictionary<DateTime, UsageBucket> byStart = raw.ToDictionary(b => b.BucketStartUtc, b => b);

            UsageTimeseries series = new UsageTimeseries { FromUtc = from, ToUtc = to, BucketMinutes = minutes };
            for (DateTime start = from; start < to; start = start.AddSeconds(seconds))
            {
                UsageBucket bucket = byStart.TryGetValue(start, out UsageBucket? found) ? found : new UsageBucket { BucketStartUtc = start };
                bucket.BucketEndUtc = start.AddSeconds(seconds);
                series.Buckets.Add(bucket);
            }

            return series;
        }

        #endregion

        #region Private-Methods

        private static string KeyExpression(UsageGroupByEnum groupBy)
        {
            switch (groupBy)
            {
                case UsageGroupByEnum.User: return "COALESCE(userid, '(system)')";
                case UsageGroupByEnum.Task: return "task";
                case UsageGroupByEnum.Provider: return "COALESCE(provider, '')";
                case UsageGroupByEnum.Topic: return "COALESCE(topicid, '(none)')";
                case UsageGroupByEnum.PromptKey: return "COALESCE(promptkey, '(none)')";
                case UsageGroupByEnum.Day: return "to_char(date_trunc('day', createdutc AT TIME ZONE 'UTC'), 'YYYY-MM-DD')";
                case UsageGroupByEnum.Tenant: return "tenantid";
                default: return "COALESCE(model, '')";
            }
        }

        private static string BuildWhere(UsageReportFilter filter, List<object?> args)
        {
            List<string> clauses = new List<string>();
            if (!filter.AllTenants)
            {
                if (String.IsNullOrWhiteSpace(filter.TenantId)) throw new ArgumentNullException(nameof(filter) + ".TenantId");
                args.Add(filter.TenantId);
                clauses.Add("tenantid = $" + args.Count);
            }

            args.Add(DateTime.SpecifyKind(filter.FromUtc, DateTimeKind.Utc));
            clauses.Add("createdutc >= $" + args.Count);
            args.Add(DateTime.SpecifyKind(filter.ToUtc, DateTimeKind.Utc));
            clauses.Add("createdutc < $" + args.Count);
            if (filter.IncludeDryRun != true) clauses.Add("isdryrun = FALSE");
            if (!String.IsNullOrWhiteSpace(filter.UserId)) { args.Add(filter.UserId); clauses.Add("userid = $" + args.Count); }
            if (!String.IsNullOrWhiteSpace(filter.TopicId)) { args.Add(filter.TopicId); clauses.Add("topicid = $" + args.Count); }
            if (!String.IsNullOrWhiteSpace(filter.ModelEndpointId)) { args.Add(filter.ModelEndpointId); clauses.Add("modelendpointid = $" + args.Count); }
            if (filter.Task.HasValue) { args.Add(filter.Task.Value.ToString()); clauses.Add("task = $" + args.Count); }
            return " WHERE " + String.Join(" AND ", clauses);
        }

        private static UsageRow MapRow(NpgsqlDataReader r)
        {
            return new UsageRow
            {
                Key = r.IsDBNull(0) ? String.Empty : r.GetString(0),
                Calls = r.GetInt64(1),
                Failures = r.IsDBNull(2) ? 0 : r.GetInt64(2),
                InputTokens = r.GetInt64(3),
                CachedInputTokens = r.GetInt64(4),
                CacheWriteTokens = r.GetInt64(5),
                OutputTokens = r.GetInt64(6),
                ReasoningTokens = r.GetInt64(7),
                TotalTokens = r.GetInt64(8),
                ToolCalls = r.GetInt64(9),
                CostUsd = Math.Round(r.GetDecimal(10), 6),
                AverageDurationMs = Math.Round(r.GetDouble(11), 1),
                AverageTimeToFirstTokenMs = r.IsDBNull(12) ? null : Math.Round(r.GetDouble(12), 1),
                P95TimeToFirstTokenMs = r.IsDBNull(13) ? null : Math.Round(r.GetDouble(13), 1)
            };
        }

        private async Task LabelAsync(UsageGroupByEnum groupBy, List<UsageRow> rows, CancellationToken token)
        {
            if (groupBy != UsageGroupByEnum.User && groupBy != UsageGroupByEnum.Tenant) return;
            foreach (UsageRow row in rows)
            {
                if (groupBy == UsageGroupByEnum.User)
                {
                    if (row.Key == "(system)") { row.Label = "System"; continue; }
                    List<string> emails = await QueryAsync("SELECT email FROM users WHERE id = $1", r => r.GetString(0), token, row.Key).ConfigureAwait(false);
                    row.Label = emails.FirstOrDefault() ?? row.Key;
                }
                else
                {
                    List<string> names = await QueryAsync("SELECT name FROM tenants WHERE id = $1", r => r.GetString(0), token, row.Key).ConfigureAwait(false);
                    row.Label = names.FirstOrDefault() ?? row.Key;
                }
            }
        }

        #endregion
    }
}
