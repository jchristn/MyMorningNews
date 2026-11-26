namespace MyMorningNews.Core.Briefings
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;

    /// <summary>
    /// Structured comparison provenance (plan section 6.2): classifies a briefing's items against the issues its
    /// comparison snapshot selected, using only recorded links (update and duplicate judgments, graded predictions, and
    /// deleted baselines), never earlier prose. Thread safety: stateless; safe for concurrent use.
    /// </summary>
    public class ComparisonService
    {
        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private int _MaxBaselineItems = 1000;

        private static readonly BriefingDeltaKindEnum[] _Order = new[]
        {
            BriefingDeltaKindEnum.Correction, BriefingDeltaKindEnum.ChangedFact, BriefingDeltaKindEnum.NewDevelopment,
            BriefingDeltaKindEnum.Resolved, BriefingDeltaKindEnum.Unchanged, BriefingDeltaKindEnum.Unavailable
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <exception cref="ArgumentNullException">database is null.</exception>
        public ComparisonService(DatabaseDriverBase database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        #endregion

        #region Public-Members

        /// <summary>
        /// Most items read from each baseline issue. Default 1000, minimum 10, maximum 5000.
        /// </summary>
        public int MaxBaselineItems
        {
            get { return _MaxBaselineItems; }
            set { _MaxBaselineItems = Math.Clamp(value, 10, 5000); }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Classify the findings a composition uses against the snapshots' baselines.
        /// </summary>
        /// <param name="tenantId">Tenant of the briefing and its baselines.</param>
        /// <param name="snapshots">Snapshots used; Off snapshots and empty baselines contribute nothing.</param>
        /// <param name="current">Findings the briefing includes, in briefing order.</param>
        /// <param name="topics">Topics covered, for repeated (unchanged) findings.</param>
        /// <param name="runId">Run of a run briefing, or null for an edition (then the period bounds apply).</param>
        /// <param name="periodStartUtc">Period start.</param>
        /// <param name="periodEndUtc">Period end.</param>
        /// <param name="isDryRun">Dry-run composition.</param>
        /// <param name="timeZone">Reader time zone for dates in the summary sentence.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Unsaved changes and the summary sentence.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public async Task<ComparisonResult> ComputeAsync(string tenantId, IReadOnlyCollection<RunHistorySnapshot> snapshots, IReadOnlyCollection<Finding> current,
            IReadOnlyCollection<Topic> topics, string? runId, DateTime periodStartUtc, DateTime periodEndUtc, bool isDryRun, string timeZone, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            ArgumentNullException.ThrowIfNull(snapshots);
            ArgumentNullException.ThrowIfNull(current);
            ArgumentNullException.ThrowIfNull(topics);
            ComparisonResult result = new ComparisonResult();
            List<string> baselineIds = snapshots.Where(s => s.Mode != HistoryModeEnum.Off.ToString()).SelectMany(s => s.BaselineIds).Distinct(StringComparer.Ordinal).ToList();
            if (baselineIds.Count == 0) return result;

            List<BriefingDelta> deltas = new List<BriefingDelta>();
            Dictionary<string, Briefing> baselines = new Dictionary<string, Briefing>(StringComparer.Ordinal);
            Dictionary<string, string> findingBaseline = new Dictionary<string, string>(StringComparer.Ordinal);
            Dictionary<string, string> predictionBaseline = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string id in baselineIds)
            {
                token.ThrowIfCancellationRequested();
                Briefing? b = await _Database.Briefings.ReadAsync(tenantId, id, token).ConfigureAwait(false);
                if (b == null || !b.Active)
                {
                    deltas.Add(new BriefingDelta { TenantId = tenantId, BriefingId = "pending", Kind = BriefingDeltaKindEnum.Unavailable, Title = "An earlier briefing is no longer available", BaselineItemId = id, Summary = "It was deleted after this comparison was chosen." });
                    continue;
                }

                baselines[id] = b;
                List<BriefingItem> items = (await _Database.BriefingItems.EnumerateAsync(new BriefingItemFilter { TenantId = tenantId, BriefingId = id, PageSize = Math.Min(1000, _MaxBaselineItems) }, token).ConfigureAwait(false)).Objects;
                foreach (BriefingItem item in items)
                {
                    if (item.ItemType == BriefingItemTypeEnum.Finding) findingBaseline.TryAdd(item.ItemId, id);
                    else if (item.ItemType == BriefingItemTypeEnum.Prediction) predictionBaseline.TryAdd(item.ItemId, id);
                }
            }

            Dictionary<string, Finding> priorFindings = (await _Database.Findings.ReadManyAsync(tenantId, findingBaseline.Keys, token).ConfigureAwait(false))
                .ToDictionary(f => f.Id, f => f, StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in findingBaseline.Where(p => !priorFindings.TryGetValue(p.Key, out Finding? f) || !f.Active))
            {
                deltas.Add(new BriefingDelta
                {
                    TenantId = tenantId, BriefingId = "pending", Kind = BriefingDeltaKindEnum.Unavailable, Title = "A finding from " + baselines[pair.Value].Title + " is no longer available",
                    BaselineBriefingId = pair.Value, BaselineItemId = pair.Key, Summary = "Its evidence was removed, so it could not be compared."
                });
            }

            foreach (Finding f in current)
            {
                if (f.IsUpdateOfFindingId != null && findingBaseline.TryGetValue(f.IsUpdateOfFindingId, out string? bid) && priorFindings.TryGetValue(f.IsUpdateOfFindingId, out Finding? prior) && prior.Active)
                {
                    deltas.Add(new BriefingDelta
                    {
                        TenantId = tenantId, BriefingId = "pending", Kind = f.IsCorrection ? BriefingDeltaKindEnum.Correction : BriefingDeltaKindEnum.ChangedFact, Title = f.Title,
                        Summary = (f.IsCorrection ? "Corrects: " : "Updates: ") + prior.Title, FindingId = f.Id, BaselineBriefingId = bid, BaselineItemId = prior.Id
                    });
                }
                else
                {
                    deltas.Add(new BriefingDelta { TenantId = tenantId, BriefingId = "pending", Kind = BriefingDeltaKindEnum.NewDevelopment, Title = f.Title, FindingId = f.Id });
                }
            }

            HashSet<string> repeated = new HashSet<string>(StringComparer.Ordinal);
            foreach (Topic topic in topics)
            {
                List<Finding> duplicates = (await _Database.Findings.EnumerateAsync(new FindingFilter
                { TenantId = tenantId, TopicId = topic.Id, RunId = runId, IsDuplicate = true, IsDryRun = isDryRun, PageSize = 500 }, token).ConfigureAwait(false)).Objects;
                foreach (Finding d in duplicates.Where(d => runId != null || (d.CreatedUtc >= periodStartUtc && d.CreatedUtc <= periodEndUtc)))
                {
                    if (d.DuplicateOfFindingId == null || !findingBaseline.TryGetValue(d.DuplicateOfFindingId, out string? bid)) continue;
                    if (!priorFindings.TryGetValue(d.DuplicateOfFindingId, out Finding? prior) || !prior.Active || !repeated.Add(prior.Id)) continue;
                    deltas.Add(new BriefingDelta
                    {
                        TenantId = tenantId, BriefingId = "pending", Kind = BriefingDeltaKindEnum.Unchanged, Title = prior.Title, Summary = "Reported again with no material change.",
                        FindingId = d.Id, BaselineBriefingId = bid, BaselineItemId = prior.Id
                    });
                }
            }

            foreach (KeyValuePair<string, string> pair in predictionBaseline.Take(200))
            {
                Prediction? p = await _Database.Predictions.ReadAsync(tenantId, pair.Key, token).ConfigureAwait(false);
                Briefing baseline = baselines[pair.Value];
                if (p == null || p.Status == PredictionStatusEnum.Open || !p.ResolvedUtc.HasValue) continue;
                if (p.ResolvedUtc.Value <= baseline.CreatedUtc || p.ResolvedUtc.Value > periodEndUtc) continue;
                deltas.Add(new BriefingDelta
                {
                    TenantId = tenantId, BriefingId = "pending", Kind = BriefingDeltaKindEnum.Resolved, Title = p.Statement, Summary = "Graded " + p.Status + " since " + baseline.Title + ".",
                    PredictionId = p.Id, BaselineBriefingId = pair.Value, BaselineItemId = p.Id
                });
            }

            result.Deltas = deltas.OrderBy(d => Array.IndexOf(_Order, d.Kind)).ToList();
            for (int i = 0; i < result.Deltas.Count; i++) result.Deltas[i].SortOrder = i;
            result.Sentence = Sentence(baselineIds.Where(baselines.ContainsKey).Select(id => baselines[id]).ToList(), baselineIds.Count, result.Deltas, timeZone);
            return result;
        }

        /// <summary>
        /// Replace a briefing's saved changes.
        /// </summary>
        /// <param name="briefing">Saved briefing.</param>
        /// <param name="deltas">Changes from <see cref="ComputeAsync"/>.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public async Task SaveAsync(Briefing briefing, IReadOnlyCollection<BriefingDelta> deltas, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(briefing);
            ArgumentNullException.ThrowIfNull(deltas);
            await _Database.BriefingDeltas.DeleteByBriefingAsync(briefing.TenantId, briefing.Id, token).ConfigureAwait(false);
            foreach (BriefingDelta d in deltas)
            {
                d.TenantId = briefing.TenantId;
                d.BriefingId = briefing.Id;
                await _Database.BriefingDeltas.CreateAsync(d, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Describe what a briefing was compared with and what changed. Briefings composed before comparison provenance
        /// existed fall back to the run's snapshot and report no structured changes.
        /// </summary>
        /// <param name="briefing">Briefing (the caller has authorized access).</param>
        /// <param name="timeZone">Reader time zone for dates in the summary. Default UTC.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Comparison.</returns>
        /// <exception cref="ArgumentNullException">briefing is null.</exception>
        public async Task<BriefingComparisonResponse> DescribeAsync(Briefing briefing, string timeZone = "UTC", CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(briefing);
            List<RunHistorySnapshot> snapshots = (await _Database.RunHistorySnapshots.EnumerateAsync(new RunHistorySnapshotFilter { TenantId = briefing.TenantId, BriefingId = briefing.Id, PageSize = 100 }, token).ConfigureAwait(false)).Objects;
            if (snapshots.Count == 0 && !String.IsNullOrEmpty(briefing.RunId))
                snapshots = (await _Database.RunHistorySnapshots.EnumerateAsync(new RunHistorySnapshotFilter { TenantId = briefing.TenantId, RunId = briefing.RunId, PageSize = 1 }, token).ConfigureAwait(false)).Objects;
            BriefingComparisonResponse response = new BriefingComparisonResponse { BriefingId = briefing.Id };
            foreach (BriefingDeltaKindEnum kind in Enum.GetValues<BriefingDeltaKindEnum>()) response.Counts[kind.ToString()] = 0;
            if (snapshots.Count == 0)
            {
                response.Note = "This briefing was not compared with earlier issues.";
                return response;
            }

            RunHistorySnapshot? edition = snapshots.FirstOrDefault(s => s.EditionId != null);
            List<RunHistorySnapshot> used = edition != null ? new List<RunHistorySnapshot> { edition } : snapshots;
            List<string> ids = used.Where(s => s.Mode != HistoryModeEnum.Off.ToString()).SelectMany(s => s.BaselineIds).Distinct(StringComparer.Ordinal).ToList();
            string mode = used.Select(s => s.Mode).FirstOrDefault(m => m != HistoryModeEnum.Off.ToString()) ?? HistoryModeEnum.Off.ToString();
            response.Mode = (edition != null ? "Edition:" : String.Empty) + mode;
            response.Truncated = used.Any(s => s.Truncated);
            response.CutoffUtc = used.Max(s => s.CutoffUtc);
            response.Compared = ids.Count > 0;
            response.Note = response.Compared ? null : used.Select(s => s.Note).FirstOrDefault(n => n != null);
            foreach (string id in ids)
            {
                Briefing? b = await _Database.Briefings.ReadAsync(briefing.TenantId, id, token).ConfigureAwait(false);
                response.Baselines.Add(b == null || !b.Active ? new ComparisonBaseline { Id = id, Available = false } : new ComparisonBaseline { Id = id, Title = b.Title, CreatedUtc = b.CreatedUtc });
            }

            response.Deltas = (await _Database.BriefingDeltas.EnumerateAsync(new BriefingDeltaFilter { TenantId = briefing.TenantId, BriefingId = briefing.Id, PageSize = 1000 }, token).ConfigureAwait(false)).Objects;
            foreach (BriefingDelta d in response.Deltas) response.Counts[d.Kind.ToString()]++;
            response.NoMaterialChanges = response.Compared && Material(response.Deltas) == 0;
            if (response.Compared)
            {
                List<Briefing> available = new List<Briefing>();
                foreach (ComparisonBaseline cb in response.Baselines.Where(x => x.Available))
                    available.Add(new Briefing { Id = cb.Id, TenantId = briefing.TenantId, UserId = briefing.UserId, Title = cb.Title, CreatedUtc = cb.CreatedUtc ?? DateTime.UtcNow });
                response.Summary = Sentence(available, ids.Count, response.Deltas, timeZone);
            }

            return response;
        }

        /// <summary>
        /// The comparison of a system issue for a subscriber: only the published baseline issues and change labels, never
        /// canonical finding or prediction identifiers.
        /// </summary>
        /// <param name="issue">Canonical issue (the caller has checked the subscription).</param>
        /// <param name="timeZone">Reader time zone for dates in the summary. Default UTC.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Projected comparison.</returns>
        /// <exception cref="ArgumentNullException">issue is null.</exception>
        public async Task<BriefingComparisonResponse> DescribeForSubscriberAsync(Briefing issue, string timeZone = "UTC", CancellationToken token = default)
        {
            BriefingComparisonResponse response = await DescribeAsync(issue, timeZone, token).ConfigureAwait(false);
            foreach (BriefingDelta d in response.Deltas)
            {
                d.FindingId = null;
                d.PredictionId = null;
                d.BaselineItemId = null;
                d.TenantId = "catalog";
            }

            return response;
        }

        #endregion

        #region Private-Methods

        private static int Material(IEnumerable<BriefingDelta> deltas)
        {
            return deltas.Count(d => d.Kind == BriefingDeltaKindEnum.NewDevelopment || d.Kind == BriefingDeltaKindEnum.ChangedFact
                || d.Kind == BriefingDeltaKindEnum.Correction || d.Kind == BriefingDeltaKindEnum.Resolved);
        }

        private static string Sentence(List<Briefing> baselines, int selected, List<BriefingDelta> deltas, string timeZone)
        {
            TimeZoneInfo zone = Zone(timeZone);
            string label;
            if (baselines.Count == 0)
            {
                label = selected == 1 ? "an earlier briefing that is no longer available" : selected + " earlier briefings that are no longer available";
            }
            else if (baselines.Count == 1)
            {
                label = baselines[0].Title + " of " + Day(baselines[0].CreatedUtc, zone);
            }
            else
            {
                DateTime first = baselines.Min(b => b.CreatedUtc);
                DateTime last = baselines.Max(b => b.CreatedUtc);
                label = baselines.Count + " earlier briefings (" + Day(first, zone) + " to " + Day(last, zone) + ")";
            }

            if (Material(deltas) == 0)
            {
                int same = deltas.Count(d => d.Kind == BriefingDeltaKindEnum.Unchanged);
                return "Compared with " + label + ": no material changes found" + (same > 0 ? " (" + same + " unchanged)" : String.Empty) + ".";
            }

            List<string> parts = new List<string>();
            Add(parts, deltas, BriefingDeltaKindEnum.NewDevelopment, "new");
            Add(parts, deltas, BriefingDeltaKindEnum.ChangedFact, "changed");
            Add(parts, deltas, BriefingDeltaKindEnum.Correction, "correction", "corrections");
            Add(parts, deltas, BriefingDeltaKindEnum.Resolved, "resolved");
            Add(parts, deltas, BriefingDeltaKindEnum.Unchanged, "unchanged");
            return "Compared with " + label + ": " + String.Join(", ", parts) + ".";
        }

        private static void Add(List<string> parts, List<BriefingDelta> deltas, BriefingDeltaKindEnum kind, string one, string? many = null)
        {
            int n = deltas.Count(d => d.Kind == kind);
            if (n > 0) parts.Add(n + " " + (n == 1 ? one : many ?? one));
        }

        private static string Day(DateTime utc, TimeZoneInfo zone)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone).ToString("MMMM d", CultureInfo.InvariantCulture);
        }

        private static TimeZoneInfo Zone(string timeZone)
        {
            try
            {
                return String.IsNullOrWhiteSpace(timeZone) ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException || ex is InvalidTimeZoneException)
            {
                return TimeZoneInfo.Utc;
            }
        }

        #endregion
    }
}
