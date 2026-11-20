namespace MyMorningNews.McpServer.Tools
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;
    using Voltaic.Mcp;

    /// <summary>
    /// Prediction and suggestion tools: list, resolve a prediction, and change a suggestion's status.
    /// </summary>
    public static class OutcomeTools
    {
        /// <summary>
        /// Build the tools.
        /// </summary>
        /// <returns>Tools.</returns>
        public static List<McpTool> Build()
        {
            List<McpTool> tools = new List<McpTool>();

            tools.Add(new McpTool("list_predictions", "List predictions",
                "List predictions the research made, with their confidence and when they should be judged. Open predictions past their horizon are ready to resolve.",
                Schema.Object(Schema.Props(
                    "topicId", Schema.String("Only this topic.", 64),
                    "status", Schema.Choice<PredictionStatusEnum>("Only this status, for example Open."),
                    "pageSize", Schema.Integer("Predictions per page. Default 20.", 1, 100),
                    "pageNumber", Schema.Integer("Page (1-based). Default 1.", 1, 10000))),
                Schema.Page(Shapes.PredictionSchema), true, false,
                async (a, c, t) =>
                {
                    PredictionListOptions o = new PredictionListOptions
                    {
                        TopicId = a.OptionalText("topicId", 64), Status = a.Choice<PredictionStatusEnum>("status"), PageSize = a.Integer("pageSize", 20, 1, 100), PageNumber = a.Integer("pageNumber", 1, 1, 10000), Mine = true
                    };
                    return Shapes.Page(await c.Predictions.ListAsync(o, t).ConfigureAwait(false), Shapes.Prediction);
                }));

            tools.Add(new McpTool("resolve_prediction", "Resolve a prediction",
                "Record how a prediction turned out. This feeds the accuracy and calibration reports.",
                Schema.Object(Schema.Props(
                    "predictionId", Schema.String("Prediction ID (prd_...).", 64),
                    "outcome", Schema.Choice("How it turned out.", "Accurate", "PartiallyAccurate", "Inaccurate", "Unresolvable"),
                    "note", Schema.String("What happened, briefly.", 1000)), new[] { "predictionId", "outcome" }),
                Shapes.PredictionSchema, false, false,
                async (a, c, t) =>
                {
                    PredictionStatusEnum outcome = a.Choice<PredictionStatusEnum>("outcome") ?? throw new McpToolException("outcome is required.");
                    if (outcome == PredictionStatusEnum.Open || outcome == PredictionStatusEnum.ProposedResolved || outcome == PredictionStatusEnum.Withdrawn)
                        throw new McpToolException("outcome must be Accurate, PartiallyAccurate, Inaccurate, or Unresolvable.");
                    PredictionResolveRequest req = new PredictionResolveRequest { Status = outcome, Note = a.OptionalText("note", 1000) };
                    return Shapes.Prediction(await c.Predictions.ResolveAsync(a.RequiredText("predictionId", 64), req, t).ConfigureAwait(false));
                }));

            tools.Add(new McpTool("list_suggestions", "List suggestions",
                "List suggested next steps from your research, highest priority first.",
                Schema.Object(Schema.Props(
                    "topicId", Schema.String("Only this topic.", 64),
                    "status", Schema.Choice<SuggestionStatusEnum>("Only this status, for example New."),
                    "pageSize", Schema.Integer("Suggestions per page. Default 20.", 1, 100),
                    "pageNumber", Schema.Integer("Page (1-based). Default 1.", 1, 10000))),
                Schema.Page(Shapes.SuggestionSchema), true, false,
                async (a, c, t) =>
                {
                    SuggestionListOptions o = new SuggestionListOptions
                    {
                        TopicId = a.OptionalText("topicId", 64), Status = a.Choice<SuggestionStatusEnum>("status"), PageSize = a.Integer("pageSize", 20, 1, 100), PageNumber = a.Integer("pageNumber", 1, 1, 10000),
                        SortBy = "priority", Mine = true
                    };
                    return Shapes.Page(await c.Suggestions.ListAsync(o, t).ConfigureAwait(false), Shapes.Suggestion);
                }));

            tools.Add(new McpTool("update_suggestion_status", "Accept, snooze, dismiss, or complete a suggestion",
                "Change a suggestion's status. Snoozing needs snoozeDays (or snoozedUntil).",
                Schema.Object(Schema.Props(
                    "suggestionId", Schema.String("Suggestion ID (sug_...).", 64),
                    "status", Schema.Choice("New status.", "Accepted", "Snoozed", "Dismissed", "Done"),
                    "snoozeDays", Schema.Integer("Snooze for this many days.", 1, 365),
                    "snoozedUntil", Schema.String("Snooze until this time (ISO 8601).", 40)), new[] { "suggestionId", "status" }),
                Shapes.SuggestionSchema, false, false,
                async (a, c, t) =>
                {
                    SuggestionStatusEnum status = a.Choice<SuggestionStatusEnum>("status") ?? throw new McpToolException("status is required.");
                    if (status == SuggestionStatusEnum.New) throw new McpToolException("status must be Accepted, Snoozed, Dismissed, or Done.");
                    DateTime? until = a.Date("snoozedUntil");
                    if (until == null && a.Number("snoozeDays", 1, 365) is double days) until = DateTime.UtcNow.AddDays(days);
                    if (status == SuggestionStatusEnum.Snoozed && until == null) throw new McpToolException("Snoozing needs snoozeDays or snoozedUntil.");
                    SuggestionStatusRequest req = new SuggestionStatusRequest { Status = status, SnoozedUntilUtc = status == SuggestionStatusEnum.Snoozed ? until : null };
                    return Shapes.Suggestion(await c.Suggestions.SetStatusAsync(a.RequiredText("suggestionId", 64), req, t).ConfigureAwait(false));
                }));

            return tools;
        }
    }
}
