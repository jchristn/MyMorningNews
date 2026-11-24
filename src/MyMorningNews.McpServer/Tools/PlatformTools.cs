namespace MyMorningNews.McpServer.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;
    using MyMorningNews.Sdk.Serialization;
    using Voltaic.Mcp;

    /// <summary>Catalog, effective allowances and historical comparison. No keys, payment tokens or purchasing tools are exposed.</summary>
    public static class PlatformTools
    {
        /// <summary>Build member tools using the authenticated caller's SDK client.</summary>
        public static List<McpTool> Build()
        {
            Dictionary<string, object> empty = Schema.Object(Schema.Props());
            Dictionary<string, object> page = Schema.Object(Schema.Props("pageNumber", Schema.Integer("Page, default 1.", 1, 10000), "pageSize", Schema.Integer("Results per page, default 20.", 1, 100)));
            List<McpTool> tools = new List<McpTool>
            {
                new McpTool("get_account_plan", "Read my plan", "Read your effective plan, funding mode, inherited allowances and per-user override sources. No purchase is made.",
                    empty, Schema.Record("Effective plan and features."), true, false, async (a, c, t) => Wire(await c.Account.GetPlanAsync(t).ConfigureAwait(false))),
                new McpTool("get_account_usage", "Read my usage", "Read custom topic and system subscription counts, daily generated and reserved audio seconds, and the UTC reset. Audio playback is free.",
                    empty, Schema.Record("Current account usage; null caps mean unlimited."), true, false, async (a, c, t) => Wire(await c.Account.GetUsageAsync(t).ConfigureAwait(false))),
                new McpTool("list_system_briefings", "Browse system briefings", "Browse published system-wide briefings with eligibility and subscription status. Canonical prompts and research internals are excluded.",
                    page, Schema.Page(Schema.Record("Public catalog entry.")), true, false, async (a, c, t) => Wire(await c.Catalog.ListAsync(new CatalogListOptions { PageNumber = a.Integer("pageNumber", 1, 1, 10000), PageSize = a.Integer("pageSize", 20, 1, 100) }, t).ConfigureAwait(false))),
                new McpTool("list_system_subscriptions", "Read my system subscriptions", "List your private catalog subscriptions, delivery preferences and suspension status.",
                    page, Schema.Page(Schema.Record("Private catalog subscription.")), true, false, async (a, c, t) => Wire(await c.Catalog.SubscriptionsAsync(new ListOptions { PageNumber = a.Integer("pageNumber", 1, 1, 10000), PageSize = a.Integer("pageSize", 20, 1, 100) }, t).ConfigureAwait(false))),
                new McpTool("subscribe_system_briefing", "Subscribe to a system briefing", "Subscribe to an eligible system briefing or update its delivery preferences. This uses the system subscription allowance, without creating custom research or a paid purchase.",
                    Schema.Object(Schema.Props("publicationId", Schema.String("System publication ID.", 64), "emailEnabled", Schema.Boolean("Email new issues; default true."), "pushEnabled", Schema.Boolean("Notify mobile devices; default true.")), new[] { "publicationId" }),
                    Schema.Record("Saved private subscription."), false, false, async (a, c, t) => Wire(await c.Catalog.SubscribeAsync(a.RequiredText("publicationId", 64), new CatalogSubscriptionRequest { EmailEnabled = a.Flag("emailEnabled") ?? true, PushEnabled = a.Flag("pushEnabled") ?? true }, t).ConfigureAwait(false))),
                new McpTool("unsubscribe_system_briefing", "Unsubscribe from a system briefing", "Stop receiving a shared briefing and release its subscription slot. Shared issues remain owned by the platform.",
                    Schema.Object(Schema.Props("publicationId", Schema.String("System publication ID.", 64)), new[] { "publicationId" }), Schema.Record("Unsubscribed confirmation."), false, true,
                    async (a, c, t) => { await c.Catalog.UnsubscribeAsync(a.RequiredText("publicationId", 64), t).ConfigureAwait(false); return new Dictionary<string, object> { { "unsubscribed", true } }; }),
                new McpTool("list_system_issues", "List shared briefing issues", "List readable issues from all your current system subscriptions. Suspended and retracted publications are excluded.",
                    page, Schema.Page(Schema.Record("Shared issue content projection.")), true, false, async (a, c, t) => Wire(await c.Catalog.AllIssuesAsync(new CatalogIssueListOptions { PageNumber = a.Integer("pageNumber", 1, 1, 10000), PageSize = a.Integer("pageSize", 20, 1, 100) }, t).ConfigureAwait(false))),
                new McpTool("get_system_issue", "Read a shared briefing", "Read one shared issue as Markdown through your subscription. No subscriber list or canonical workspace data is exposed.",
                    Schema.Object(Schema.Props("publicationId", Schema.String("System publication ID.", 64), "issueId", Schema.String("Issue ID.", 64), "maxCharacters", Schema.Integer("Maximum Markdown characters, default 20000.", 1000, 200000)), new[] { "publicationId", "issueId" }),
                    Schema.Record("Issue and whether its Markdown was truncated."), true, false, async (a, c, t) =>
                    {
                        CatalogIssueResponse issue = await c.Catalog.IssueAsync(a.RequiredText("publicationId", 64), a.RequiredText("issueId", 64), t).ConfigureAwait(false);
                        int maximum = a.Integer("maxCharacters", 20000, 1000, 200000);
                        bool truncated = issue.Markdown?.Length > maximum;
                        if (truncated) issue.Markdown = issue.Markdown!.Substring(0, maximum);
                        return new Dictionary<string, object?> { { "issue", Wire(issue) }, { "truncated", truncated } };
                    }),
                new McpTool("set_topic_history", "Set briefing comparison context", "Choose whether future research compares earlier briefings to identify changes. Off produces standalone briefings. Only owned issues may be selected; the server freezes a bounded baseline for each run.",
                    Schema.Object(Schema.Props("topicId", Schema.String("Owned topic ID.", 64), "mode", Schema.Choice<HistoryModeEnum>("Comparison mode."), "count", Schema.Integer("Maximum baseline issues, default 5.", 1, 20), "sinceUtc", Schema.String("Start timestamp for SinceDate."), "briefingId", Schema.String("Baseline issue for SpecificIssue.", 64)), new[] { "topicId", "mode" }),
                    Shapes.TopicSchema, false, false, async (a, c, t) =>
                    {
                        HistoryModeEnum mode = a.Choice<HistoryModeEnum>("mode") ?? throw new McpToolException("mode is required.");
                        return Shapes.Topic(await c.Topics.UpdateAsync(a.RequiredText("topicId", 64), new TopicUpsertRequest { HistoryMode = mode, HistoryCount = a.Integer("count", 5, 1, 20), HistorySinceUtc = a.Date("sinceUtc"), HistoryBriefingId = a.OptionalText("briefingId", 64) }, t).ConfigureAwait(false));
                    })
            };
            return tools;
        }

        private static object Wire<T>(T value)
        {
            return JsonSerializer.Deserialize<JsonElement>(MyMorningNewsSerializer.Serialize(value));
        }
    }
}
