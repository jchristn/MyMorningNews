namespace MyMorningNews.Core.Pipeline.Stages
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// Stage 14: compose the run briefing (or the one-time report) from the topic template, with Markdown and HTML files.
    /// Sections whose model call fails are written as plain lists. Non-critical.
    /// </summary>
    public class ComposeBriefingStage : IRunStage
    {
        /// <inheritdoc />
        public string Name
        {
            get { return "ComposeBriefing"; }
        }

        /// <inheritdoc />
        public bool Critical
        {
            get { return false; }
        }

        /// <inheritdoc />
        public async Task<StageOutcome> ExecuteAsync(RunContext ctx, PipelineServices services, CancellationToken token)
        {
            if (services.Briefings == null) return StageOutcome.Of(0, 0, "Briefings are not configured.");
            Briefing b = await services.Briefings.ComposeRunBriefingAsync(ctx.Run, ctx.Topic, token).ConfigureAwait(false);
            if (b.Status != BriefingStatusEnum.Ready) throw new InvalidOperationException(b.ErrorMessage ?? "The briefing could not be composed.");
            ctx.BriefingId = b.Id;
            return StageOutcome.Of(ctx.Findings.Count, b.ItemCount, b.ModelFallbacks > 0 ? b.ModelFallbacks + " sections were written without a model." : null);
        }
    }
}
