namespace MyMorningNews.McpServer.Resources
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.McpServer.Tools;
    using MyMorningNews.Sdk;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;

    /// <summary>
    /// The Markdown resources: the latest briefing, a briefing by ID, a topic's brief, and a run summary.
    /// </summary>
    public static class ResourceCatalog
    {
        /// <summary>
        /// Build the resources.
        /// </summary>
        /// <returns>Resources.</returns>
        public static List<McpResourceSpec> Build()
        {
            return new List<McpResourceSpec>
            {
                new McpResourceSpec("mmn://briefings/latest", "Latest briefing", "Your newest ready briefing as Markdown.", false, async (c, v, t) =>
                {
                    BriefingDetailResponse? d = await BriefingTools.LatestAsync(c, t).ConfigureAwait(false);
                    return d == null ? "# No briefing yet\n\nNo briefing is ready yet. Run a topic or wait for the next edition.\n" : BriefingMarkdown(d);
                }),
                new McpResourceSpec("mmn://briefings/{id}", "Briefing", "A briefing as Markdown.", true, async (c, v, t) =>
                    BriefingMarkdown(await c.Briefings.GetAsync(Variable(v, "id"), t).ConfigureAwait(false))),
                new McpResourceSpec("mmn://topics/{id}/brief", "Topic brief", "A topic's brief, dimensions, and constraints as Markdown.", true, async (c, v, t) =>
                    TopicMarkdown(await c.Topics.GetAsync(Variable(v, "id"), t).ConfigureAwait(false))),
                new McpResourceSpec("mmn://runs/{id}/summary", "Run summary", "A run's status, estimated cost, stages with their cost, summary, and top findings as Markdown.", true, async (c, v, t) =>
                {
                    string id = Variable(v, "id");
                    Run run = await c.Runs.GetAsync(id, t).ConfigureAwait(false);
                    List<RunStage> stages = (await c.Runs.ListStagesAsync(id, new MyMorningNews.Sdk.Options.EnumerationQuery { PageSize = 1000 }, t).ConfigureAwait(false)).Objects;
                    EnumerationResult<Finding> top = await c.Findings.ListAsync(new FindingListOptions { RunId = id, PageSize = 10 }, t).ConfigureAwait(false);
                    return RunMarkdown(run, stages, top.Objects);
                })
            };
        }

        /// <summary>
        /// Render a briefing.
        /// </summary>
        /// <param name="d">Briefing detail.</param>
        /// <returns>Markdown.</returns>
        /// <exception cref="ArgumentNullException">Detail is null.</exception>
        public static string BriefingMarkdown(BriefingDetailResponse d)
        {
            ArgumentNullException.ThrowIfNull(d);
            if (!String.IsNullOrWhiteSpace(d.Briefing.Markdown)) return d.Briefing.Markdown!;
            StringBuilder sb = new StringBuilder();
            sb.Append("# ").Append(d.Briefing.Title).Append("\n\n");
            foreach (BriefingSection s in d.Sections.OrderBy(x => x.SortOrder))
            {
                sb.Append("## ").Append(s.Heading).Append("\n\n");
                if (!String.IsNullOrWhiteSpace(s.Markdown)) sb.Append(s.Markdown!.Trim()).Append("\n\n");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Render a topic's brief.
        /// </summary>
        /// <param name="d">Topic detail.</param>
        /// <returns>Markdown.</returns>
        /// <exception cref="ArgumentNullException">Detail is null.</exception>
        public static string TopicMarkdown(TopicDetailResponse d)
        {
            ArgumentNullException.ThrowIfNull(d);
            StringBuilder sb = new StringBuilder();
            sb.Append("# ").Append(d.Topic.Name).Append("\n\n");
            sb.Append("Status: ").Append(d.Topic.Status).Append(". Topic ID: `").Append(d.Topic.Id).Append("`.\n\n");
            if (!String.IsNullOrWhiteSpace(d.Topic.Description)) sb.Append(d.Topic.Description!.Trim()).Append("\n\n");
            sb.Append("## Brief\n\n").Append(String.IsNullOrWhiteSpace(d.Topic.Brief) ? "(No brief yet.)" : d.Topic.Brief!.Trim()).Append("\n\n");
            sb.Append("## Dimensions\n\n");
            if (!d.Dimensions.Any()) sb.Append("(None.)\n");
            foreach (TopicDimension x in d.Dimensions.OrderBy(x => x.SortOrder))
                sb.Append("- **").Append(x.Name).Append("** (").Append(x.Weight).Append(", `").Append(x.Id).Append("`)").Append(String.IsNullOrWhiteSpace(x.Description) ? String.Empty : ": " + x.Description!.Trim()).Append('\n');
            sb.Append("\n## Constraints\n\n");
            if (!d.Constraints.Any()) sb.Append("(None.)\n");
            foreach (TopicConstraint x in d.Constraints)
                sb.Append("- ").Append(x.Kind).Append(": ").Append(x.Value).Append(" (`").Append(x.Id).Append("`)").Append(String.IsNullOrWhiteSpace(x.Note) ? String.Empty : ", " + x.Note!.Trim()).Append('\n');
            return sb.ToString();
        }

        /// <summary>
        /// Render a run summary.
        /// </summary>
        /// <param name="run">Run.</param>
        /// <param name="stages">Stages.</param>
        /// <param name="top">Top findings.</param>
        /// <returns>Markdown.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static string RunMarkdown(Run run, List<RunStage> stages, List<Finding> top)
        {
            ArgumentNullException.ThrowIfNull(run);
            ArgumentNullException.ThrowIfNull(stages);
            ArgumentNullException.ThrowIfNull(top);
            StringBuilder sb = new StringBuilder();
            sb.Append("# Run ").Append(run.Id).Append("\n\n");
            sb.Append("Status: ").Append(run.Status).Append(run.IsDryRun ? " (dry run)" : String.Empty).Append(". ");
            sb.Append(run.NewFindingCount.ToString(CultureInfo.InvariantCulture)).Append(" new findings, ").Append(run.DuplicateCount.ToString(CultureInfo.InvariantCulture)).Append(" duplicates, ")
                .Append(run.SourceCount.ToString(CultureInfo.InvariantCulture)).Append(" sources.");
            sb.Append("\n\n");
            sb.Append("Cost: ").Append(Usd(run.EstimatedCostUsd)).Append(" (estimated, USD) across ").Append(run.LlmCallCount.ToString(CultureInfo.InvariantCulture)).Append(" model calls, ")
                .Append(run.InputTokens.ToString("N0", CultureInfo.InvariantCulture)).Append(" input and ").Append(run.OutputTokens.ToString("N0", CultureInfo.InvariantCulture)).Append(" output tokens");
            if (run.UnpricedCallCount > 0) sb.Append("; ").Append(run.UnpricedCallCount.ToString(CultureInfo.InvariantCulture)).Append(" calls had no price and are not counted");
            sb.Append(".\n\n");
            if (!String.IsNullOrWhiteSpace(run.ErrorMessage)) sb.Append("Error: ").Append(run.ErrorMessage).Append("\n\n");
            if (!String.IsNullOrWhiteSpace(run.Summary)) sb.Append("## Summary\n\n").Append(run.Summary!.Trim()).Append("\n\n");
            sb.Append("## Stages\n\n| Stage | Status | In | Out | ms | Model calls | Cost (USD) | Share |\n|---|---|---|---|---|---|---|---|\n");
            decimal total = run.EstimatedCostUsd ?? 0m;
            foreach (RunStage s in stages.OrderBy(x => x.SortOrder))
            {
                string share = s.EstimatedCostUsd.HasValue && total > 0m ? Math.Round(s.EstimatedCostUsd.Value / total * 100m, 0).ToString(CultureInfo.InvariantCulture) + "%" : "-";
                sb.Append("| ").Append(s.Stage).Append(" | ").Append(s.Status).Append(" | ").Append(s.ItemsIn).Append(" | ").Append(s.ItemsOut).Append(" | ").Append(Math.Round(s.DurationMs).ToString(CultureInfo.InvariantCulture))
                    .Append(" | ").Append(s.LlmCallCount.ToString(CultureInfo.InvariantCulture)).Append(s.UnpricedCallCount > 0 ? " (" + s.UnpricedCallCount.ToString(CultureInfo.InvariantCulture) + " unpriced)" : String.Empty)
                    .Append(" | ").Append(s.EstimatedCostUsd.HasValue ? Usd(s.EstimatedCostUsd) : "-").Append(" | ").Append(share).Append(" |\n");
            }
            sb.Append("\n## Top findings\n\n");
            if (!top.Any()) sb.Append("(None.)\n");
            foreach (Finding f in top)
                sb.Append("- **").Append(f.Title).Append("** (").Append(Math.Round(f.RelevanceScore).ToString(CultureInfo.InvariantCulture)).Append(", `").Append(f.Id).Append("`)").Append(String.IsNullOrWhiteSpace(f.Summary) ? String.Empty : ": " + f.Summary!.Trim()).Append('\n');
            return sb.ToString();
        }

        private static string Usd(decimal? amount)
        {
            return amount.HasValue ? "$" + amount.Value.ToString("0.0000", CultureInfo.InvariantCulture) : "not priced";
        }

        private static string Variable(IReadOnlyDictionary<string, string> v, string name)
        {
            if (!v.TryGetValue(name, out string? value) || String.IsNullOrWhiteSpace(value)) throw new ArgumentException("The URI needs a " + name + ".");
            return Uri.UnescapeDataString(value);
        }
    }
}
