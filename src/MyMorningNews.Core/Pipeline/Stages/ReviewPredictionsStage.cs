namespace MyMorningNews.Core.Pipeline.Stages
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Models.Results;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Services;

    /// <summary>
    /// Stage 11: research.review-predictions checks open predictions against this run's evidence and proposes a status.
    /// A proposal is never final until the reader accepts it. Skipped on dry runs and when nothing is open. Non-critical.
    /// </summary>
    public class ReviewPredictionsStage : IRunStage
    {
        /// <inheritdoc />
        public string Name
        {
            get { return "ReviewPredictions"; }
        }

        /// <inheritdoc />
        public bool Critical
        {
            get { return false; }
        }

        /// <inheritdoc />
        public async Task<StageOutcome> ExecuteAsync(RunContext ctx, PipelineServices services, CancellationToken token)
        {
            if (ctx.Run.IsDryRun || ctx.Findings.Count == 0) return StageOutcome.Of(0, 0);
            List<Prediction> open = (await services.Database.Predictions.EnumerateAsync(new PredictionFilter { TenantId = ctx.Run.TenantId, TopicId = ctx.Topic.Id, Status = PredictionStatusEnum.Open, IsDryRun = false, PageSize = 50 }, token).ConfigureAwait(false)).Objects;
            if (open.Count == 0) return StageOutcome.Of(0, 0);
            StructuredCallResult<PredictionReviewResult> result = await services.CallAsync<PredictionReviewResult>(ctx, "research.review-predictions", new Dictionary<string, object?>
            {
                { "predictions.due", open.Select(p => "[" + p.Id + "] " + p.Statement + " (confidence " + p.Confidence.ToString("0.00", CultureInfo.InvariantCulture) + ", by " + p.HorizonUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ")").ToList() },
                { "findings", PipelineServices.FormatFindings(ctx.Findings, 25) }
            }, token).ConfigureAwait(false);

            int created = 0;
            foreach (ProposedReview r in result.Value.Reviews)
            {
                Prediction? p = open.FirstOrDefault(x => String.Equals(x.Id, r.PredictionId.Trim().Trim('[', ']'), StringComparison.Ordinal));
                if (p == null || r.Status == PredictionStatusEnum.Open || r.Status == PredictionStatusEnum.ProposedResolved || r.Status == PredictionStatusEnum.Withdrawn) continue;
                await services.Database.PredictionReviews.CreateAsync(new PredictionReview
                {
                    TenantId = ctx.Run.TenantId,
                    PredictionId = p.Id,
                    RunId = ctx.Run.Id,
                    ProposedStatus = r.Status,
                    Rationale = r.Rationale,
                    Confidence = r.Confidence,
                    FindingIds = r.Findings.Select(t => PipelineServices.MatchFinding(ctx.Findings, t)?.Id).Where(id => id != null).Select(id => id!).Distinct().ToList()
                }, token).ConfigureAwait(false);
                p.Status = PredictionStatusEnum.ProposedResolved;
                await services.Database.Predictions.UpdateAsync(p, token).ConfigureAwait(false);
                created++;
            }

            return StageOutcome.Of(open.Count, created);
        }
    }
}
