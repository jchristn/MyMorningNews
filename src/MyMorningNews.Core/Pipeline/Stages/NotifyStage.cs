namespace MyMorningNews.Core.Pipeline.Stages
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Stage 15: queue the run digest email to the topic recipients (skipped for dry runs, when email on run is off, and
    /// for empty runs unless the topic asks for them). Non-critical.
    /// </summary>
    public class NotifyStage : IRunStage
    {
        /// <inheritdoc />
        public string Name
        {
            get { return "Notify"; }
        }

        /// <inheritdoc />
        public bool Critical
        {
            get { return false; }
        }

        /// <inheritdoc />
        public async Task<StageOutcome> ExecuteAsync(RunContext ctx, PipelineServices services, CancellationToken token)
        {
            if (services.Email == null || ctx.Run.IsDryRun) return StageOutcome.Of(0, 0);
            ctx.Run.Summary ??= ctx.Summary;
            int queued = await services.Email.QueueRunDigestAsync(ctx.Run, token).ConfigureAwait(false);
            ctx.Delivered = queued > 0;
            return StageOutcome.Of(1, queued);
        }
    }
}
