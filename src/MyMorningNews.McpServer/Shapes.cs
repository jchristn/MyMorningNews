namespace MyMorningNews.McpServer
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using MyMorningNews.Sdk;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;

    /// <summary>
    /// Compact projections of REST records for tool results, with matching output schemas. Tools return these rather than
    /// full records so results stay small enough for a model's context. Thread safe (stateless).
    /// </summary>
    public static class Shapes
    {
        #region Public-Methods

        /// <summary>
        /// Shape a topic.
        /// </summary>
        /// <param name="t">Topic.</param>
        /// <returns>Projection.</returns>
        public static Dictionary<string, object?> Topic(Topic t)
        {
            ArgumentNullException.ThrowIfNull(t);
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                { "id", t.Id }, { "name", t.Name }, { "status", t.Status.ToString() }, { "description", t.Description }, { "isOneTime", t.IsOneTime },
                { "emailOnRun", t.EmailOnRun }, { "lastRunUtc", Iso(t.LastRunUtc) }, { "createdUtc", Iso(t.CreatedUtc) }
            };
        }

        /// <summary>
        /// Topic schema.
        /// </summary>
        public static Dictionary<string, object> TopicSchema
        {
            get
            {
                return Schema.Object(Schema.Props(
                    "id", Schema.String("Topic ID (top_...)."), "name", Schema.String("Name."), "status", Schema.String(Names<TopicStatusEnum>()),
                    "description", Schema.NullableString("Description."), "isOneTime", Schema.Boolean("One-time report rather than recurring."),
                    "emailOnRun", Schema.Boolean("Emails after every run."), "lastRunUtc", Schema.NullableString("Last run (UTC)."), "createdUtc", Schema.String("Created (UTC).")),
                    new[] { "id", "name", "status" }, true);
            }
        }

        /// <summary>
        /// Shape a run.
        /// </summary>
        /// <param name="r">Run.</param>
        /// <returns>Projection.</returns>
        public static Dictionary<string, object?> Run(Run r)
        {
            ArgumentNullException.ThrowIfNull(r);
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                { "id", r.Id }, { "topicId", r.TopicId }, { "status", r.Status.ToString() }, { "trigger", r.Trigger.ToString() }, { "isDryRun", r.IsDryRun },
                { "queuedUtc", Iso(r.QueuedUtc) }, { "startedUtc", Iso(r.StartedUtc) }, { "completedUtc", Iso(r.CompletedUtc) }, { "summary", r.Summary },
                { "newFindingCount", r.NewFindingCount }, { "duplicateCount", r.DuplicateCount }, { "sourceCount", r.SourceCount },
                { "estimatedCostUsd", r.EstimatedCostUsd }, { "llmCallCount", r.LlmCallCount }, { "unpricedCallCount", r.UnpricedCallCount },
                { "inputTokens", r.InputTokens }, { "outputTokens", r.OutputTokens }, { "errorMessage", r.ErrorMessage }, { "skippedReason", r.SkippedReason }
            };
        }

        /// <summary>
        /// Run schema.
        /// </summary>
        public static Dictionary<string, object> RunSchema
        {
            get
            {
                return Schema.Object(Schema.Props(
                    "id", Schema.String("Run ID (run_...)."), "topicId", Schema.String("Topic."), "status", Schema.String(Names<RunStatusEnum>()),
                    "trigger", Schema.String("What started it."), "isDryRun", Schema.Boolean("Dry run (nothing saved for delivery)."),
                    "queuedUtc", Schema.NullableString("Queued (UTC)."), "startedUtc", Schema.NullableString("Started (UTC)."), "completedUtc", Schema.NullableString("Completed (UTC)."),
                    "summary", Schema.NullableString("Synthesis summary."), "newFindingCount", Schema.Integer("New findings.", 0, int.MaxValue),
                    "duplicateCount", Schema.Integer("Duplicates.", 0, int.MaxValue), "sourceCount", Schema.Integer("Sources fetched.", 0, int.MaxValue),
                    "estimatedCostUsd", Schema.Number("Estimated model cost in US dollars, from the endpoints' prices. Null when no call was priced. Updated after every stage.", true),
                    "llmCallCount", Schema.Integer("Model calls made during the run.", 0, int.MaxValue),
                    "unpricedCallCount", Schema.Integer("Model calls with no price (an endpoint without prices, or an agent CLI on a subscription), left out of the cost.", 0, int.MaxValue),
                    "inputTokens", Schema.Integer("Input tokens.", 0, int.MaxValue), "outputTokens", Schema.Integer("Output tokens.", 0, int.MaxValue),
                    "errorMessage", Schema.NullableString("Error, when failed."), "skippedReason", Schema.NullableString("Why it was skipped, when skipped.")),
                    new[] { "id", "topicId", "status" }, true);
            }
        }

        /// <summary>
        /// Shape a run stage.
        /// </summary>
        /// <param name="s">Stage.</param>
        /// <returns>Projection.</returns>
        public static Dictionary<string, object?> Stage(RunStage s)
        {
            ArgumentNullException.ThrowIfNull(s);
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                { "id", s.Id }, { "stage", s.Stage }, { "status", s.Status.ToString() }, { "itemsIn", s.ItemsIn }, { "itemsOut", s.ItemsOut }, { "durationMs", Math.Round(s.DurationMs, 0) },
                { "llmCallCount", s.LlmCallCount }, { "unpricedCallCount", s.UnpricedCallCount }, { "estimatedCostUsd", s.EstimatedCostUsd }, { "error", s.ErrorMessage }
            };
        }

        /// <summary>
        /// Run stage schema.
        /// </summary>
        public static Dictionary<string, object> StageSchema
        {
            get
            {
                return Schema.Object(Schema.Props(
                    "id", Schema.String("Run stage ID."), "stage", Schema.String("Stage name, for example Plan, Search, Extract, Synthesize."), "status", Schema.String(Names<StageStatusEnum>()),
                    "itemsIn", Schema.Integer("Items into the stage.", 0, int.MaxValue), "itemsOut", Schema.Integer("Items out of the stage.", 0, int.MaxValue),
                    "durationMs", Schema.Number("Duration in milliseconds."), "llmCallCount", Schema.Integer("Model calls made in the stage.", 0, int.MaxValue),
                    "unpricedCallCount", Schema.Integer("Model calls with no price, left out of the cost.", 0, int.MaxValue),
                    "estimatedCostUsd", Schema.Number("Estimated model cost of the stage in US dollars. Null when no call was priced.", true), "error", Schema.NullableString("Error, when failed.")),
                    new[] { "stage", "status" }, true);
            }
        }

        /// <summary>
        /// Shape cost and token totals.
        /// </summary>
        /// <param name="c">Totals.</param>
        /// <returns>Projection.</returns>
        public static Dictionary<string, object?> Cost(CostTotals c)
        {
            ArgumentNullException.ThrowIfNull(c);
            Dictionary<string, object?> d = new Dictionary<string, object?>(StringComparer.Ordinal);
            AddCost(d, c);
            return d;
        }

        /// <summary>
        /// Cost totals schema.
        /// </summary>
        public static Dictionary<string, object> CostSchema
        {
            get
            {
                return Schema.Object(CostProps(), new[] { "estimatedCostUsd", "llmCallCount" }, true);
            }
        }

        /// <summary>
        /// Shape a run cost report.
        /// </summary>
        /// <param name="r">Report.</param>
        /// <returns>Projection.</returns>
        public static Dictionary<string, object?> RunCosts(RunCostReport r)
        {
            ArgumentNullException.ThrowIfNull(r);
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                { "runId", r.RunId }, { "topicId", r.TopicId }, { "status", r.Status.ToString() }, { "currency", r.Currency }, { "totals", Cost(r.Totals) },
                { "costPerNewFindingUsd", r.CostPerNewFindingUsd },
                { "stages", r.Stages.OrderBy(x => x.SortOrder).Select(x =>
                    {
                        Dictionary<string, object?> d = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            { "runStageId", x.RunStageId }, { "stage", x.Stage }, { "status", x.Status.ToString() }, { "durationMs", Math.Round(x.DurationMs, 0) }, { "shareOfCost", x.ShareOfCost }
                        };
                        AddCost(d, x);
                        return d;
                    }).ToList() },
                { "unattributed", r.Unattributed == null ? null : Cost(r.Unattributed) },
                { "byModel", r.ByModel.Select(x =>
                    {
                        Dictionary<string, object?> d = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            { "modelEndpointId", x.ModelEndpointId }, { "endpointName", x.EndpointName }, { "provider", x.Provider }, { "model", x.Model }
                        };
                        AddCost(d, x);
                        return d;
                    }).ToList() },
                { "byTask", r.ByTask.Select(x =>
                    {
                        Dictionary<string, object?> d = new Dictionary<string, object?>(StringComparer.Ordinal) { { "task", x.Task.ToString() } };
                        AddCost(d, x);
                        return d;
                    }).ToList() }
            };
        }

        /// <summary>
        /// Run cost report schema.
        /// </summary>
        public static Dictionary<string, object> RunCostsSchema
        {
            get
            {
                Dictionary<string, object> stage = WithCost(Schema.Props(
                    "runStageId", Schema.String("Run stage ID."), "stage", Schema.String("Stage name."), "status", Schema.String(Names<StageStatusEnum>()),
                    "durationMs", Schema.Number("Stage duration in milliseconds."),
                    "shareOfCost", Schema.Number("Share of the run's cost, from 0 to 1. Null when the run has no priced cost.", true)));
                Dictionary<string, object> model = WithCost(Schema.Props(
                    "modelEndpointId", Schema.NullableString("Model endpoint ID."), "endpointName", Schema.NullableString("Endpoint name."),
                    "provider", Schema.NullableString("Provider."), "model", Schema.NullableString("Model.")));
                Dictionary<string, object> task = WithCost(Schema.Props("task", Schema.String(Names<ModelTaskEnum>())));
                Dictionary<string, object> totals = CostSchema;
                totals["description"] = "Totals across the run.";
                return Schema.Object(Schema.Props(
                    "runId", Schema.String("Run ID."), "topicId", Schema.String("Topic."), "status", Schema.String(Names<RunStatusEnum>()), "currency", Schema.String("Always USD."),
                    "totals", totals,
                    "costPerNewFindingUsd", Schema.Number("Estimated cost divided by new findings. Null when there is no priced cost or no new finding.", true),
                    "stages", Schema.Array("One row per stage, in run order.", Schema.Object(stage, new[] { "stage", "llmCallCount" }, true)),
                    "unattributed", Schema.Record("Totals of calls with no stage (same fields as totals), or null when every call has one.", true),
                    "byModel", Schema.Array("By endpoint and model, most expensive first.", Schema.Object(model, new[] { "llmCallCount" }, true)),
                    "byTask", Schema.Array("By model task, most expensive first.", Schema.Object(task, new[] { "task", "llmCallCount" }, true))),
                    new[] { "runId", "currency", "totals", "stages" }, true);
            }
        }

        /// <summary>
        /// Shape a finding.
        /// </summary>
        /// <param name="f">Finding.</param>
        /// <returns>Projection.</returns>
        public static Dictionary<string, object?> Finding(Finding f)
        {
            ArgumentNullException.ThrowIfNull(f);
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                { "id", f.Id }, { "topicId", f.TopicId }, { "runId", f.RunId }, { "title", f.Title }, { "summary", f.Summary }, { "whyItMatters", f.WhyItMatters },
                { "category", f.Category.ToString() }, { "tier", f.Tier.ToString() }, { "relevanceScore", Math.Round(f.RelevanceScore, 1) }, { "confidence", Math.Round(f.Confidence, 2) },
                { "isDuplicate", f.IsDuplicate }, { "pinned", f.Pinned }, { "hidden", f.Hidden }, { "publishedUtc", Iso(f.PublishedUtc) }, { "createdUtc", Iso(f.CreatedUtc) }
            };
        }

        /// <summary>
        /// Finding schema.
        /// </summary>
        public static Dictionary<string, object> FindingSchema
        {
            get
            {
                return Schema.Object(Schema.Props(
                    "id", Schema.String("Finding ID (fnd_...)."), "topicId", Schema.String("Topic."), "runId", Schema.String("Run."), "title", Schema.String("Title."),
                    "summary", Schema.NullableString("Summary."), "whyItMatters", Schema.NullableString("Why it matters for the topic."), "category", Schema.String("Category."),
                    "tier", Schema.String(Names<FindingTierEnum>()), "relevanceScore", Schema.Number("Composite relevance, 0 to 100."),
                    "confidence", Schema.Number("Model confidence, 0 to 1."), "isDuplicate", Schema.Boolean("Repeats an earlier finding."),
                    "pinned", Schema.Boolean("Pinned by the owner."), "hidden", Schema.Boolean("Hidden by the owner."),
                    "publishedUtc", Schema.NullableString("Source publication time (UTC)."), "createdUtc", Schema.String("Found (UTC).")),
                    new[] { "id", "title", "relevanceScore" }, true);
            }
        }

        /// <summary>
        /// Shape a briefing without its Markdown.
        /// </summary>
        /// <param name="b">Briefing.</param>
        /// <returns>Projection.</returns>
        public static Dictionary<string, object?> Briefing(Briefing b)
        {
            ArgumentNullException.ThrowIfNull(b);
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                { "id", b.Id }, { "title", b.Title }, { "kind", b.Kind.ToString() }, { "status", b.Status.ToString() }, { "topicId", b.TopicId }, { "editionId", b.EditionId },
                { "runId", b.RunId }, { "itemCount", b.ItemCount }, { "wordCount", b.WordCount }, { "readMinutes", b.ReadMinutes }, { "hasAudio", !String.IsNullOrEmpty(b.MediaAssetId) },
                { "pinned", b.Pinned }, { "composedUtc", Iso(b.ComposedUtc) }, { "createdUtc", Iso(b.CreatedUtc) }
            };
        }

        /// <summary>
        /// Briefing schema (without Markdown).
        /// </summary>
        public static Dictionary<string, object> BriefingSchema
        {
            get
            {
                return Schema.Object(Schema.Props(
                    "id", Schema.String("Briefing ID (brf_...)."), "title", Schema.String("Title."), "kind", Schema.String(Names<BriefingKindEnum>()),
                    "status", Schema.String(Names<BriefingStatusEnum>()), "topicId", Schema.NullableString("Topic, for run briefings."),
                    "editionId", Schema.NullableString("Edition, for edition briefings."), "runId", Schema.NullableString("Run."), "itemCount", Schema.Integer("Items.", 0, int.MaxValue),
                    "wordCount", Schema.Integer("Words.", 0, int.MaxValue), "readMinutes", Schema.Integer("Reading time in minutes.", 0, int.MaxValue),
                    "hasAudio", Schema.Boolean("Has an audio version."), "pinned", Schema.Boolean("Kept past retention."),
                    "composedUtc", Schema.NullableString("Composed (UTC)."), "createdUtc", Schema.String("Created (UTC).")),
                    new[] { "id", "title", "status" }, true);
            }
        }

        /// <summary>
        /// Shape a prediction.
        /// </summary>
        /// <param name="p">Prediction.</param>
        /// <returns>Projection.</returns>
        public static Dictionary<string, object?> Prediction(Prediction p)
        {
            ArgumentNullException.ThrowIfNull(p);
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                { "id", p.Id }, { "topicId", p.TopicId }, { "statement", p.Statement }, { "rationale", p.Rationale }, { "confidence", Math.Round(p.Confidence, 2) },
                { "horizonUtc", Iso(p.HorizonUtc) }, { "status", p.Status.ToString() }, { "resolvedUtc", Iso(p.ResolvedUtc) }, { "resolutionNote", p.ResolutionNote },
                { "brierScore", p.BrierScore.HasValue ? Math.Round(p.BrierScore.Value, 4) : null }
            };
        }

        /// <summary>
        /// Prediction schema.
        /// </summary>
        public static Dictionary<string, object> PredictionSchema
        {
            get
            {
                return Schema.Object(Schema.Props(
                    "id", Schema.String("Prediction ID (prd_...)."), "topicId", Schema.String("Topic."), "statement", Schema.String("What is predicted."),
                    "rationale", Schema.NullableString("Why."), "confidence", Schema.Number("Stated probability, 0 to 1."), "horizonUtc", Schema.String("When it should be judged (UTC)."),
                    "status", Schema.String(Names<PredictionStatusEnum>()), "resolvedUtc", Schema.NullableString("Resolved (UTC)."),
                    "resolutionNote", Schema.NullableString("Resolution note."), "brierScore", Schema.Number("Brier score once graded (lower is better).", true)),
                    new[] { "id", "statement", "status" }, true);
            }
        }

        /// <summary>
        /// Shape a suggestion.
        /// </summary>
        /// <param name="s">Suggestion.</param>
        /// <returns>Projection.</returns>
        public static Dictionary<string, object?> Suggestion(Suggestion s)
        {
            ArgumentNullException.ThrowIfNull(s);
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                { "id", s.Id }, { "topicId", s.TopicId }, { "title", s.Title }, { "description", s.Description }, { "rationale", s.Rationale },
                { "priority", s.Priority.ToString() }, { "effort", s.Effort.ToString() }, { "status", s.Status.ToString() }, { "snoozedUntilUtc", Iso(s.SnoozedUntilUtc) },
                { "createdUtc", Iso(s.CreatedUtc) }
            };
        }

        /// <summary>
        /// Suggestion schema.
        /// </summary>
        public static Dictionary<string, object> SuggestionSchema
        {
            get
            {
                return Schema.Object(Schema.Props(
                    "id", Schema.String("Suggestion ID (sug_...)."), "topicId", Schema.String("Topic."), "title", Schema.String("What to do."),
                    "description", Schema.NullableString("Detail."), "rationale", Schema.NullableString("Why."), "priority", Schema.String("Priority."), "effort", Schema.String("Effort."),
                    "status", Schema.String(Names<SuggestionStatusEnum>()), "snoozedUntilUtc", Schema.NullableString("Snoozed until (UTC)."),
                    "createdUtc", Schema.String("Created (UTC).")),
                    new[] { "id", "title", "status" }, true);
            }
        }

        /// <summary>
        /// Shape a learned preference.
        /// </summary>
        /// <param name="p">Preference.</param>
        /// <returns>Projection.</returns>
        public static Dictionary<string, object?> Preference(LearnedPreference p)
        {
            ArgumentNullException.ThrowIfNull(p);
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                { "id", p.Id }, { "topicId", p.TopicId }, { "kind", p.Kind.ToString() }, { "statement", p.Statement }, { "status", p.Status.ToString() },
                { "origin", p.Origin.ToString() }, { "supportCount", p.SupportCount }, { "confidence", Math.Round(p.Confidence, 2) }
            };
        }

        /// <summary>
        /// Preference schema.
        /// </summary>
        public static Dictionary<string, object> PreferenceSchema
        {
            get
            {
                return Schema.Object(Schema.Props(
                    "id", Schema.String("Preference ID (lpr_...)."), "topicId", Schema.NullableString("Topic, or null for all topics."),
                    "kind", Schema.String(Names<PreferenceKindEnum>()), "statement", Schema.String("The preference in plain words."),
                    "status", Schema.String(Names<PreferenceStatusEnum>()), "origin", Schema.String(Names<PreferenceOriginEnum>()),
                    "supportCount", Schema.Integer("Ratings behind it.", 0, int.MaxValue), "confidence", Schema.Number("Confidence, 0 to 1.")),
                    new[] { "id", "statement", "status" }, true);
            }
        }

        /// <summary>
        /// Shape a feedback record.
        /// </summary>
        /// <param name="f">Feedback.</param>
        /// <returns>Projection.</returns>
        public static Dictionary<string, object?> Feedback(Feedback f)
        {
            ArgumentNullException.ThrowIfNull(f);
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                { "id", f.Id }, { "targetType", f.TargetType.ToString() }, { "targetId", f.TargetId }, { "relevance", f.Relevance?.ToString() },
                { "accuracy", f.Accuracy?.ToString() }, { "usefulness", f.Usefulness?.ToString() }, { "reason", f.Reason }, { "channel", f.Channel.ToString() },
                { "createdUtc", Iso(f.CreatedUtc) }
            };
        }

        /// <summary>
        /// Feedback schema.
        /// </summary>
        public static Dictionary<string, object> FeedbackSchema
        {
            get
            {
                return Schema.Object(Schema.Props(
                    "id", Schema.String("Feedback ID (fbk_...)."), "targetType", Schema.String("What was rated."), "targetId", Schema.String("Rated item."),
                    "relevance", Schema.NullableString("Relevance rating."), "accuracy", Schema.NullableString("Accuracy rating."), "usefulness", Schema.NullableString("Usefulness rating."),
                    "reason", Schema.NullableString("The reason given."), "channel", Schema.String("Where it came from (Mcp for this server)."), "createdUtc", Schema.String("Recorded (UTC).")),
                    new[] { "id", "targetType", "targetId" }, true);
            }
        }

        /// <summary>
        /// Shape a page of records.
        /// </summary>
        /// <typeparam name="T">Record type.</typeparam>
        /// <param name="page">Page.</param>
        /// <param name="shape">Projection.</param>
        /// <returns>Projection with items and totals.</returns>
        public static Dictionary<string, object?> Page<T>(EnumerationResult<T> page, Func<T, Dictionary<string, object?>> shape)
        {
            ArgumentNullException.ThrowIfNull(page);
            ArgumentNullException.ThrowIfNull(shape);
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                { "items", page.Items.Select(shape).ToList() }, { "totalCount", page.TotalCount }, { "pageNumber", page.PageNumber }, { "totalPages", page.TotalPages }
            };
        }

        /// <summary>
        /// Shorten text to a limit, marking the cut.
        /// </summary>
        /// <param name="text">Text, or null.</param>
        /// <param name="max">Maximum characters.</param>
        /// <returns>Text, possibly shortened, or null.</returns>
        public static string? Clip(string? text, int max)
        {
            if (text == null || text.Length <= max) return text;
            return text.Substring(0, max) + "\n\n[truncated: " + (text.Length - max) + " more characters; raise maxCharacters to read more]";
        }

        /// <summary>
        /// "One of: A, B, C." for an enum.
        /// </summary>
        /// <typeparam name="T">Enum type.</typeparam>
        /// <returns>Description text.</returns>
        public static string Names<T>() where T : struct, Enum
        {
            return "One of: " + String.Join(", ", Enum.GetNames<T>()) + ".";
        }

        /// <summary>
        /// ISO 8601 UTC text for a time, or null.
        /// </summary>
        /// <param name="utc">Time, or null.</param>
        /// <returns>Text or null.</returns>
        public static string? Iso(DateTime? utc)
        {
            return utc.HasValue ? DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) : null;
        }

        #endregion

        #region Private-Methods

        private static void AddCost(Dictionary<string, object?> d, CostTotals c)
        {
            d["estimatedCostUsd"] = c.EstimatedCostUsd;
            d["inputCostUsd"] = c.InputCostUsd;
            d["outputCostUsd"] = c.OutputCostUsd;
            d["llmCallCount"] = c.LlmCallCount;
            d["unpricedCallCount"] = c.UnpricedCallCount;
            d["failedCallCount"] = c.FailedCallCount;
            d["inputTokens"] = c.InputTokens;
            d["outputTokens"] = c.OutputTokens;
            d["cachedInputTokens"] = c.CachedInputTokens;
            d["cacheWriteTokens"] = c.CacheWriteTokens;
            d["reasoningTokens"] = c.ReasoningTokens;
            d["modelDurationMs"] = Math.Round(c.ModelDurationMs, 0);
        }

        private static Dictionary<string, object> WithCost(Dictionary<string, object> identity)
        {
            foreach (KeyValuePair<string, object> p in CostProps()) identity[p.Key] = p.Value;
            return identity;
        }

        private static Dictionary<string, object> CostProps()
        {
            return Schema.Props(
                "estimatedCostUsd", Schema.Number("Estimated cost in US dollars, from the endpoints' prices. Null when no call was priced.", true),
                "inputCostUsd", Schema.Number("Input share of the cost (uncached, cached, and cache-write input). Null when no call was priced.", true),
                "outputCostUsd", Schema.Number("Output share of the cost. Null when no call was priced.", true),
                "llmCallCount", Schema.Integer("Model calls.", 0, int.MaxValue),
                "unpricedCallCount", Schema.Integer("Calls with no price (an endpoint without prices, or an agent CLI on a subscription), left out of the amounts.", 0, int.MaxValue),
                "failedCallCount", Schema.Integer("Failed calls.", 0, int.MaxValue),
                "inputTokens", Schema.Integer("Input tokens.", 0, int.MaxValue),
                "outputTokens", Schema.Integer("Output tokens.", 0, int.MaxValue),
                "cachedInputTokens", Schema.Integer("Input tokens read from the prompt cache.", 0, int.MaxValue),
                "cacheWriteTokens", Schema.Integer("Input tokens written to the prompt cache.", 0, int.MaxValue),
                "reasoningTokens", Schema.Integer("Reasoning tokens.", 0, int.MaxValue),
                "modelDurationMs", Schema.Number("Time spent in model calls, in milliseconds."));
        }

        #endregion
    }
}
