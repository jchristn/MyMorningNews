namespace MyMorningNews.Core.Pipeline.Stages
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Stage 15: notify about the run through <see cref="Services.NotificationService"/>: the run digest email to the
    /// topic recipients (skipped when email on run is off, and for empty runs unless the topic asks for them), and a push
    /// and in-app notification to the owner when they turned those on and the run found something new. Skipped for dry
    /// runs. Non-critical.
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
            if (services.Notifications == null || ctx.Run.IsDryRun) return StageOutcome.Of(0, 0);
            ctx.Run.Summary ??= ctx.Summary;
            Services.NotificationResult sent = await services.Notifications.NotifyAsync(ctx.Run.TenantId, ctx.Run.UserId, Enums.NotificationKindEnum.RunResults,
                new Services.NotificationContext { Run = ctx.Run, TopicName = ctx.Topic.Name }, token).ConfigureAwait(false);
            int queued = sent.EmailsQueued;
            ctx.Delivered = queued > 0;
            return StageOutcome.Of(1, queued);
        }
    }
}
