namespace MyMorningNews.Core.Pipeline.Stages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Services;

    /// <summary>
    /// Stage 7: embeds each candidate's title and summary. Non-critical: without embeddings, de-duplication falls back to
    /// word similarity and novelty is neutral.
    /// </summary>
    public class EmbedStage : IRunStage
    {
        /// <inheritdoc />
        public string Name
        {
            get { return "Embed"; }
        }

        /// <inheritdoc />
        public bool Critical
        {
            get { return false; }
        }

        /// <inheritdoc />
        public async Task<StageOutcome> ExecuteAsync(RunContext ctx, PipelineServices services, CancellationToken token)
        {
            if (ctx.Candidates.Count == 0) return StageOutcome.Of(0, 0);
            EmbeddingBatch batch = await services.Models.EmbedAsync(ctx.Run.TenantId, ctx.Topic.Id, ctx.Candidates.Select(c => c.EmbeddingText).ToList(), token).ConfigureAwait(false);
            for (int i = 0; i < ctx.Candidates.Count && i < batch.Vectors.Count; i++) ctx.Candidates[i].Vector = batch.Vectors[i];
            ctx.EmbeddingModel = batch.Model;
            ctx.EmbeddingEndpointId = batch.ModelEndpointId;
            return StageOutcome.Of(ctx.Candidates.Count, batch.Vectors.Count);
        }
    }
}
