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
    /// Stage 12: research.predict writes up to five falsifiable predictions with a confidence and a horizon, skipping
    /// near-duplicates of open predictions. Non-critical.
    /// </summary>
    public class PredictStage : IRunStage
    {
        /// <inheritdoc />
        public string Name
        {
            get { return "Predict"; }
        }

        /// <inheritdoc />
        public bool Critical
        {
            get { return false; }
        }

        /// <inheritdoc />
        public async Task<StageOutcome> ExecuteAsync(RunContext ctx, PipelineServices services, CancellationToken token)
        {
            if (ctx.Findings.Count == 0) return StageOutcome.Of(0, 0);
            List<Prediction> all = (await services.Database.Predictions.EnumerateAsync(new PredictionFilter { TenantId = ctx.Run.TenantId, TopicId = ctx.Topic.Id, IsDryRun = false, PageSize = 200 }, token).ConfigureAwait(false)).Objects;
            List<Prediction> open = all.Where(p => p.Status == PredictionStatusEnum.Open || p.Status == PredictionStatusEnum.ProposedResolved).ToList();
            int accurate = all.Count(p => p.Status == PredictionStatusEnum.Accurate);
            int partial = all.Count(p => p.Status == PredictionStatusEnum.PartiallyAccurate);
            int inaccurate = all.Count(p => p.Status == PredictionStatusEnum.Inaccurate);
            string record = accurate + partial + inaccurate == 0 ? "No resolved predictions yet." : accurate + " accurate, " + partial + " partially accurate, " + inaccurate + " inaccurate.";
            List<Prediction> confident = all.Where(p => p.Confidence >= 0.8 && (p.Status == PredictionStatusEnum.Accurate || p.Status == PredictionStatusEnum.PartiallyAccurate || p.Status == PredictionStatusEnum.Inaccurate)).ToList();
            if (confident.Count >= 3)
            {
                double hit = confident.Sum(p => p.Status == PredictionStatusEnum.Accurate ? 1.0 : p.Status == PredictionStatusEnum.PartiallyAccurate ? 0.5 : 0.0) / confident.Count;
                if (hit < 0.7) record += " Overconfident above 0.8: only " + Math.Round(hit * 100) + "% of those came true, so lower confidence unless the evidence is strong.";
            }

            List<double> briers = all.Where(p => p.BrierScore.HasValue).Select(p => p.BrierScore!.Value).ToList();
            if (briers.Count > 0) record += " Mean Brier score " + briers.Average().ToString("0.00", CultureInfo.InvariantCulture) + " (lower is better).";

            StructuredCallResult<PredictionResult> result = await services.CallAsync<PredictionResult>(ctx, "research.predict", new Dictionary<string, object?>
            {
                { "findings", PipelineServices.FormatFindings(ctx.Findings, 25) },
                { "predictions.open", open.Select(p => p.Statement + " (by " + p.HorizonUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ")").ToList() },
                { "track.record", record }
            }, token).ConfigureAwait(false);

            int created = 0;
            foreach (ProposedPrediction pp in result.Value.Predictions)
            {
                if (open.Any(o => PipelineServices.WordSimilarity(o.Statement, pp.Statement) >= 0.75)) continue;
                DateTime horizon = DateTime.TryParse(pp.Horizon, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime h) && h > ctx.NowUtc
                    ? DateTime.SpecifyKind(h, DateTimeKind.Utc) : ctx.NowUtc.AddDays(90);
                Finding? support = pp.Findings.Select(t => PipelineServices.MatchFinding(ctx.Findings, t)).FirstOrDefault(f => f != null);
                Prediction saved = await services.Database.Predictions.CreateAsync(new Prediction
                {
                    TenantId = ctx.Run.TenantId,
                    RunId = ctx.Run.Id,
                    TopicId = ctx.Topic.Id,
                    UserId = ctx.Topic.UserId,
                    DimensionId = support?.DimensionId,
                    Statement = pp.Statement.Trim(),
                    Rationale = pp.Rationale,
                    Confidence = pp.Confidence,
                    HorizonUtc = horizon,
                    IsDryRun = ctx.Run.IsDryRun
                }, token).ConfigureAwait(false);
                open.Add(saved);
                created++;
            }

            return StageOutcome.Of(ctx.Findings.Count, created);
        }
    }
}
