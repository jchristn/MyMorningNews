namespace MyMorningNews.McpServer.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;
    using Voltaic.Mcp;

    /// <summary>
    /// Finding tools: list with filters, search, read with evidence, and pin or hide.
    /// </summary>
    public static class FindingTools
    {
        /// <summary>
        /// Build the tools.
        /// </summary>
        /// <returns>Tools.</returns>
        public static List<McpTool> Build()
        {
            List<McpTool> tools = new List<McpTool>();
            Dictionary<string, object> filters = Schema.Props(
                "topicId", Schema.String("Only this topic (top_...).", 64),
                "runId", Schema.String("Only this run (run_...).", 64),
                "tier", Schema.Choice<FindingTierEnum>("Only this tier."),
                "minScore", Schema.Integer("Minimum relevance score (0 to 100).", 0, 100),
                "since", Schema.String("Only findings found at or after this time (ISO 8601, for example 2026-11-18).", 40),
                "pinned", Schema.Boolean("Only pinned (true) or unpinned (false) findings."),
                "includeHidden", Schema.Boolean("Include findings you hid. Default false."),
                "pageSize", Schema.Integer("Findings per page. Default 20.", 1, 100),
                "pageNumber", Schema.Integer("Page (1-based). Default 1.", 1, 10000));

            tools.Add(new McpTool("list_findings", "List findings",
                "List research findings, highest relevance first. Duplicates, dry runs, and below-threshold findings are left out unless tier is BelowThreshold.",
                Schema.Object(filters), Schema.Page(Shapes.FindingSchema), true, false,
                async (a, c, t) => Shapes.Page(await c.Findings.ListAsync(Options(a, null), t).ConfigureAwait(false), Shapes.Finding)));

            Dictionary<string, object> searchProps = new Dictionary<string, object>(filters, StringComparer.Ordinal) { ["query"] = Schema.String("Words to look for in titles and summaries.", 200) };
            tools.Add(new McpTool("search_findings", "Search findings",
                "Search findings by text in their titles and summaries, with the same filters as list_findings.",
                Schema.Object(searchProps, new[] { "query" }), Schema.Page(Shapes.FindingSchema), true, false,
                async (a, c, t) => Shapes.Page(await c.Findings.ListAsync(Options(a, a.RequiredText("query", 200)), t).ConfigureAwait(false), Shapes.Finding)));

            tools.Add(new McpTool("get_finding", "Read a finding with evidence",
                "Read one finding with its verified evidence: the exact quotes, where they came from, and the score breakdown.",
                Schema.Object(Schema.Props("findingId", Schema.String("Finding ID (fnd_...).", 64)), new[] { "findingId" }),
                Schema.Object(Schema.Props(
                    "finding", Shapes.FindingSchema,
                    "topicName", Schema.NullableString("Topic name."),
                    "dimension", Schema.NullableString("The dimension it answers."),
                    "rationale", Schema.NullableString("Why the model scored it this way."),
                    "scores", Schema.Record("Score components (0 to 100): llm, feedback, novelty, source, recency."),
                    "evidence", Schema.Array("Verified quotes.", Schema.Record("Evidence: id, quote, verified, role, sourceTitle, sourceUrl, sourceDomain, sourcePublishedUtc."))), new[] { "finding", "evidence" }, true),
                true, false,
                async (a, c, t) =>
                {
                    FindingDetailResponse d = await c.Findings.GetAsync(a.RequiredText("findingId", 64), t).ConfigureAwait(false);
                    Finding f = d.Finding;
                    return new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        { "finding", Shapes.Finding(f) },
                        { "topicName", d.TopicName },
                        { "dimension", d.Dimension?.Name },
                        { "rationale", f.RelevanceRationale },
                        { "scores", new Dictionary<string, object?> { { "llm", Math.Round(f.LlmScore, 1) }, { "feedback", Math.Round(f.FeedbackScore, 1) }, { "novelty", Math.Round(f.NoveltyScore, 1) }, { "source", Math.Round(f.SourceScore, 1) }, { "recency", Math.Round(f.RecencyScore, 1) } } },
                        { "evidence", d.Evidence.Select(e => new Dictionary<string, object?>
                            {
                                { "id", e.Evidence.Id }, { "quote", e.Evidence.Quote }, { "verified", e.Evidence.Verified }, { "role", e.Role }, { "sourceTitle", e.SourceTitle },
                                { "sourceUrl", e.SourceUrl }, { "sourceDomain", e.SourceDomain }, { "sourcePublishedUtc", Shapes.Iso(e.SourcePublishedUtc) }
                            }).ToList() }
                    };
                }));

            tools.Add(new McpTool("update_finding", "Pin or hide a finding",
                "Pin a finding to keep it at hand, or hide one you do not want to see again. Hidden findings stay out of lists, briefings, and emails.",
                Schema.Object(Schema.Props(
                    "findingId", Schema.String("Finding ID (fnd_...).", 64),
                    "pinned", Schema.Boolean("Pin (true) or unpin (false)."),
                    "hidden", Schema.Boolean("Hide (true) or show (false).")), new[] { "findingId" }),
                Shapes.FindingSchema, false, false,
                async (a, c, t) =>
                {
                    FindingUpdateRequest req = new FindingUpdateRequest { Pinned = a.Flag("pinned"), Hidden = a.Flag("hidden") };
                    if (!req.Pinned.HasValue && !req.Hidden.HasValue) throw new McpToolException("Send pinned, hidden, or both.");
                    return Shapes.Finding(await c.Findings.UpdateAsync(a.RequiredText("findingId", 64), req, t).ConfigureAwait(false));
                }));

            return tools;
        }

        private static FindingListOptions Options(ToolArguments a, string? search)
        {
            FindingListOptions o = new FindingListOptions
            {
                TopicId = a.OptionalText("topicId", 64),
                RunId = a.OptionalText("runId", 64),
                Tier = a.Choice<FindingTierEnum>("tier"),
                MinScore = a.Number("minScore", 0, 100) is double min ? (int)Math.Round(min) : null,
                FromUtc = a.Date("since"),
                Pinned = a.Flag("pinned"),
                IncludeHidden = a.Flag("includeHidden"),
                Search = search,
                PageSize = a.Integer("pageSize", 20, 1, 100),
                PageNumber = a.Integer("pageNumber", 1, 1, 10000),
                Mine = true
            };
            return o;
        }
    }
}
