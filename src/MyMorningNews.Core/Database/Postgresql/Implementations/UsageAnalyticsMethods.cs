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

        #region Public-Members

        /// <summary>
        /// Key of the row that combines every group beyond the requested top in a grouped time series.
        /// </summary>
        public const string OtherKey = "(other)";

        /// <summary>
        /// Separator between the endpoint identifier and the model name in a Model group key.
        /// </summary>
        public const string ModelKeySeparator = "|";

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
            List<UsageRow> totals = await QueryAsync("SELECT 'total', " + _Aggregates + " FROM llmcalls" + where, r => MapRow(r, 0), token, args.ToArray()).ConfigureAwait(false);
            args.Add(Math.Clamp(filter.Top, 1, 1000));
            string sql = "SELECT " + KeyExpression(filter.GroupBy) + " AS k, " + _Aggregates + " FROM llmcalls" + where +
                " GROUP BY k ORDER BY 11 DESC, 2 DESC LIMIT $" + args.Count;
            List<UsageRow> rows = await QueryAsync(sql, r => MapRow(r, 0), token, args.ToArray()).ConfigureAwait(false);

            UsageRow total = totals.FirstOrDefault() ?? new UsageRow { Key = "total" };
            foreach (UsageRow r in rows) r.CostShare = Share(r.CostUsd, total.CostUsd);
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
            if (filter.SeriesGroupBy == UsageGroupByEnum.Day) throw new ArgumentException("A time series is already bucketed by time; group it by another dimension.", nameof(filter));
            if (filter.SeriesGroupBy == UsageGroupByEnum.Tenant && !filter.AllTenants) throw new ArgumentException("Grouping by tenant requires AllTenants.", nameof(filter));

            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            int whereCount = args.Count;
            args.Add(from);
            int fromIndex = args.Count;
            args.Add(seconds);
            int secIndex = args.Count;
            string bucket = "FLOOR(EXTRACT(EPOCH FROM (createdutc - $" + fromIndex + ")) / $" + secIndex + ")::bigint";
            List<UsageBucket> raw = await QueryAsync("SELECT " + bucket + " AS b, 'total', " + _Aggregates + " FROM llmcalls" + where + " GROUP BY b",
                r => ToBucket(MapRow(r, 1), from.AddSeconds(r.GetInt64(0) * seconds)), token, args.ToArray()).ConfigureAwait(false);
            Dictionary<DateTime, UsageBucket> byStart = raw.ToDictionary(b => b.BucketStartUtc, b => b);

            UsageTimeseries series = new UsageTimeseries { FromUtc = from, ToUtc = to, BucketMinutes = minutes, GroupBy = filter.SeriesGroupBy };
            for (DateTime start = from; start < to; start = start.AddSeconds(seconds))
            {
                UsageBucket bucketRow = byStart.TryGetValue(start, out UsageBucket? found) ? found : new UsageBucket { BucketStartUtc = start };
                bucketRow.BucketEndUtc = start.AddSeconds(seconds);
                series.Buckets.Add(bucketRow);
            }

            if (!filter.SeriesGroupBy.HasValue) return series;

            UsageGroupByEnum groupBy = filter.SeriesGroupBy.Value;
            string key = KeyExpression(groupBy);
            List<object?> topArgs = args.Take(whereCount).ToList();
            topArgs.Add(Math.Clamp(filter.Top, 1, 1000));
            List<string> topKeys = await QueryAsync(
                "SELECT k FROM (SELECT " + key + " AS k, COALESCE(SUM(estimatedcostusd),0) AS c, COUNT(*) AS n FROM llmcalls" + where + " GROUP BY k) g ORDER BY c DESC, n DESC, k ASC LIMIT $" + topArgs.Count,
                r => r.IsDBNull(0) ? String.Empty : r.GetString(0), token, topArgs.ToArray()).ConfigureAwait(false);

            string[] keys = topKeys.ToArray();
            List<object?> groupArgs = args.Take(whereCount).ToList();
            groupArgs.Add(keys);
            List<UsageRow> groups = await QueryAsync("SELECT " + GroupedKey(key, groupArgs.Count) + " AS k, " + _Aggregates + " FROM llmcalls" + where + " GROUP BY k",
                r => MapRow(r, 0), token, groupArgs.ToArray()).ConfigureAwait(false);
            groups = groups.OrderBy(g => g.Key == OtherKey ? 1 : 0).ThenByDescending(g => g.CostUsd).ThenByDescending(g => g.Calls).ThenBy(g => g.Key, StringComparer.Ordinal).ToList();
            decimal rangeCost = groups.Sum(g => g.CostUsd);
            foreach (UsageRow g in groups) g.CostShare = Share(g.CostUsd, rangeCost);
            await LabelAsync(groupBy, groups, token).ConfigureAwait(false);
            series.Groups = groups;

            Dictionary<string, int> order = new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, UsageRow> byKey = new Dictionary<string, UsageRow>(StringComparer.Ordinal);
            for (int i = 0; i < groups.Count; i++)
            {
                order[groups[i].Key] = i;
                byKey[groups[i].Key] = groups[i];
            }

            args.Add(keys);
            Dictionary<DateTime, List<UsageRow>> rowsByStart = new Dictionary<DateTime, List<UsageRow>>();
            await QueryAsync("SELECT " + bucket + " AS b, " + GroupedKey(key, args.Count) + " AS k, " + _Aggregates + " FROM llmcalls" + where + " GROUP BY b, k",
                r =>
                {
                    DateTime start = from.AddSeconds(r.GetInt64(0) * seconds);
                    if (!rowsByStart.TryGetValue(start, out List<UsageRow>? list))
                    {
                        list = new List<UsageRow>();
                        rowsByStart[start] = list;
                    }

                    UsageRow row = MapRow(r, 1);
                    list.Add(row);
                    return row;
                }, token, args.ToArray()).ConfigureAwait(false);

            foreach (UsageBucket b in series.Buckets)
            {
                if (!rowsByStart.TryGetValue(b.BucketStartUtc, out List<UsageRow>? rows)) continue;
                foreach (UsageRow row in rows)
                {
                    row.CostShare = Share(row.CostUsd, b.CostUsd);
                    if (byKey.TryGetValue(row.Key, out UsageRow? group))
                    {
                        row.Label = group.Label;
                        row.ModelEndpointId = group.ModelEndpointId;
                        row.Model = group.Model;
                    }
                }

                b.Rows = rows.OrderBy(row => order.TryGetValue(row.Key, out int position) ? position : Int32.MaxValue).ToList();
            }

            return series;
        }

        #endregion

        #region Private-Methods

        private static string GroupedKey(string key, int keysIndex)
        {
            return "CASE WHEN " + key + " = ANY($" + keysIndex + ") THEN " + key + " ELSE '" + OtherKey + "' END";
        }

        private static double Share(decimal part, decimal whole)
        {
            return whole > 0 ? Math.Round((double)(part / whole), 4) : 0;
        }

        private static UsageBucket ToBucket(UsageRow row, DateTime start)
        {
            return new UsageBucket
            {
                BucketStartUtc = start,
                Calls = row.Calls,
                Failures = row.Failures,
                InputTokens = row.InputTokens,
                OutputTokens = row.OutputTokens,
                CachedInputTokens = row.CachedInputTokens,
                CacheWriteTokens = row.CacheWriteTokens,
                ReasoningTokens = row.ReasoningTokens,
                ToolCalls = row.ToolCalls,
                CostUsd = row.CostUsd,
                AverageTimeToFirstTokenMs = row.AverageTimeToFirstTokenMs,
                P95TimeToFirstTokenMs = row.P95TimeToFirstTokenMs
            };
        }

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
                default: return "(COALESCE(modelendpointid, '') || '" + ModelKeySeparator + "' || COALESCE(model, ''))";
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

        private static UsageRow MapRow(NpgsqlDataReader r, int o)
        {
            return new UsageRow
            {
                Key = r.IsDBNull(o) ? String.Empty : r.GetString(o),
                Calls = r.GetInt64(o + 1),
                Failures = r.IsDBNull(o + 2) ? 0 : r.GetInt64(o + 2),
                InputTokens = r.GetInt64(o + 3),
                CachedInputTokens = r.GetInt64(o + 4),
                CacheWriteTokens = r.GetInt64(o + 5),
                OutputTokens = r.GetInt64(o + 6),
                ReasoningTokens = r.GetInt64(o + 7),
                TotalTokens = r.GetInt64(o + 8),
                ToolCalls = r.GetInt64(o + 9),
                CostUsd = Math.Round(r.GetDecimal(o + 10), 6),
                AverageDurationMs = Math.Round(r.GetDouble(o + 11), 1),
                AverageTimeToFirstTokenMs = r.IsDBNull(o + 12) ? null : Math.Round(r.GetDouble(o + 12), 1),
                P95TimeToFirstTokenMs = r.IsDBNull(o + 13) ? null : Math.Round(r.GetDouble(o + 13), 1)
            };
        }

        private async Task LabelAsync(UsageGroupByEnum groupBy, List<UsageRow> rows, CancellationToken token)
        {
            foreach (UsageRow row in rows.Where(r => r.Key == OtherKey)) row.Label = "Other";
            List<UsageRow> named = rows.Where(r => r.Key != OtherKey).ToList();
            if (groupBy == UsageGroupByEnum.Model)
            {
                foreach (UsageRow row in named)
                {
                    int split = row.Key.IndexOf(ModelKeySeparator, StringComparison.Ordinal);
                    string endpointId = split < 0 ? String.Empty : row.Key.Substring(0, split);
                    row.Model = split < 0 ? row.Key : row.Key.Substring(split + 1);
                    row.ModelEndpointId = endpointId.Length == 0 ? null : endpointId;
                }

                string[] ids = named.Where(r => r.ModelEndpointId != null).Select(r => r.ModelEndpointId!).Distinct(StringComparer.Ordinal).ToArray();
                Dictionary<string, string> names = new Dictionary<string, string>(StringComparer.Ordinal);
                if (ids.Length > 0)
                {
                    List<KeyValuePair<string, string>> found = await QueryAsync("SELECT id, name FROM modelendpoints WHERE id = ANY($1)",
                        r => new KeyValuePair<string, string>(r.GetString(0), r.GetString(1)), token, new object?[] { ids }).ConfigureAwait(false);
                    foreach (KeyValuePair<string, string> pair in found) names[pair.Key] = pair.Value;
                }

                foreach (UsageRow row in named)
                {
                    string model = String.IsNullOrEmpty(row.Model) ? "(unknown model)" : row.Model;
                    row.Label = row.ModelEndpointId != null && names.TryGetValue(row.ModelEndpointId, out string? name) ? model + " (" + name + ")" : model;
                }

                return;
            }

            if (groupBy != UsageGroupByEnum.User && groupBy != UsageGroupByEnum.Tenant) return;
            foreach (UsageRow row in named)
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
