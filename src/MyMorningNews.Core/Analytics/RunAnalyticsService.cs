namespace MyMorningNews.Core.Analytics
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Scheduling;

    /// <summary>
    /// The runs report (plan section 25.6, charts 2 to 5 and 21 to 24), computed from the records at request time for
    /// a tenant, optionally a user and a topic, and a period. Dry runs are excluded.
    /// <para>
    /// Definitions. A run is in the report when it was created in the period. Stage durations use the finished
    /// (succeeded or failed) stage rows of those runs. Per run, sources examined are the Search stage's items out
    /// (the search results the run collected), sources fetched are the Fetch stage's items out (the pages it read),
    /// and sources used are the distinct source items behind at least one non-duplicate finding of the run at or
    /// above the display threshold (tiers MustRead, WorthALook, and Fyi). When a stage row is missing, examined falls
    /// back to the run's stored search results and fetched to Run.SourceCount. Provider yield credits a search result
    /// as used when a used source in the same run is the result's source item.
    /// </para>
    /// Thread safety: safe for concurrent use once configured.
    /// </summary>
    public class RunAnalyticsService
    {
        /// <summary>
        /// The most recent runs listed per run (findings by tier, duplicate share, sources). Default 60, minimum 1,
        /// maximum 500.
        /// </summary>
        public int MaxRunPoints
        {
            get
            {
                return _MaxRunPoints;
            }
            set
            {
                _MaxRunPoints = Math.Clamp(value, 1, 500);
            }
        }

        /// <summary>
        /// The most rows read from any one table for a report; larger periods are cut off at this many rows. Default
        /// 50000, minimum 1000, maximum 1000000.
        /// </summary>
        public int MaxRows
        {
            get
            {
                return _MaxRows;
            }
            set
            {
                _MaxRows = Math.Clamp(value, 1000, 1000000);
            }
        }

        /// <summary>
        /// The most days listed by day. Default 731, minimum 1, maximum 3660.
        /// </summary>
        public int MaxDays
        {
            get
            {
                return _MaxDays;
            }
            set
            {
                _MaxDays = Math.Clamp(value, 1, 3660);
            }
        }

        private const int _PageSize = 1000;
        private static readonly TimeSpan _Slack = TimeSpan.FromDays(2);
        private readonly DatabaseDriverBase _Database;
        private int _MaxRunPoints = 60;
        private int _MaxRows = 50000;
        private int _MaxDays = 731;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <exception cref="ArgumentNullException">The database is null.</exception>
        public RunAnalyticsService(DatabaseDriverBase database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        /// <summary>
        /// Build the runs report.
        /// </summary>
        /// <param name="scope">Scope.</param>
        /// <param name="timeZone">IANA time zone for days and hours. Null, empty, or unknown means UTC.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Report.</returns>
        /// <exception cref="ArgumentNullException">Scope is null.</exception>
        public async Task<RunsReport> RunsAsync(AnalyticsScope scope, string? timeZone, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(scope);
            TimeZoneInfo? found = TimeZoneHelper.TryFind(timeZone);
            TimeZoneInfo tz = found ?? TimeZoneInfo.Utc;
            DateTime fromUtc = Utc(scope.FromUtc);
            DateTime toUtc = Utc(scope.ToUtc);
            RunsReport r = new RunsReport { FromUtc = fromUtc, ToUtc = toUtc, TimeZone = found != null && timeZone != null ? timeZone.Trim() : "UTC" };

            List<Run> runs = (await RunsInScopeAsync(scope, fromUtc, toUtc, token).ConfigureAwait(false)).OrderBy(x => x.CreatedUtc).ThenBy(x => x.Id, StringComparer.Ordinal).ToList();
            HashSet<string> runIds = new HashSet<string>(runs.Select(x => x.Id), StringComparer.Ordinal);
            r.Total = runs.Count;
            List<Run> priced = runs.Where(x => x.EstimatedCostUsd.HasValue).ToList();
            r.TotalCostUsd = priced.Count > 0 ? priced.Sum(x => x.EstimatedCostUsd!.Value) : null;
            r.MeanCostPerRunUsd = priced.Count > 0 ? Math.Round(priced.Average(x => x.EstimatedCostUsd!.Value), 6) : null;
            foreach (RunStatusEnum status in Enum.GetValues<RunStatusEnum>()) r.ByStatus[status.ToString()] = runs.Count(x => x.Status == status);
            r.ByDay = Days(runs, fromUtc, toUtc, tz);
            r.Activity = Activity(runs, tz);

            DateTime readTo = toUtc + _Slack;
            List<RunStage> stages = (await StagesAsync(scope.TenantId, fromUtc, readTo, token).ConfigureAwait(false)).Where(s => runIds.Contains(s.RunId)).ToList();
            r.Stages = StageRows(stages);

            List<Finding> findings = await FindingsAsync(scope, fromUtc, readTo, token).ConfigureAwait(false);
            r.ScoreHistogram = Histogram(findings.Where(f => f.CreatedUtc >= fromUtc && f.CreatedUtc < toUtc));
            List<Finding> runFindings = findings.Where(f => runIds.Contains(f.RunId)).ToList();
            Dictionary<string, List<string>> usedBy = runFindings.Where(f => f.SourceItemId != null && f.Tier != FindingTierEnum.BelowThreshold)
                .GroupBy(f => Key(f.RunId, f.SourceItemId!), StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Select(f => f.Id).ToList(), StringComparer.Ordinal);

            List<SearchResult> results = (await ResultsAsync(scope, fromUtc, readTo, token).ConfigureAwait(false)).Where(x => runIds.Contains(x.RunId)).ToList();
            r.Providers = Providers(results, usedBy);

            List<Run> recent = runs.Skip(Math.Max(0, runs.Count - _MaxRunPoints)).ToList();
            r.RunsTruncated = runs.Count > recent.Count;
            Dictionary<string, string> names = await TopicNamesAsync(scope.TenantId, recent.Select(x => x.TopicId), token).ConfigureAwait(false);
            r.Runs = Points(recent, stages, runFindings, results, usedBy, names);
            return r;
        }

        private async Task<List<Run>> RunsInScopeAsync(AnalyticsScope scope, DateTime fromUtc, DateTime toUtc, CancellationToken token)
        {
            List<Run> all = new List<Run>();
            RunFilter filter = new RunFilter { TenantId = scope.TenantId, UserId = scope.UserId, TopicId = scope.TopicId, IsDryRun = false, FromUtc = fromUtc, ToUtc = toUtc, PageSize = _PageSize };
            for (int page = 1; all.Count < _MaxRows; page++)
            {
                token.ThrowIfCancellationRequested();
                filter.PageNumber = page;
                EnumerationResult<Run> result = await _Database.Runs.EnumerateAsync(filter, token).ConfigureAwait(false);
                all.AddRange(result.Objects);
                if (result.Objects.Count < _PageSize) break;
            }

            return all;
        }

        private async Task<List<RunStage>> StagesAsync(string tenantId, DateTime fromUtc, DateTime toUtc, CancellationToken token)
        {
            List<RunStage> all = new List<RunStage>();
            RunStageFilter filter = new RunStageFilter { TenantId = tenantId, FromUtc = fromUtc, ToUtc = toUtc, PageSize = _PageSize };
            for (int page = 1; all.Count < _MaxRows; page++)
            {
                token.ThrowIfCancellationRequested();
                filter.PageNumber = page;
                EnumerationResult<RunStage> result = await _Database.RunStages.EnumerateAsync(filter, token).ConfigureAwait(false);
                all.AddRange(result.Objects);
                if (result.Objects.Count < _PageSize) break;
            }

            return all;
        }

        private async Task<List<Finding>> FindingsAsync(AnalyticsScope scope, DateTime fromUtc, DateTime toUtc, CancellationToken token)
        {
            List<Finding> all = new List<Finding>();
            FindingFilter filter = new FindingFilter
            {
                TenantId = scope.TenantId, UserId = scope.UserId, TopicId = scope.TopicId, IsDuplicate = false, IsDryRun = false, FromUtc = fromUtc, ToUtc = toUtc, PageSize = _PageSize
            };
            for (int page = 1; all.Count < _MaxRows; page++)
            {
                token.ThrowIfCancellationRequested();
                filter.PageNumber = page;
                EnumerationResult<Finding> result = await _Database.Findings.EnumerateAsync(filter, token).ConfigureAwait(false);
                all.AddRange(result.Objects);
                if (result.Objects.Count < _PageSize) break;
            }

            return all;
        }

        private async Task<List<SearchResult>> ResultsAsync(AnalyticsScope scope, DateTime fromUtc, DateTime toUtc, CancellationToken token)
        {
            List<SearchResult> all = new List<SearchResult>();
            SearchResultFilter filter = new SearchResultFilter { TenantId = scope.TenantId, TopicId = scope.TopicId, FromUtc = fromUtc, ToUtc = toUtc, PageSize = _PageSize };
            for (int page = 1; all.Count < _MaxRows; page++)
            {
                token.ThrowIfCancellationRequested();
                filter.PageNumber = page;
                EnumerationResult<SearchResult> result = await _Database.SearchResults.EnumerateAsync(filter, token).ConfigureAwait(false);
                all.AddRange(result.Objects);
                if (result.Objects.Count < _PageSize) break;
            }

            return all;
        }

        private async Task<Dictionary<string, string>> TopicNamesAsync(string tenantId, IEnumerable<string> ids, CancellationToken token)
        {
            Dictionary<string, string> names = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string id in ids.Distinct(StringComparer.Ordinal))
            {
                Topic? t = await _Database.Topics.ReadAsync(tenantId, id, token).ConfigureAwait(false);
                if (t != null) names[id] = t.Name;
            }

            return names;
        }

        private List<RunDayRow> Days(List<Run> runs, DateTime fromUtc, DateTime toUtc, TimeZoneInfo tz)
        {
            List<RunDayRow> days = new List<RunDayRow>();
            Dictionary<string, RunDayRow> byKey = new Dictionary<string, RunDayRow>(StringComparer.Ordinal);
            DateTime local = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, tz).Date;
            while (days.Count < _MaxDays)
            {
                DateTime startUtc = ScheduleCalculator.LocalToUtc(local, tz);
                if (startUtc >= toUtc) break;
                RunDayRow row = new RunDayRow { Day = Day(local), StartUtc = startUtc, EndUtc = ScheduleCalculator.LocalToUtc(local.AddDays(1), tz), Counts = StatusCounts() };
                days.Add(row);
                byKey[row.Day] = row;
                local = local.AddDays(1);
            }

            foreach (Run run in runs)
            {
                if (!byKey.TryGetValue(Day(TimeZoneInfo.ConvertTimeFromUtc(Utc(run.CreatedUtc), tz)), out RunDayRow? row)) continue;
                row.Total++;
                row.Counts[run.Status.ToString()]++;
                if (run.EstimatedCostUsd.HasValue) row.CostUsd = (row.CostUsd ?? 0) + run.EstimatedCostUsd.Value;
            }

            return days;
        }

        private static List<ActivityCell> Activity(List<Run> runs, TimeZoneInfo tz)
        {
            List<ActivityCell> cells = new List<ActivityCell>();
            for (int d = 0; d < 7; d++)
            {
                for (int h = 0; h < 24; h++) cells.Add(new ActivityCell { DayOfWeek = d, Hour = h });
            }

            foreach (Run run in runs)
            {
                DateTime local = TimeZoneInfo.ConvertTimeFromUtc(Utc(run.CreatedUtc), tz);
                cells[(int)local.DayOfWeek * 24 + local.Hour].Count++;
            }

            return cells;
        }

        private static List<StageDurationRow> StageRows(List<RunStage> stages)
        {
            return stages.Where(s => s.Status == StageStatusEnum.Succeeded || s.Status == StageStatusEnum.Failed)
                .GroupBy(s => s.Stage, StringComparer.Ordinal)
                .Select(g =>
                {
                    List<double> sorted = g.Select(s => Math.Max(0, s.DurationMs)).OrderBy(x => x).ToList();
                    return new StageDurationRow
                    {
                        Stage = g.Key,
                        SortOrder = g.Min(s => s.SortOrder),
                        Count = sorted.Count,
                        Failed = g.Count(s => s.Status == StageStatusEnum.Failed),
                        P50Ms = Math.Round(Percentile(sorted, 0.5), 2),
                        P95Ms = Math.Round(Percentile(sorted, 0.95), 2),
                        MeanMs = Math.Round(sorted.Average(), 2),
                        MaxMs = Math.Round(sorted[sorted.Count - 1], 2),
                        TotalCostUsd = g.Any(s => s.EstimatedCostUsd.HasValue) ? g.Sum(s => s.EstimatedCostUsd ?? 0) : null,
                        MeanCostUsd = g.Any(s => s.EstimatedCostUsd.HasValue) ? Math.Round(g.Where(s => s.EstimatedCostUsd.HasValue).Average(s => s.EstimatedCostUsd!.Value), 6) : null,
                        LlmCallCount = g.Sum(s => s.LlmCallCount)
                    };
                })
                .OrderBy(x => x.SortOrder).ThenBy(x => x.Stage, StringComparer.Ordinal).ToList();
        }

        private static List<ScoreBucket> Histogram(IEnumerable<Finding> findings)
        {
            List<ScoreBucket> buckets = new List<ScoreBucket>();
            for (int i = 0; i < 10; i++) buckets.Add(new ScoreBucket { Lower = i * 10, Upper = i * 10 + 10 });
            foreach (Finding f in findings)
            {
                double score = Math.Clamp(f.RelevanceScore, 0, 100);
                buckets[Math.Min(9, (int)Math.Floor(score / 10.0))].Count++;
            }

            return buckets;
        }

        private static List<ProviderYieldRow> Providers(List<SearchResult> results, Dictionary<string, List<string>> usedBy)
        {
            return results.GroupBy(x => String.IsNullOrWhiteSpace(x.Provider) ? "unknown" : x.Provider!, StringComparer.Ordinal)
                .Select(g =>
                {
                    List<SearchResult> used = g.Where(x => x.SourceItemId != null && usedBy.ContainsKey(Key(x.RunId, x.SourceItemId))).ToList();
                    int count = g.Count();
                    return new ProviderYieldRow
                    {
                        Provider = g.Key,
                        Results = count,
                        Fetched = g.Count(x => x.Disposition == SearchResultDispositionEnum.Fetched),
                        Used = used.Count,
                        Findings = used.SelectMany(x => usedBy[Key(x.RunId, x.SourceItemId!)]).Distinct(StringComparer.Ordinal).Count(),
                        Yield = count == 0 ? null : Math.Round(used.Count / (double)count, 4)
                    };
                })
                .OrderByDescending(x => x.Results).ThenBy(x => x.Provider, StringComparer.Ordinal).ToList();
        }

        private static List<RunPointRow> Points(List<Run> runs, List<RunStage> stages, List<Finding> findings, List<SearchResult> results, Dictionary<string, List<string>> usedBy, Dictionary<string, string> names)
        {
            HashSet<string> wanted = new HashSet<string>(runs.Select(x => x.Id), StringComparer.Ordinal);
            Dictionary<string, List<RunStage>> stagesByRun = stages.Where(s => wanted.Contains(s.RunId)).GroupBy(s => s.RunId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
            Dictionary<string, List<Finding>> findingsByRun = findings.Where(f => wanted.Contains(f.RunId)).GroupBy(f => f.RunId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
            Dictionary<string, int> resultsByRun = results.Where(x => wanted.Contains(x.RunId)).GroupBy(x => x.RunId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
            List<RunPointRow> points = new List<RunPointRow>();
            foreach (Run run in runs)
            {
                List<RunStage> mine = stagesByRun.TryGetValue(run.Id, out List<RunStage>? s) ? s : new List<RunStage>();
                List<Finding> found = findingsByRun.TryGetValue(run.Id, out List<Finding>? f) ? f : new List<Finding>();
                RunStage? search = Latest(mine, "Search");
                RunStage? fetch = Latest(mine, "Fetch");
                RunPointRow row = new RunPointRow
                {
                    RunId = run.Id,
                    TopicId = run.TopicId,
                    TopicName = names.TryGetValue(run.TopicId, out string? name) ? name : null,
                    CreatedUtc = Utc(run.CreatedUtc),
                    Status = run.Status,
                    DurationMs = run.DurationMs,
                    EstimatedCostUsd = run.EstimatedCostUsd,
                    LlmCallCount = run.LlmCallCount,
                    UnpricedCallCount = run.UnpricedCallCount,
                    NewFindings = run.NewFindingCount,
                    Duplicates = run.DuplicateCount,
                    DuplicateShare = run.NewFindingCount + run.DuplicateCount == 0 ? null : Math.Round(run.DuplicateCount / (double)(run.NewFindingCount + run.DuplicateCount), 4),
                    Examined = search != null ? search.ItemsOut : (resultsByRun.TryGetValue(run.Id, out int stored) ? stored : 0),
                    Fetched = fetch != null ? fetch.ItemsOut : run.SourceCount,
                    Used = found.Where(x => x.SourceItemId != null && usedBy.ContainsKey(Key(run.Id, x.SourceItemId))).Select(x => x.SourceItemId!).Distinct(StringComparer.Ordinal).Count()
                };
                foreach (FindingTierEnum tier in Enum.GetValues<FindingTierEnum>()) row.Tiers[tier.ToString()] = found.Count(x => x.Tier == tier);
                points.Add(row);
            }

            return points;
        }

        private static RunStage? Latest(List<RunStage> stages, string name)
        {
            return stages.Where(s => String.Equals(s.Stage, name, StringComparison.Ordinal)).OrderByDescending(s => s.LastUpdateUtc).FirstOrDefault();
        }

        private static Dictionary<string, int> StatusCounts()
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (RunStatusEnum status in Enum.GetValues<RunStatusEnum>()) counts[status.ToString()] = 0;
            return counts;
        }

        private static double Percentile(List<double> sorted, double p)
        {
            if (sorted.Count == 0) return 0;
            double position = p * (sorted.Count - 1);
            int lower = (int)Math.Floor(position);
            int upper = (int)Math.Ceiling(position);
            return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
        }

        private static string Key(string runId, string? sourceItemId)
        {
            return runId + "|" + (sourceItemId ?? String.Empty);
        }

        private static string Day(DateTime local)
        {
            return local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static DateTime Utc(DateTime value)
        {
            return value.Kind == DateTimeKind.Utc ? value : value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
    }
}
