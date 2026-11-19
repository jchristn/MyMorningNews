namespace MyMorningNews.Core.Analytics
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Settings;

    /// <summary>
    /// Relevance, accuracy, feedback, suggestion, reason, and evidence quality analytics (plan sections 19 and 20), and
    /// ranking weight recommendations. Computed from the records at request time, scoped to a tenant, optionally a user
    /// and a topic, and a period. Thread safety: safe for concurrent use.
    /// </summary>
    public class AnalyticsService
    {
        private readonly DatabaseDriverBase _Database;
        private readonly ServerSettings _Settings;
        private static readonly Regex _Failed = new Regex(@"(\d+) of (\d+) quotes could not be verified", RegexOptions.Compiled);
        private static readonly Regex _All = new Regex(@"All (\d+) quotes verified", RegexOptions.Compiled);

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="settings">Server settings.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public AnalyticsService(DatabaseDriverBase database, ServerSettings settings)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Relevance precision and coverage.
        /// </summary>
        /// <param name="scope">Scope.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Report.</returns>
        public async Task<RelevanceReport> RelevanceAsync(AnalyticsScope scope, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(scope);
            List<Finding> shown = (await _Database.Findings.EnumerateAsync(new FindingFilter
            {
                TenantId = scope.TenantId, UserId = scope.UserId, TopicId = scope.TopicId, IsDuplicate = false, IsDryRun = false, ExcludeTier = FindingTierEnum.BelowThreshold,
                FromUtc = scope.FromUtc, ToUtc = scope.ToUtc, PageSize = 5000
            }, token).ConfigureAwait(false)).Items;
            Dictionary<string, Models.Feedback> ratings = (await CurrentAsync(scope, FeedbackTargetTypeEnum.Finding, token).ConfigureAwait(false))
                .Where(f => f.Relevance.HasValue).GroupBy(f => f.TargetId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedUtc).First(), StringComparer.Ordinal);
            List<Finding> rated = shown.Where(f => ratings.ContainsKey(f.Id)).ToList();
            Func<Finding, bool> positive = f => ratings[f.Id].Relevance != RelevanceRatingEnum.NotRelevant;
            RelevanceReport r = new RelevanceReport { Shown = shown.Count, Rated = rated.Count, Precision = Rate(rated.Count(positive), rated.Count), Coverage = Rate(rated.Count, shown.Count) };
            Dictionary<string, string> topicNames = await TopicNamesAsync(scope.TenantId, rated.Select(f => f.TopicId), token).ConfigureAwait(false);
            r.ByTopic = Rows(rated.GroupBy(f => f.TopicId), positive, k => topicNames.TryGetValue(k, out string? n) ? n : null);
            r.ByTier = Rows(rated.GroupBy(f => f.Tier.ToString()), positive, null);
            Dictionary<string, string> dimNames = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string id in rated.Where(f => f.DimensionId != null).Select(f => f.DimensionId!).Distinct())
            {
                TopicDimension? d = await _Database.TopicDimensions.ReadAsync(scope.TenantId, id, token).ConfigureAwait(false);
                if (d != null) dimNames[id] = d.Name;
            }

            r.ByDimension = Rows(rated.GroupBy(f => f.DimensionId ?? "none"), positive, k => dimNames.TryGetValue(k, out string? n) ? n : (k == "none" ? "No dimension" : null));
            Dictionary<string, string> domains = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Finding f in rated.Where(f => f.SourceItemId != null))
            {
                SourceItem? s = await _Database.SourceItems.ReadAsync(scope.TenantId, f.SourceItemId!, token).ConfigureAwait(false);
                domains[f.Id] = s?.Domain ?? "unknown";
            }

            r.ByDomain = Rows(rated.GroupBy(f => domains.TryGetValue(f.Id, out string? d) ? d : "unknown"), positive, null).OrderByDescending(x => x.Count).Take(20).ToList();
            r.ByDay = Rows(rated.GroupBy(f => Day(f.CreatedUtc)), positive, null).OrderBy(x => x.Key, StringComparer.Ordinal).ToList();
            return r;
        }

        /// <summary>
        /// Prediction accuracy, Brier score, calibration, latency, and agent agreement.
        /// </summary>
        /// <param name="scope">Scope (the period applies to when predictions were made).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Report.</returns>
        public async Task<AccuracyReport> AccuracyAsync(AnalyticsScope scope, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(scope);
            List<Prediction> preds = (await _Database.Predictions.EnumerateAsync(new PredictionFilter
            {
                TenantId = scope.TenantId, UserId = scope.UserId, TopicId = scope.TopicId, IsDryRun = false, FromUtc = scope.FromUtc, ToUtc = scope.ToUtc, PageSize = 5000
            }, token).ConfigureAwait(false)).Items;
            DateTime now = DateTime.UtcNow;
            List<Prediction> resolved = preds.Where(IsGraded).ToList();
            AccuracyReport r = new AccuracyReport
            {
                Total = preds.Count,
                Open = preds.Count(p => p.Status == PredictionStatusEnum.Open),
                DueForReview = preds.Count(p => p.Status == PredictionStatusEnum.ProposedResolved || (p.Status == PredictionStatusEnum.Open && p.HorizonUtc <= now)),
                Resolved = resolved.Count,
                Unresolvable = preds.Count(p => p.Status == PredictionStatusEnum.Unresolvable),
                AccuracyRate = resolved.Count == 0 ? null : Math.Round(resolved.Sum(Outcome) / resolved.Count, 4),
                MeanBrier = resolved.Any(p => p.BrierScore.HasValue) ? Math.Round(resolved.Where(p => p.BrierScore.HasValue).Average(p => p.BrierScore!.Value), 4) : null,
                MeanResolutionLatencyDays = resolved.Any(p => p.ResolvedUtc.HasValue) ? Math.Round(resolved.Where(p => p.ResolvedUtc.HasValue).Average(p => (p.ResolvedUtc!.Value - p.HorizonUtc).TotalDays), 2) : null
            };
            for (int i = 0; i < 5; i++)
            {
                double lo = i * 0.2;
                double hi = i == 4 ? 1.0 : lo + 0.2;
                List<Prediction> bucket = resolved.Where(p => p.Confidence >= lo && (i == 4 ? p.Confidence <= hi : p.Confidence < hi)).ToList();
                r.Calibration.Add(new CalibrationBucket
                {
                    Lower = Math.Round(lo, 2), Upper = Math.Round(hi, 2), Count = bucket.Count,
                    MeanConfidence = bucket.Count == 0 ? null : Math.Round(bucket.Average(p => p.Confidence), 4),
                    Observed = bucket.Count == 0 ? null : Math.Round(bucket.Sum(Outcome) / bucket.Count, 4)
                });
            }

            int accepted = 0;
            int decided = 0;
            foreach (Prediction p in preds)
            {
                foreach (PredictionReview rv in (await _Database.PredictionReviews.EnumerateAsync(new PredictionReviewFilter { TenantId = scope.TenantId, PredictionId = p.Id, PageSize = 50 }, token).ConfigureAwait(false)).Items.Where(x => x.AcceptedByUser.HasValue))
                {
                    decided++;
                    if (rv.AcceptedByUser == true) accepted++;
                }
            }

            r.ReviewsDecided = decided;
            r.AgentAgreement = Rate(accepted, decided);
            Dictionary<string, string> names = await TopicNamesAsync(scope.TenantId, resolved.Select(p => p.TopicId), token).ConfigureAwait(false);
            r.ByTopic = resolved.GroupBy(p => p.TopicId).Select(g => new BreakdownRow
            {
                Key = g.Key, Label = names.TryGetValue(g.Key, out string? n) ? n : null, Count = g.Count(), Positive = g.Count(p => p.Status == PredictionStatusEnum.Accurate),
                Rate = Math.Round(g.Sum(Outcome) / g.Count(), 4), Value = g.Any(p => p.BrierScore.HasValue) ? Math.Round(g.Where(p => p.BrierScore.HasValue).Average(p => p.BrierScore!.Value), 4) : null
            }).OrderByDescending(x => x.Count).ToList();
            return r;
        }

        /// <summary>
        /// Feedback volume by channel, item type, and day, with email link opens and submissions.
        /// </summary>
        /// <param name="scope">Scope.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Report.</returns>
        public async Task<FeedbackReport> FeedbackAsync(AnalyticsScope scope, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(scope);
            List<Models.Feedback> all = await CurrentAsync(scope, null, token).ConfigureAwait(false);
            Func<Models.Feedback, bool> reason = f => !String.IsNullOrWhiteSpace(f.Reason);
            FeedbackReport r = new FeedbackReport { Total = all.Count, WithReason = all.Count(reason) };
            r.ByChannel = Rows(all.GroupBy(f => f.Channel.ToString()), reason, null);
            r.ByTargetType = Rows(all.GroupBy(f => f.TargetType.ToString()), reason, null);
            r.ByDay = Rows(all.GroupBy(f => Day(f.CreatedUtc)), reason, null).OrderBy(x => x.Key, StringComparer.Ordinal).ToList();
            List<EmailActionToken> tokens = (await _Database.EmailActionTokens.EnumerateAsync(new EmailActionTokenFilter
            {
                TenantId = scope.TenantId, UserId = scope.UserId, Kind = ActionTokenKindEnum.Feedback, FromUtc = scope.FromUtc, ToUtc = scope.ToUtc, PageSize = 10000
            }, token).ConfigureAwait(false)).Items;
            r.EmailLinkOpens = tokens.Sum(t => t.OpenCount);
            r.EmailLinkSubmits = tokens.Sum(t => t.SubmitCount);
            return r;
        }

        /// <summary>
        /// Suggestion outcomes and time to decision.
        /// </summary>
        /// <param name="scope">Scope.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Report.</returns>
        public async Task<SuggestionReport> SuggestionsAsync(AnalyticsScope scope, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(scope);
            List<Suggestion> all = (await _Database.Suggestions.EnumerateAsync(new SuggestionFilter
            {
                TenantId = scope.TenantId, UserId = scope.UserId, TopicId = scope.TopicId, IsDryRun = false, FromUtc = scope.FromUtc, ToUtc = scope.ToUtc, PageSize = 5000
            }, token).ConfigureAwait(false)).Items;
            List<Suggestion> decided = all.Where(s => s.Status != SuggestionStatusEnum.New && s.StatusChangedUtc.HasValue).ToList();
            Func<Suggestion, bool> good = s => s.Status == SuggestionStatusEnum.Done || s.Status == SuggestionStatusEnum.Accepted;
            return new SuggestionReport
            {
                Total = all.Count,
                New = all.Count(s => s.Status == SuggestionStatusEnum.New),
                Accepted = all.Count(s => s.Status == SuggestionStatusEnum.Accepted),
                Done = all.Count(s => s.Status == SuggestionStatusEnum.Done),
                Dismissed = all.Count(s => s.Status == SuggestionStatusEnum.Dismissed),
                Snoozed = all.Count(s => s.Status == SuggestionStatusEnum.Snoozed),
                MeanDaysToDecision = decided.Count == 0 ? null : Math.Round(decided.Average(s => (s.StatusChangedUtc!.Value - s.CreatedUtc).TotalDays), 2),
                ByPriority = all.GroupBy(s => s.Priority.ToString()).Select(g => new BreakdownRow { Key = g.Key, Count = g.Count(), Positive = g.Count(good), Rate = Rate(g.Count(good), g.Count()) }).ToList()
            };
        }

        /// <summary>
        /// Reason tags by count and by week.
        /// </summary>
        /// <param name="scope">Scope.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Report.</returns>
        public async Task<ReasonReport> ReasonsAsync(AnalyticsScope scope, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(scope);
            HashSet<string> mine = scope.UserId == null ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>((await CurrentAsync(scope, null, token, true).ConfigureAwait(false)).Select(f => f.Id), StringComparer.Ordinal);
            List<FeedbackReasonTag> tags = (await _Database.FeedbackReasonTags.EnumerateAsync(new FeedbackReasonTagFilter
            {
                TenantId = scope.TenantId, TopicId = scope.TopicId, FromUtc = scope.FromUtc, ToUtc = scope.ToUtc, PageSize = 10000
            }, token).ConfigureAwait(false)).Items.Where(t => scope.UserId == null || mine.Contains(t.FeedbackId)).ToList();
            DateTime recent = DateTime.UtcNow.AddDays(-30);
            return new ReasonReport
            {
                Tags = tags.GroupBy(t => t.Tag).Select(g => new BreakdownRow { Key = g.Key, Count = g.Count(), Value = g.Count(t => t.CreatedUtc >= recent) }).OrderByDescending(x => x.Count).Take(50).ToList(),
                ByWeek = tags.GroupBy(t => Week(t.CreatedUtc) + "|" + t.Tag).Select(g => new BreakdownRow { Key = g.Key.Split('|')[0], Label = g.Key.Split('|')[1], Count = g.Count() })
                    .OrderBy(x => x.Key, StringComparer.Ordinal).ToList()
            };
        }

        /// <summary>
        /// Share of extracted quotes that failed verification, by extraction model and day.
        /// </summary>
        /// <param name="scope">Scope.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Report.</returns>
        public async Task<QualityReport> QualityAsync(AnalyticsScope scope, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(scope);
            List<Run> runs = (await _Database.Runs.EnumerateAsync(new RunFilter { TenantId = scope.TenantId, UserId = scope.UserId, TopicId = scope.TopicId, FromUtc = scope.FromUtc, ToUtc = scope.ToUtc, PageSize = 2000 }, token).ConfigureAwait(false)).Items;
            List<QuoteCount> counts = new List<QuoteCount>();
            foreach (Run run in runs)
            {
                RunStage? extract = (await _Database.RunStages.EnumerateAsync(new RunStageFilter { TenantId = scope.TenantId, RunId = run.Id, PageSize = 50 }, token).ConfigureAwait(false)).Items.FirstOrDefault(s => s.Stage == "Extract");
                if (extract?.ErrorMessage == null) continue;
                int total;
                int failed;
                Match m = _Failed.Match(extract.ErrorMessage);
                Match a = _All.Match(extract.ErrorMessage);
                if (m.Success) { failed = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture); total = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture); }
                else if (a.Success) { failed = 0; total = int.Parse(a.Groups[1].Value, CultureInfo.InvariantCulture); }
                else continue;
                LlmCall? call = (await _Database.LlmCalls.EnumerateAsync(new LlmCallFilter { TenantId = scope.TenantId, RunId = run.Id, PromptKey = "research.extract", PageSize = 1 }, token).ConfigureAwait(false)).Items.FirstOrDefault();
                counts.Add(new QuoteCount { Model = call?.Model ?? "unknown", Day = Day(run.CreatedUtc), Total = total, Verified = total - failed });
            }

            int quotes = counts.Sum(c => c.Total);
            int verified = counts.Sum(c => c.Verified);
            return new QualityReport
            {
                Quotes = quotes,
                Verified = verified,
                FailureRate = quotes == 0 ? null : Math.Round((quotes - verified) / (double)quotes, 4),
                ByModel = counts.GroupBy(c => c.Model).Select(g => new BreakdownRow { Key = g.Key, Count = g.Sum(x => x.Total), Positive = g.Sum(x => x.Verified), Rate = Rate(g.Sum(x => x.Verified), g.Sum(x => x.Total)) }).ToList(),
                ByDay = counts.GroupBy(c => c.Day).Select(g => new BreakdownRow { Key = g.Key, Count = g.Sum(x => x.Total), Positive = g.Sum(x => x.Verified), Rate = Rate(g.Sum(x => x.Verified), g.Sum(x => x.Total)) })
                    .OrderBy(x => x.Key, StringComparer.Ordinal).ToList()
            };
        }

        /// <summary>
        /// Recommended ranking weights for a topic: components that separate liked from disliked findings get more weight.
        /// Needs 20 rated findings; never changes the topic by itself.
        /// </summary>
        /// <param name="topic">Topic.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Recommendation.</returns>
        public async Task<RankingRecommendation> RankingAsync(Topic topic, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(topic);
            PipelineSettings p = _Settings.Pipeline;
            RankingRecommendation r = new RankingRecommendation();
            r.Current["llm"] = topic.WeightLlm ?? p.WeightLlm;
            r.Current["feedback"] = topic.WeightFeedback ?? p.WeightFeedback;
            r.Current["novelty"] = topic.WeightNovelty ?? p.WeightNovelty;
            r.Current["source"] = topic.WeightSource ?? p.WeightSource;
            r.Current["recency"] = topic.WeightRecency ?? p.WeightRecency;
            AnalyticsScope scope = new AnalyticsScope { TenantId = topic.TenantId, UserId = topic.UserId, TopicId = topic.Id, FromUtc = DateTime.UtcNow.AddYears(-5), ToUtc = DateTime.UtcNow };
            List<Models.Feedback> ratings = (await CurrentAsync(scope, FeedbackTargetTypeEnum.Finding, token).ConfigureAwait(false)).Where(f => f.Relevance.HasValue).ToList();
            List<Finding> pos = new List<Finding>();
            List<Finding> neg = new List<Finding>();
            foreach (Models.Feedback f in ratings)
            {
                Finding? x = await _Database.Findings.ReadAsync(topic.TenantId, f.TargetId, token).ConfigureAwait(false);
                if (x == null) continue;
                if (f.Relevance == RelevanceRatingEnum.NotRelevant) neg.Add(x); else pos.Add(x);
            }

            r.RatedFindings = pos.Count + neg.Count;
            if (r.RatedFindings < 20 || pos.Count == 0 || neg.Count == 0)
            {
                r.Note = "Rate at least 20 findings, including some you liked and some you did not, to get a recommendation.";
                foreach (KeyValuePair<string, double> kv in r.Current) r.Recommended[kv.Key] = kv.Value;
                return r;
            }

            r.Enough = true;
            Dictionary<string, Func<Finding, double>> parts = new Dictionary<string, Func<Finding, double>>(StringComparer.Ordinal)
            {
                { "llm", f => f.LlmScore }, { "feedback", f => f.FeedbackScore }, { "novelty", f => f.NoveltyScore }, { "source", f => f.SourceScore }, { "recency", f => f.RecencyScore }
            };
            Dictionary<string, double> raw = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, Func<Finding, double>> part in parts)
            {
                double sep = pos.Average(part.Value) - neg.Average(part.Value);
                r.Separation[part.Key] = Math.Round(sep, 2);
                raw[part.Key] = 0.5 * r.Current[part.Key] + 0.5 * Math.Max(0.02, sep / 100.0 + 0.05);
            }

            double sum = raw.Values.Sum();
            foreach (KeyValuePair<string, double> kv in raw) r.Recommended[kv.Key] = Math.Round(kv.Value / sum, 3);
            string best = r.Separation.OrderByDescending(kv => kv.Value).First().Key;
            r.Note = "Your ratings line up best with the " + best + " score. The recommendation blends your current weights with what separated liked from disliked findings.";
            return r;
        }

        private async Task<List<Models.Feedback>> CurrentAsync(AnalyticsScope scope, FeedbackTargetTypeEnum? type, CancellationToken token, bool ignorePeriod = false)
        {
            return (await _Database.Feedback.EnumerateAsync(new FeedbackFilter
            {
                TenantId = scope.TenantId, UserId = scope.UserId, TopicId = scope.TopicId, TargetType = type, IsCurrent = true,
                FromUtc = ignorePeriod ? null : scope.FromUtc, ToUtc = ignorePeriod ? null : scope.ToUtc, PageSize = 10000
            }, token).ConfigureAwait(false)).Items;
        }

        private async Task<Dictionary<string, string>> TopicNamesAsync(string tenantId, IEnumerable<string> ids, CancellationToken token)
        {
            Dictionary<string, string> names = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string id in ids.Distinct())
            {
                Topic? t = await _Database.Topics.ReadAsync(tenantId, id, token).ConfigureAwait(false);
                if (t != null) names[id] = t.Name;
            }

            return names;
        }

        private static List<BreakdownRow> Rows<T>(IEnumerable<IGrouping<string, T>> groups, Func<T, bool> positive, Func<string, string?>? label)
        {
            return groups.Select(g => new BreakdownRow { Key = g.Key, Label = label?.Invoke(g.Key), Count = g.Count(), Positive = g.Count(positive), Rate = Rate(g.Count(positive), g.Count()) })
                .OrderByDescending(r => r.Count).ToList();
        }

        private static bool IsGraded(Prediction p)
        {
            return p.Status == PredictionStatusEnum.Accurate || p.Status == PredictionStatusEnum.PartiallyAccurate || p.Status == PredictionStatusEnum.Inaccurate;
        }

        private static double Outcome(Prediction p)
        {
            return p.Status == PredictionStatusEnum.Accurate ? 1.0 : p.Status == PredictionStatusEnum.PartiallyAccurate ? 0.5 : 0.0;
        }

        private static double? Rate(int positive, int count)
        {
            return count == 0 ? null : Math.Round(positive / (double)count, 4);
        }

        private static string Day(DateTime utc)
        {
            return utc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static string Week(DateTime utc)
        {
            int offset = ((int)utc.DayOfWeek + 6) % 7;
            return utc.Date.AddDays(-offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }
}
