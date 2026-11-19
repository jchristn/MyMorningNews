namespace MyMorningNews.Core.Pipeline.Stages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Models.Results;
    using MyMorningNews.Core.Services;

    /// <summary>
    /// Stage 10: research.synthesize writes the run summary (what changed, themes, how it relates to earlier runs).
    /// Non-critical: on failure the summary lists the top findings.
    /// </summary>
    public class SynthesizeStage : IRunStage
    {
        /// <inheritdoc />
        public string Name
        {
            get { return "Synthesize"; }
        }

        /// <inheritdoc />
        public bool Critical
        {
            get { return false; }
        }

        /// <inheritdoc />
        public async Task<StageOutcome> ExecuteAsync(RunContext ctx, PipelineServices services, CancellationToken token)
        {
            if (ctx.Findings.Count == 0)
            {
                ctx.Summary = "Nothing new this run: " + ctx.Run.SourceCount + " sources read, " + ctx.Run.DuplicateCount + " repeats of earlier findings.";
                return StageOutcome.Of(0, 0);
            }

            ctx.Summary = "Found " + ctx.Findings.Count + " new findings. Top: " + String.Join("; ", ctx.Findings.Take(3).Select(f => f.Title)) + ".";
            StructuredCallResult<SynthesisResult> result = await services.CallAsync<SynthesisResult>(ctx, "research.synthesize", new Dictionary<string, object?>
            {
                { "findings", PipelineServices.FormatFindings(ctx.Findings, 25) }
            }, token).ConfigureAwait(false);
            StringBuilder sb = new StringBuilder(result.Value.Summary.Trim());
            if (result.Value.Themes.Count > 0)
            {
                sb.Append("\n\nThemes:");
                foreach (SynthesisTheme t in result.Value.Themes) sb.Append("\n- ").Append(t.Name).Append(String.IsNullOrWhiteSpace(t.Description) ? String.Empty : ": " + t.Description);
            }

            ctx.Summary = sb.ToString();
            return StageOutcome.Of(ctx.Findings.Count, result.Value.Themes.Count);
        }
    }
}
