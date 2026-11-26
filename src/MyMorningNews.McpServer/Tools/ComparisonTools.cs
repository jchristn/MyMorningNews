namespace MyMorningNews.McpServer.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Sdk;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;
    using Voltaic.Mcp;

    /// <summary>
    /// Comparison and plan-slot tools: preview a topic's or an edition's comparison baseline, set an edition's comparison,
    /// read what a briefing was compared with, choose the system briefings an edition includes, and choose which custom
    /// topics keep running on a smaller plan. Every call runs as the authenticated caller, so the server's ownership and
    /// plan checks apply; no keys or other users' data are returned.
    /// </summary>
    public static class ComparisonTools
    {
        /// <summary>
        /// Most system briefings an edition can include.
        /// </summary>
        public const int MaxPublications = 20;

        /// <summary>
        /// Most topics one keep order can list.
        /// </summary>
        public const int MaxKeepTopics = 200;

        /// <summary>
        /// Topics returned per page by get_topic_slots when pageSize is omitted, and by set_topic_slots.
        /// </summary>
        public const int DefaultSlotPageSize = 25;

        /// <summary>
        /// Build the tools.
        /// </summary>
        /// <returns>Tools.</returns>
        public static List<McpTool> Build()
        {
            List<McpTool> tools = new List<McpTool>();

            tools.Add(new McpTool("preview_topic_history", "Preview a topic's comparison baseline",
                "Show which earlier issues the topic's next research run would compare with, how large that context is, and whether it would be cut to the budget. " +
                "Pass mode, count, sinceUtc, briefingId, or tokenBudget to try settings without saving them; anything left out uses the saved value. Nothing is changed.",
                Schema.Object(HistoryInputProps("topicId", "Owned topic ID (top_...)."), new[] { "topicId" }),
                PreviewSchema, true, false,
                async (a, c, t) => Preview(await c.Topics.PreviewHistoryAsync(a.RequiredText("topicId", 64), HistoryRequest(a), t).ConfigureAwait(false))));

            tools.Add(new McpTool("preview_edition_history", "Preview an edition's comparison baseline",
                "Show which earlier issues of this edition the next composition would compare with. Pass settings to try them without saving; anything left out uses the saved value. Nothing is changed.",
                Schema.Object(HistoryInputProps("editionId", "Owned edition ID (edn_...)."), new[] { "editionId" }),
                PreviewSchema, true, false,
                async (a, c, t) => Preview(await c.Editions.PreviewHistoryAsync(a.RequiredText("editionId", 64), HistoryRequest(a), t).ConfigureAwait(false))));

            tools.Add(new McpTool("set_edition_history", "Set an edition's comparison",
                "Choose whether each composed edition is compared with its own earlier issues. Off (the default) keeps each topic's own research comparisons. " +
                "Settings left out keep their saved values; briefingId must be a ready issue of this edition.",
                Schema.Object(HistoryInputProps("editionId", "Owned edition ID (edn_...)."), new[] { "editionId", "mode" }),
                EditionSchema, false, false,
                async (a, c, t) =>
                {
                    HistoryModeEnum mode = a.Choice<HistoryModeEnum>("mode") ?? throw new McpToolException("mode is required.");
                    HistoryPreviewRequest h = HistoryRequest(a);
                    EditionUpsertRequest req = new EditionUpsertRequest
                    {
                        HistoryMode = mode, HistoryCount = h.HistoryCount, HistorySinceUtc = h.HistorySinceUtc, HistoryBriefingId = h.HistoryBriefingId, HistoryTokenBudget = h.HistoryTokenBudget
                    };
                    return Edition(await c.Editions.UpdateAsync(a.RequiredText("editionId", 64), req, t).ConfigureAwait(false));
                }));

            tools.Add(new McpTool("get_briefing_comparison", "Read what a briefing was compared with",
                "Read the earlier issues a briefing was compared with and the changes found: new developments, changed facts, corrections, resolved items, unchanged items, and items that became unavailable, in display order. " +
                "A briefing composed without a comparison returns compared false with a note.",
                Schema.Object(Schema.Props("briefingId", Schema.String("Briefing ID (brf_...).", 64)), new[] { "briefingId" }),
                ComparisonSchema, true, false,
                async (a, c, t) => Comparison(await c.Briefings.GetComparisonAsync(a.RequiredText("briefingId", 64), t).ConfigureAwait(false))));

            tools.Add(new McpTool("get_edition_publications", "List an edition's system briefings",
                "List the system briefings (catalog publications) an edition includes, in order, and whether each can be read now. Unavailable ones are skipped when the edition is composed.",
                Schema.Object(Schema.Props("editionId", Schema.String("Owned edition ID (edn_...).", 64)), new[] { "editionId" }),
                PublicationsSchema, true, false,
                async (a, c, t) => Publications(await c.Editions.GetAsync(a.RequiredText("editionId", 64), t).ConfigureAwait(false))));

            tools.Add(new McpTool("set_edition_publications", "Choose an edition's system briefings",
                "Replace the system briefings an edition includes with publicationIds, in order (distinct, at most 20, each one you are subscribed to; see list_system_subscriptions). " +
                "An empty list removes them all. Including system briefings needs a plan with custom research; the server refuses otherwise. No research is started.",
                Schema.Object(Schema.Props(
                    "editionId", Schema.String("Owned edition ID (edn_...).", 64),
                    "publicationIds", Schema.Array("Publication IDs (cat_...) in order. Empty removes every publication.", Schema.String("Publication ID.", 64), MaxPublications)), new[] { "editionId", "publicationIds" }),
                PublicationsSchema, false, false,
                async (a, c, t) =>
                {
                    EditionPublicationsRequest req = new EditionPublicationsRequest { PublicationIds = a.TextList("publicationIds", MaxPublications) };
                    return Publications(await c.Editions.SetPublicationsAsync(a.RequiredText("editionId", 64), req, t).ConfigureAwait(false));
                }));

            tools.Add(new McpTool("get_topic_slots", "Read my topic slots",
                "Read your custom topic count against your plan's topic limit plus one page of your topics in keep order: which ones fit the plan and run, and which keep their history but do not run. " +
                "Position and withinPlan reflect the whole order, so a later page or a search keeps each topic's true slot; use pageNumber and pageSize to read further. " +
                "While overLimit is above zero, new topics cannot be created, duplicated, or restored.",
                Schema.Object(Schema.Props(
                    "search", Schema.String("Text in the topic name.", 200),
                    "pageSize", Schema.Integer("Topics per page. Default " + DefaultSlotPageSize + ".", 1, 100),
                    "pageNumber", Schema.Integer("Page (1-based). Default 1.", 1, 10000))),
                SlotsSchema, true, false,
                async (a, c, t) =>
                {
                    TopicSlotsResponse summary = await c.Account.GetTopicSlotsAsync(t).ConfigureAwait(false);
                    ListOptions o = new ListOptions { Search = a.OptionalText("search", 200), PageSize = a.Integer("pageSize", DefaultSlotPageSize, 1, 100), PageNumber = a.Integer("pageNumber", 1, 1, 10000) };
                    return Slots(summary, await c.Account.GetTopicSlotTopicsAsync(o, t).ConfigureAwait(false));
                }));

            tools.Add(new McpTool("set_topic_slots", "Choose which topics keep running",
                "Set the order in which your custom topics keep running when your plan allows fewer than you have, most important first. Topics you leave out follow in creation order. " +
                "An empty list clears the choice. Nothing is paused, archived, or deleted; a choice saved ahead of a downgrade applies when the limit shrinks. " +
                "Returns the updated count and the first " + DefaultSlotPageSize + " topics in the new keep order; use get_topic_slots to read further pages.",
                Schema.Object(Schema.Props(
                    "keepTopicIds", Schema.Array("Recurring, non-archived topic IDs (top_...) in keep order. Empty clears the choice.", Schema.String("Topic ID.", 64), MaxKeepTopics)), new[] { "keepTopicIds" }),
                SlotsSchema, false, false,
                async (a, c, t) =>
                {
                    TopicSlotsResponse summary = await c.Account.SetTopicSlotsAsync(new TopicSlotsRequest { KeepTopicIds = a.TextList("keepTopicIds", MaxKeepTopics) }, t).ConfigureAwait(false);
                    return Slots(summary, await c.Account.GetTopicSlotTopicsAsync(new ListOptions { PageSize = DefaultSlotPageSize }, t).ConfigureAwait(false));
                }));

            return tools;
        }

        /// <summary>
        /// Shape a baseline preview.
        /// </summary>
        /// <param name="p">Preview.</param>
        /// <returns>Projection.</returns>
        /// <exception cref="ArgumentNullException">Preview is null.</exception>
        public static Dictionary<string, object?> Preview(HistoryPreviewResponse p)
        {
            ArgumentNullException.ThrowIfNull(p);
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                { "mode", p.Mode }, { "baselines", p.Baselines.Select(Baseline).ToList() }, { "contextBytes", p.ContextBytes }, { "budgetBytes", p.BudgetBytes },
                { "truncated", p.Truncated }, { "note", p.Note }, { "cutoffUtc", Shapes.Iso(p.CutoffUtc) }
            };
        }

        /// <summary>
        /// Shape a briefing comparison.
        /// </summary>
        /// <param name="r">Comparison.</param>
        /// <returns>Projection.</returns>
        /// <exception cref="ArgumentNullException">Comparison is null.</exception>
        public static Dictionary<string, object?> Comparison(BriefingComparisonResponse r)
        {
            ArgumentNullException.ThrowIfNull(r);
            Dictionary<string, object?> counts = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (string kind in Enum.GetNames<BriefingDeltaKindEnum>()) counts[kind] = r.Counts.TryGetValue(kind, out int n) ? n : 0;
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                { "briefingId", r.BriefingId }, { "compared", r.Compared }, { "mode", r.Mode }, { "note", r.Note }, { "summary", r.Summary }, { "truncated", r.Truncated },
                { "noMaterialChanges", r.NoMaterialChanges }, { "cutoffUtc", Shapes.Iso(r.CutoffUtc) }, { "baselines", r.Baselines.Select(Baseline).ToList() }, { "counts", counts },
                {
                    "deltas", r.Deltas.OrderBy(d => d.SortOrder).Select(d => new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        { "kind", d.Kind.ToString() }, { "title", d.Title }, { "summary", d.Summary }, { "findingId", d.FindingId }, { "predictionId", d.PredictionId },
                        { "baselineBriefingId", d.BaselineBriefingId }, { "baselineItemId", d.BaselineItemId }
                    }).ToList()
                }
            };
        }

        /// <summary>
        /// Shape an edition with its comparison settings and system briefings.
        /// </summary>
        /// <param name="r">Edition response.</param>
        /// <returns>Projection.</returns>
        /// <exception cref="ArgumentNullException">Edition response is null.</exception>
        public static Dictionary<string, object?> Edition(EditionResponse r)
        {
            ArgumentNullException.ThrowIfNull(r);
            Edition e = r.Edition;
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                { "id", e.Id }, { "name", e.Name }, { "enabled", e.Enabled }, { "historyMode", e.HistoryMode.ToString() }, { "historyCount", e.HistoryCount },
                { "historySinceUtc", Shapes.Iso(e.HistorySinceUtc) }, { "historyBriefingId", e.HistoryBriefingId }, { "historyTokenBudget", e.HistoryTokenBudget },
                { "publications", r.Publications.Select(Publication).ToList() }, { "lastComposedUtc", Shapes.Iso(e.LastComposedUtc) }
            };
        }

        /// <summary>
        /// Shape an edition's system briefings.
        /// </summary>
        /// <param name="r">Edition response.</param>
        /// <returns>Projection.</returns>
        /// <exception cref="ArgumentNullException">Edition response is null.</exception>
        public static Dictionary<string, object?> Publications(EditionResponse r)
        {
            ArgumentNullException.ThrowIfNull(r);
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                { "editionId", r.Edition.Id }, { "publications", r.Publications.OrderBy(p => p.SortOrder).Select(Publication).ToList() }
            };
        }

        /// <summary>
        /// Shape the account's topic slot summary with one page of its topics in slot order.
        /// </summary>
        /// <param name="r">Slot summary.</param>
        /// <param name="page">One page of topics in slot order.</param>
        /// <returns>Projection.</returns>
        /// <exception cref="ArgumentNullException">Summary or page is null.</exception>
        public static Dictionary<string, object?> Slots(TopicSlotsResponse r, EnumerationResult<TopicSlotResponse> page)
        {
            ArgumentNullException.ThrowIfNull(r);
            ArgumentNullException.ThrowIfNull(page);
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                { "limit", r.Limit }, { "counted", r.Counted }, { "overLimit", r.OverLimit }, { "customized", r.Customized },
                {
                    "topics", page.Objects.OrderBy(x => x.Position).Select(x => new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        { "topicId", x.TopicId }, { "name", x.Name }, { "status", x.Status.ToString() }, { "position", x.Position }, { "chosen", x.Chosen },
                        { "withinPlan", x.WithinPlan }, { "lastRunUtc", Shapes.Iso(x.LastRunUtc) }
                    }).ToList()
                },
                { "totalRecords", page.TotalRecords }, { "pageNumber", page.PageNumber }, { "pageSize", page.PageSize }, { "totalPages", page.TotalPages }
            };
        }

        private static Dictionary<string, object> HistoryInputProps(string idName, string idDescription)
        {
            return Schema.Props(
                idName, Schema.String(idDescription, 64),
                "mode", Schema.Choice<HistoryModeEnum>("Comparison mode."),
                "count", Schema.Integer("Maximum baseline issues for RecentIssues and SinceDate.", 1, 20),
                "sinceUtc", Schema.String("Earliest baseline time for SinceDate (ISO 8601, in the past)."),
                "briefingId", Schema.String("Pinned baseline issue (brf_...) for SpecificIssue.", 64),
                "tokenBudget", Schema.Integer("Comparison context budget in UTF-8 bytes.", 256, 32000));
        }

        private static HistoryPreviewRequest HistoryRequest(ToolArguments a)
        {
            return new HistoryPreviewRequest
            {
                HistoryMode = a.Choice<HistoryModeEnum>("mode"), HistoryCount = a.OptionalInteger("count", 1, 20), HistorySinceUtc = a.Date("sinceUtc"),
                HistoryBriefingId = a.OptionalText("briefingId", 64), HistoryTokenBudget = a.OptionalInteger("tokenBudget", 256, 32000)
            };
        }

        private static Dictionary<string, object?> Baseline(ComparisonBaseline b)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { { "id", b.Id }, { "title", b.Title }, { "createdUtc", Shapes.Iso(b.CreatedUtc) }, { "available", b.Available } };
        }

        private static Dictionary<string, object?> Publication(EditionPublicationResponse p)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { { "publicationId", p.PublicationId }, { "title", p.Title }, { "sortOrder", p.SortOrder }, { "available", p.Available } };
        }

        private static Dictionary<string, object> BaselineSchema
        {
            get
            {
                return Schema.Object(Schema.Props(
                    "id", Schema.String("Briefing ID (brf_...)."), "title", Schema.String("Title."), "createdUtc", Schema.NullableString("Created (UTC)."),
                    "available", Schema.Boolean("False when the issue can no longer be read.")), new[] { "id", "available" }, true);
            }
        }

        private static Dictionary<string, object> PublicationSchema
        {
            get
            {
                return Schema.Object(Schema.Props(
                    "publicationId", Schema.String("Publication ID (cat_...)."), "title", Schema.String("Title."), "sortOrder", Schema.Integer("Position in the edition.", 0, int.MaxValue),
                    "available", Schema.Boolean("False when retracted, unsubscribed, or no longer allowed by the plan; skipped at composition.")), new[] { "publicationId", "available" }, true);
            }
        }

        private static Dictionary<string, object> PreviewSchema
        {
            get
            {
                return Schema.Object(Schema.Props(
                    "mode", Schema.String("Mode the preview used. " + Shapes.Names<HistoryModeEnum>()),
                    "baselines", Schema.Array("Issues that would be compared with, newest first.", BaselineSchema),
                    "contextBytes", Schema.Integer("Size of the comparison context in UTF-8 bytes.", 0, int.MaxValue),
                    "budgetBytes", Schema.Integer("Budget in UTF-8 bytes.", 0, int.MaxValue),
                    "truncated", Schema.Boolean("The context would be cut to the budget."),
                    "note", Schema.NullableString("Why nothing would be compared, or null."),
                    "cutoffUtc", Schema.NullableString("Issues created before this time are eligible (UTC).")), new[] { "mode", "baselines" }, true);
            }
        }

        private static Dictionary<string, object> ComparisonSchema
        {
            get
            {
                Dictionary<string, object> counts = Schema.Props();
                foreach (string kind in Enum.GetNames<BriefingDeltaKindEnum>()) counts[kind] = Schema.Integer(kind + " changes.", 0, int.MaxValue);
                return Schema.Object(Schema.Props(
                    "briefingId", Schema.String("Briefing ID."),
                    "compared", Schema.Boolean("True when at least one earlier issue was compared."),
                    "mode", Schema.String("Mode used, for example PreviousIssue, Edition:PreviousIssue, or Off."),
                    "note", Schema.NullableString("Why the briefing stands alone, or null."),
                    "summary", Schema.NullableString("One-line summary, the same sentence the briefing carries, or null."),
                    "truncated", Schema.Boolean("The baseline was cut to its budget."),
                    "noMaterialChanges", Schema.Boolean("Compared and nothing new, changed, corrected, or resolved was found."),
                    "cutoffUtc", Schema.NullableString("Issues created before this time were eligible (UTC)."),
                    "baselines", Schema.Array("Issues compared with, newest first.", BaselineSchema),
                    "counts", Schema.Object(counts, Enum.GetNames<BriefingDeltaKindEnum>(), false),
                    "deltas", Schema.Array("Changes in display order: corrections, changes, new, resolved, unchanged, unavailable.", Schema.Object(Schema.Props(
                        "kind", Schema.String(Shapes.Names<BriefingDeltaKindEnum>()), "title", Schema.String("Title."), "summary", Schema.NullableString("Summary."),
                        "findingId", Schema.NullableString("Current finding (use with get_finding)."), "predictionId", Schema.NullableString("Prediction."),
                        "baselineBriefingId", Schema.NullableString("Earlier briefing the item comes from."), "baselineItemId", Schema.NullableString("Item in the earlier briefing.")),
                        new[] { "kind", "title" }, true))), new[] { "briefingId", "compared", "counts", "deltas" }, true);
            }
        }

        private static Dictionary<string, object> EditionSchema
        {
            get
            {
                return Schema.Object(Schema.Props(
                    "id", Schema.String("Edition ID (edn_...)."), "name", Schema.String("Name."), "enabled", Schema.Boolean("Composed on its schedules."),
                    "historyMode", Schema.String(Shapes.Names<HistoryModeEnum>()), "historyCount", Schema.Integer("Maximum baseline issues.", 1, 20),
                    "historySinceUtc", Schema.NullableString("Earliest baseline time (UTC)."), "historyBriefingId", Schema.NullableString("Pinned baseline issue."),
                    "historyTokenBudget", Schema.Integer("Comparison budget in UTF-8 bytes.", 256, 32000),
                    "publications", Schema.Array("System briefings the edition includes, in order.", PublicationSchema),
                    "lastComposedUtc", Schema.NullableString("Last composed (UTC).")), new[] { "id", "name", "historyMode" }, true);
            }
        }

        private static Dictionary<string, object> PublicationsSchema
        {
            get
            {
                return Schema.Object(Schema.Props(
                    "editionId", Schema.String("Edition ID."),
                    "publications", Schema.Array("System briefings the edition includes, in order.", PublicationSchema)), new[] { "editionId", "publications" }, true);
            }
        }

        private static Dictionary<string, object> SlotsSchema
        {
            get
            {
                return Schema.Object(Schema.Props(
                    "limit", Schema.Number("Custom topics the plan allows; null for unlimited, 0 when custom research is not in the plan.", true),
                    "counted", Schema.Integer("Recurring, non-archived topics. Paused topics count.", 0, int.MaxValue),
                    "overLimit", Schema.Integer("Topics past the limit: they keep history but do not run.", 0, int.MaxValue),
                    "customized", Schema.Boolean("You have chosen a keep order."),
                    "topics", Schema.Array("One page of topics in keep order.", Schema.Object(Schema.Props(
                        "topicId", Schema.String("Topic ID."), "name", Schema.String("Name."), "status", Schema.String(Shapes.Names<TopicStatusEnum>()),
                        "position", Schema.Integer("Position (1-based).", 1, int.MaxValue), "chosen", Schema.Boolean("You placed it explicitly."),
                        "withinPlan", Schema.Boolean("It fits the plan and runs."), "lastRunUtc", Schema.NullableString("Last run (UTC).")),
                        new[] { "topicId", "position", "withinPlan" }, true)),
                    "totalRecords", Schema.Integer("Topics matching across all pages.", 0, long.MaxValue),
                    "pageNumber", Schema.Integer("Page number (1-based).", 1, int.MaxValue),
                    "pageSize", Schema.Integer("Maximum topics on this page.", 1, 1000),
                    "totalPages", Schema.Integer("Pages available.", 0, int.MaxValue)), new[] { "counted", "overLimit", "customized", "topics", "totalRecords" }, true);
            }
        }
    }
}
