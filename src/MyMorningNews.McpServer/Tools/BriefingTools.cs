namespace MyMorningNews.McpServer.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using Voltaic.Mcp;

    /// <summary>
    /// Briefing tools: list, read the latest, and read one, as Markdown.
    /// </summary>
    public static class BriefingTools
    {
        /// <summary>
        /// Default Markdown budget for one tool result, in characters.
        /// </summary>
        public const int DefaultMaxCharacters = 20000;

        /// <summary>
        /// Build the tools.
        /// </summary>
        /// <returns>Tools.</returns>
        public static List<McpTool> Build()
        {
            List<McpTool> tools = new List<McpTool>();
            Dictionary<string, object> detailSchema = Schema.Object(Schema.Props(
                "briefing", Shapes.BriefingSchema,
                "topicName", Schema.NullableString("Topic name, for run briefings."),
                "editionName", Schema.NullableString("Edition name, for edition briefings."),
                "markdown", Schema.NullableString("The briefing as Markdown (may be truncated; see maxCharacters)."),
                "sections", Schema.Array("Section headings in order.", Schema.String("Heading.")),
                "items", Schema.Array("Items cited in the briefing, in order.", Schema.Record("Item: type (Finding, Prediction, Suggestion), id, citation."))), new[] { "briefing" }, true);
            Dictionary<string, object> found = Schema.Object(Schema.Props(
                "found", Schema.Boolean("False when there is no ready briefing yet."),
                "briefing", Schema.Record("The briefing detail, as get_briefing returns it, or null.", true)), new[] { "found" }, true);
            Dictionary<string, object> chars = Schema.Integer("Most Markdown characters to return. Default 20000.", 1000, 200000);

            tools.Add(new McpTool("list_briefings", "List briefings",
                "List your briefings, newest first: morning editions, per-run briefings, and one-time reports.",
                Schema.Object(Schema.Props(
                    "kind", Schema.Choice<BriefingKindEnum>("Only this kind."),
                    "topicId", Schema.String("Only this topic's briefings.", 64),
                    "editionId", Schema.String("Only this edition's briefings.", 64),
                    "pageSize", Schema.Integer("Briefings per page. Default 10.", 1, 50),
                    "pageNumber", Schema.Integer("Page (1-based). Default 1.", 1, 10000))),
                Schema.Page(Shapes.BriefingSchema), true, false,
                async (a, c, t) =>
                {
                    BriefingListOptions o = new BriefingListOptions
                    {
                        Kind = a.Choice<BriefingKindEnum>("kind"), TopicId = a.OptionalText("topicId", 64), EditionId = a.OptionalText("editionId", 64),
                        PageSize = a.Integer("pageSize", 10, 1, 50), PageNumber = a.Integer("pageNumber", 1, 1, 10000), Mine = true
                    };
                    return Shapes.Page(await c.Briefings.ListAsync(o, t).ConfigureAwait(false), Shapes.Briefing);
                }));

            tools.Add(new McpTool("get_latest_briefing", "Read today's briefing",
                "Read your newest ready briefing as Markdown. Pass topicId or editionId to narrow it, for example to one topic's latest run briefing.",
                Schema.Object(Schema.Props(
                    "kind", Schema.Choice<BriefingKindEnum>("Only this kind."),
                    "topicId", Schema.String("Only this topic.", 64),
                    "editionId", Schema.String("Only this edition.", 64),
                    "maxCharacters", chars)),
                found, true, false,
                async (a, c, t) =>
                {
                    BriefingListOptions o = new BriefingListOptions
                    {
                        Kind = a.Choice<BriefingKindEnum>("kind"), TopicId = a.OptionalText("topicId", 64), EditionId = a.OptionalText("editionId", 64), Status = BriefingStatusEnum.Ready, Mine = true
                    };
                    BriefingDetailResponse? d = await c.Briefings.GetLatestAsync(o, t).ConfigureAwait(false);
                    return new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        { "found", d != null }, { "briefing", d == null ? null : Detail(d, a.Integer("maxCharacters", DefaultMaxCharacters, 1000, 200000)) }
                    };
                }));

            tools.Add(new McpTool("get_briefing", "Read a briefing",
                "Read one briefing as Markdown, with its sections and the items it cites (use their IDs with get_finding or submit_feedback).",
                Schema.Object(Schema.Props("briefingId", Schema.String("Briefing ID (brf_...).", 64), "maxCharacters", chars), new[] { "briefingId" }),
                detailSchema, true, false,
                async (a, c, t) => Detail(await c.Briefings.GetAsync(a.RequiredText("briefingId", 64), t).ConfigureAwait(false), a.Integer("maxCharacters", DefaultMaxCharacters, 1000, 200000))));

            return tools;
        }

        /// <summary>
        /// Shape a briefing detail with its Markdown limited to a budget.
        /// </summary>
        /// <param name="d">Detail.</param>
        /// <param name="maxCharacters">Markdown budget.</param>
        /// <returns>Projection.</returns>
        /// <exception cref="ArgumentNullException">Detail is null.</exception>
        public static Dictionary<string, object?> Detail(BriefingDetailResponse d, int maxCharacters)
        {
            ArgumentNullException.ThrowIfNull(d);
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                { "briefing", Shapes.Briefing(d.Briefing) },
                { "topicName", d.TopicName },
                { "editionName", d.EditionName },
                { "markdown", Shapes.Clip(d.Briefing.Markdown, maxCharacters) },
                { "sections", d.Sections.OrderBy(s => s.SortOrder).Select(s => s.Heading).ToList() },
                { "items", d.Items.OrderBy(i => i.SortOrder).Select(i => new Dictionary<string, object?> { { "type", i.ItemType.ToString() }, { "id", i.ItemId }, { "citation", i.CitationNumber } }).ToList() }
            };
        }

        /// <summary>
        /// The newest ready briefing as Markdown, for resources and prompts. Null when there is none.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Detail or null.</returns>
        /// <exception cref="ArgumentNullException">Client is null.</exception>
        public static Task<BriefingDetailResponse?> LatestAsync(MyMorningNewsClient client, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.Briefings.GetLatestAsync(new BriefingListOptions { Status = BriefingStatusEnum.Ready, Mine = true }, token);
        }
    }
}
