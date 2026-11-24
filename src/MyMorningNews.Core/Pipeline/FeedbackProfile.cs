namespace MyMorningNews.Core.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;

    /// <summary>
    /// What the reader has rated on a topic, loaded once per run for ranking (plan section 17): current relevance ratings
    /// of findings (with titles for the no-embedding fallback) and per-domain reputation from finding, source, and domain
    /// feedback. Thread safety: read-only after loading.
    /// </summary>
    public class FeedbackProfile
    {
        /// <summary>
        /// Rating value (-1 to 1) by finding ID.
        /// </summary>
        public Dictionary<string, double> RatedFindings { get; } = new Dictionary<string, double>(StringComparer.Ordinal);

        /// <summary>
        /// Titles of rated findings by ID.
        /// </summary>
        public Dictionary<string, string> Titles { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Reputation points (0 to 100) per domain, one entry per rating.
        /// </summary>
        public Dictionary<string, List<double>> DomainRatings { get; } = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Load the profile for a run's topic owner.
        /// </summary>
        /// <param name="services">Pipeline services.</param>
        /// <param name="ctx">Run context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Profile.</returns>
        public static async Task<FeedbackProfile> LoadAsync(PipelineServices services, RunContext ctx, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(ctx);
            FeedbackProfile profile = new FeedbackProfile();
            List<Models.Feedback> rated = (await services.Database.Feedback.EnumerateAsync(new FeedbackFilter { TenantId = ctx.Run.TenantId, UserId = ctx.Topic.UserId, TopicId = ctx.Topic.Id, IsCurrent = true, PageSize = 1000 }, token).ConfigureAwait(false)).Objects
                .Where(f => f.Relevance.HasValue).ToList();
            foreach (Models.Feedback f in rated.Where(f => f.TargetType == FeedbackTargetTypeEnum.Finding))
            {
                Finding? finding = await services.Database.Findings.ReadAsync(ctx.Run.TenantId, f.TargetId, token).ConfigureAwait(false);
                if (finding == null) continue;
                profile.RatedFindings[finding.Id] = Ranker.RatingValue(f.Relevance!.Value);
                profile.Titles[finding.Id] = finding.Title;
                SourceItem? s = finding.SourceItemId == null ? null : await services.Database.SourceItems.ReadAsync(ctx.Run.TenantId, finding.SourceItemId, token).ConfigureAwait(false);
                if (!String.IsNullOrWhiteSpace(s?.Domain)) profile.AddDomain(s!.Domain!, Ranker.ReputationPoints(f.Relevance.Value));
            }

            List<Models.Feedback> sourceRatings = (await services.Database.Feedback.EnumerateAsync(new FeedbackFilter { TenantId = ctx.Run.TenantId, UserId = ctx.Topic.UserId, IsCurrent = true, PageSize = 1000 }, token).ConfigureAwait(false)).Objects
                .Where(f => f.Relevance.HasValue && (f.TargetType == FeedbackTargetTypeEnum.SourceItem || f.TargetType == FeedbackTargetTypeEnum.Domain)).ToList();
            foreach (Models.Feedback f in sourceRatings.Where(f => f.TargetType == FeedbackTargetTypeEnum.SourceItem))
            {
                SourceItem? s = await services.Database.SourceItems.ReadAsync(ctx.Run.TenantId, f.TargetId, token).ConfigureAwait(false);
                if (!String.IsNullOrWhiteSpace(s?.Domain)) profile.AddDomain(s!.Domain!, Ranker.ReputationPoints(f.Relevance!.Value));
            }

            foreach (Models.Feedback f in sourceRatings.Where(f => f.TargetType == FeedbackTargetTypeEnum.Domain)) profile.AddDomain(f.TargetId, Ranker.ReputationPoints(f.Relevance!.Value));
            return profile;
        }

        /// <summary>
        /// Feedback score for a candidate: nearest rated findings by embedding when available, else by title words.
        /// </summary>
        /// <param name="services">Pipeline services.</param>
        /// <param name="ctx">Run context.</param>
        /// <param name="candidate">Candidate.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>0 to 100; 50 when nothing similar was rated.</returns>
        public async Task<double> ScoreAsync(PipelineServices services, RunContext ctx, Candidate candidate, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(candidate);
            if (RatedFindings.Count == 0) return 50;
            List<KeyValuePair<double, double>> neighbors = new List<KeyValuePair<double, double>>();
            if (candidate.Vector != null && ctx.EmbeddingModel != null)
            {
                List<EmbeddingMatch> near = await services.Database.Embeddings.NearestAsync(ctx.Run.TenantId, ctx.Topic.Id, "Finding", ctx.EmbeddingModel, candidate.Vector, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), null,
                    services.Settings.Learning.FeedbackNeighbors * 5, token).ConfigureAwait(false);
                foreach (EmbeddingMatch m in near.Where(m => RatedFindings.ContainsKey(m.OwnerId) && m.Similarity >= 0.3).Take(services.Settings.Learning.FeedbackNeighbors))
                    neighbors.Add(new KeyValuePair<double, double>(m.Similarity, RatedFindings[m.OwnerId]));
            }
            else
            {
                foreach (KeyValuePair<string, string> t in Titles)
                {
                    double sim = PipelineServices.WordSimilarity(candidate.Extracted.Title, t.Value);
                    if (sim >= 0.2) neighbors.Add(new KeyValuePair<double, double>(sim, RatedFindings[t.Key]));
                }
            }

            double weight = neighbors.Sum(n => n.Key);
            if (weight <= 0) return 50;
            double affinity = neighbors.Sum(n => n.Key * n.Value) / weight;
            return Ranker.Feedback(affinity, Math.Min(1, weight / 2.0));
        }

        /// <summary>
        /// Source score for a URL from the domain's ratings plus preferred and blocked domains.
        /// </summary>
        /// <param name="domain">Domain of the source, or null.</param>
        /// <param name="preferred">Preferred.</param>
        /// <param name="blocked">Blocked.</param>
        /// <returns>0 to 100.</returns>
        public double SourceScore(string? domain, bool preferred, bool blocked)
        {
            List<double>? ratings = null;
            if (!String.IsNullOrWhiteSpace(domain))
            {
                string d = domain.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? domain.Substring(4) : domain;
                DomainRatings.TryGetValue(d, out ratings);
            }

            return Ranker.Source(ratings == null || ratings.Count == 0 ? null : ratings.Average(), ratings?.Count ?? 0, preferred, blocked);
        }

        private void AddDomain(string domain, double points)
        {
            string d = domain.Trim().ToLowerInvariant();
            if (d.StartsWith("www.", StringComparison.Ordinal)) d = d.Substring(4);
            if (!DomainRatings.TryGetValue(d, out List<double>? list))
            {
                list = new List<double>();
                DomainRatings[d] = list;
            }

            list.Add(points);
        }
    }
}
