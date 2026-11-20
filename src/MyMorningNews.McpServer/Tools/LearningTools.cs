namespace MyMorningNews.McpServer.Tools
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;
    using Voltaic.Mcp;

    /// <summary>
    /// Feedback, learned preference, and analytics tools.
    /// </summary>
    public static class LearningTools
    {
        /// <summary>
        /// Ratings accepted by submit_feedback, as the REST action names.
        /// </summary>
        public static readonly string[] Ratings = new[] { "relevant", "somewhat", "not-relevant", "accurate", "partially", "inaccurate", "too-early", "useful", "not-useful" };

        /// <summary>
        /// Build the tools.
        /// </summary>
        /// <returns>Tools.</returns>
        public static List<McpTool> Build()
        {
            List<McpTool> tools = new List<McpTool>();

            tools.Add(new McpTool("submit_feedback", "Rate an item, with a reason",
                "Rate a finding, prediction, suggestion, briefing, briefing section, source, or domain, ideally with a short reason in your own words. "
                + "Ratings change ranking on the next run, and reasons are distilled into preferences you can accept. A new rating replaces your earlier one for the same item. "
                + "Findings, briefings, sections, sources, and domains take relevant, somewhat, or not-relevant. Predictions take accurate, partially, inaccurate, or too-early. "
                + "Suggestions take useful or not-useful.",
                Schema.Object(Schema.Props(
                    "targetType", Schema.Choice("What is being rated.", "Finding", "Prediction", "Suggestion", "Briefing", "BriefingSection", "SourceItem", "Domain"),
                    "targetId", Schema.String("The item's ID (for Domain, the domain name).", 253),
                    "rating", Schema.Choice("The rating.", Ratings),
                    "reason", Schema.String("Why, in a sentence or two. Optional but it is what the system learns from.", 2000)), new[] { "targetType", "targetId" }),
                Shapes.FeedbackSchema, false, false,
                async (a, c, t) =>
                {
                    FeedbackTargetTypeEnum target = a.Choice<FeedbackTargetTypeEnum>("targetType") ?? throw new McpToolException("targetType is required.");
                    if (target == FeedbackTargetTypeEnum.LearnedPreference || target == FeedbackTargetTypeEnum.PredictionReview)
                        throw new McpToolException("Use accept_learned_preference or reject_learned_preference for preferences.");
                    string? rating = a.Text("rating")?.ToLowerInvariant();
                    if (rating != null && Array.IndexOf(Ratings, rating) < 0) throw new McpToolException("rating must be one of: " + String.Join(", ", Ratings) + ".");
                    string? reason = a.OptionalText("reason", 2000);
                    if (rating == null && reason == null) throw new McpToolException("Send a rating, a reason, or both.");
                    FeedbackCreateRequest req = new FeedbackCreateRequest { TargetType = target, TargetId = a.RequiredText("targetId", 253), Action = rating, Reason = reason, Channel = FeedbackChannelEnum.Mcp };
                    return Shapes.Feedback(await c.Feedback.CreateAsync(req, t).ConfigureAwait(false));
                }));

            tools.Add(new McpTool("list_learned_preferences", "List learned preferences",
                "List the preferences that steer your research: ones you wrote, and ones proposed from your ratings (status Proposed) that wait for your decision.",
                Schema.Object(Schema.Props(
                    "topicId", Schema.String("Only this topic's preferences.", 64),
                    "status", Schema.Choice<PreferenceStatusEnum>("Only this status, for example Proposed."),
                    "pageSize", Schema.Integer("Preferences per page. Default 25.", 1, 100),
                    "pageNumber", Schema.Integer("Page (1-based). Default 1.", 1, 10000))),
                Schema.Page(Shapes.PreferenceSchema), true, false,
                async (a, c, t) =>
                {
                    PreferenceListOptions o = new PreferenceListOptions
                    {
                        TopicId = a.OptionalText("topicId", 64), Status = a.Choice<PreferenceStatusEnum>("status"), PageSize = a.Integer("pageSize", 25, 1, 100), PageNumber = a.Integer("pageNumber", 1, 1, 10000), Mine = true
                    };
                    return Shapes.Page(await c.Preferences.ListAsync(o, t).ConfigureAwait(false), Shapes.Preference);
                }));

            tools.Add(new McpTool("accept_learned_preference", "Accept a proposed preference",
                "Accept a proposed preference so future runs follow it.",
                Schema.Object(Schema.Props("preferenceId", Schema.String("Preference ID (lpr_...).", 64)), new[] { "preferenceId" }),
                Shapes.PreferenceSchema, false, false,
                async (a, c, t) => Shapes.Preference(await c.Preferences.AcceptAsync(a.RequiredText("preferenceId", 64), t).ConfigureAwait(false))));

            tools.Add(new McpTool("reject_learned_preference", "Reject a proposed preference",
                "Reject a proposed preference. It will not be proposed again from the same ratings.",
                Schema.Object(Schema.Props("preferenceId", Schema.String("Preference ID (lpr_...).", 64)), new[] { "preferenceId" }),
                Shapes.PreferenceSchema, false, false,
                async (a, c, t) => Shapes.Preference(await c.Preferences.RejectAsync(a.RequiredText("preferenceId", 64), t).ConfigureAwait(false))));

            tools.Add(new McpTool("get_analytics_summary", "Summarize research quality",
                "Headline numbers for a window (default the last 30 days): runs, findings shown and rated, relevance precision, prediction accuracy and Brier score, "
                + "feedback, suggestions, quote verification, briefings, emails, and model cost.",
                Schema.Object(Schema.Props(
                    "topicId", Schema.String("Only this topic.", 64),
                    "days", Schema.Integer("Window length in days, ending now. Default 30.", 1, 365))),
                Schema.Object(Schema.Props(
                    "fromUtc", Schema.String("Window start (UTC)."), "toUtc", Schema.String("Window end (UTC)."),
                    "activeTopics", Schema.Integer("Active topics.", 0, long.MaxValue), "runs", Schema.Record("Runs: total, completed, partiallySucceeded, failed."),
                    "findings", Schema.Record("Findings: shown, rated, relevancePrecision."), "predictions", Schema.Record("Predictions: open, resolved, accuracy, meanBrier."),
                    "feedback", Schema.Record("Feedback: total, withReason."), "suggestions", Schema.Record("Suggestions: new, accepted."),
                    "quoteFailureRate", Schema.Number("Share of quotes that failed verification.", true), "briefingsReady", Schema.Integer("Briefings composed.", 0, long.MaxValue),
                    "emails", Schema.Record("Emails: sent, failed."), "model", Schema.Record("Model use: calls, costUsd.")), new[] { "fromUtc", "toUtc" }, true),
                true, false,
                async (a, c, t) =>
                {
                    DateTime to = DateTime.UtcNow;
                    AnalyticsOptions o = new AnalyticsOptions { TopicId = a.OptionalText("topicId", 64), FromUtc = to.AddDays(-a.Integer("days", 30, 1, 365)), ToUtc = to };
                    OverviewReport r = await c.Analytics.OverviewAsync(o, t).ConfigureAwait(false);
                    return new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        { "fromUtc", Shapes.Iso(r.FromUtc) }, { "toUtc", Shapes.Iso(r.ToUtc) }, { "activeTopics", r.ActiveTopics },
                        { "runs", new Dictionary<string, object?> { { "total", r.Runs }, { "completed", r.RunsCompleted }, { "partiallySucceeded", r.RunsPartiallySucceeded }, { "failed", r.RunsFailed } } },
                        { "findings", new Dictionary<string, object?> { { "shown", r.FindingsShown }, { "rated", r.FindingsRated }, { "relevancePrecision", r.RelevancePrecision } } },
                        { "predictions", new Dictionary<string, object?> { { "open", r.PredictionsOpen }, { "resolved", r.PredictionsResolved }, { "accuracy", r.PredictionAccuracy }, { "meanBrier", r.MeanBrier } } },
                        { "feedback", new Dictionary<string, object?> { { "total", r.FeedbackTotal }, { "withReason", r.FeedbackWithReason } } },
                        { "suggestions", new Dictionary<string, object?> { { "new", r.SuggestionsNew }, { "accepted", r.SuggestionsAccepted } } },
                        { "quoteFailureRate", r.QuoteFailureRate }, { "briefingsReady", r.BriefingsReady },
                        { "emails", new Dictionary<string, object?> { { "sent", r.EmailsSent }, { "failed", r.EmailsFailed } } },
                        { "model", new Dictionary<string, object?> { { "calls", r.ModelCalls }, { "costUsd", r.ModelCostUsd } } }
                    };
                }));

            return tools;
        }
    }
}
