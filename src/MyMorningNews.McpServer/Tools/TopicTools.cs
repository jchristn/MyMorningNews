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
    using MyMorningNews.Sdk.Requests;
    using Voltaic.Mcp;

    /// <summary>
    /// Topic and run tools: list, read, create from a description, run now, and read a run.
    /// </summary>
    public static class TopicTools
    {
        /// <summary>
        /// Build the tools.
        /// </summary>
        /// <returns>Tools.</returns>
        public static List<McpTool> Build()
        {
            List<McpTool> tools = new List<McpTool>();

            tools.Add(new McpTool("list_topics", "List topics",
                "List your research topics (newest first), with status and last run time. Use the topic ID with the other tools.",
                Schema.Object(Schema.Props(
                    "status", Schema.Choice<TopicStatusEnum>("Only topics in this status."),
                    "search", Schema.String("Text in the name or description.", 200),
                    "pageSize", Schema.Integer("Topics per page. Default 25.", 1, 100),
                    "pageNumber", Schema.Integer("Page (1-based). Default 1.", 1, 10000))),
                Schema.Page(Shapes.TopicSchema), true, false,
                async (a, c, t) =>
                {
                    TopicListOptions o = new TopicListOptions { Status = a.Choice<TopicStatusEnum>("status"), Search = a.OptionalText("search", 200), PageSize = a.Integer("pageSize", 25, 1, 100), PageNumber = a.Integer("pageNumber", 1, 1, 10000), Mine = true };
                    return Shapes.Page(await c.Topics.ListAsync(o, t).ConfigureAwait(false), Shapes.Topic);
                }));

            tools.Add(new McpTool("get_topic", "Read a topic",
                "Read a topic with its brief, dimensions (the angles researched), constraints, and email recipients.",
                Schema.Object(Schema.Props("topicId", Schema.String("Topic ID (top_...).", 64)), new[] { "topicId" }),
                Schema.Object(Schema.Props(
                    "topic", Shapes.TopicSchema,
                    "brief", Schema.NullableString("The research brief."),
                    "dimensions", Schema.Array("Research dimensions.", Schema.Record("Dimension: id, name, description, weight.")),
                    "constraints", Schema.Array("Constraints.", Schema.Record("Constraint: id, kind, value.")),
                    "recipients", Schema.Array("Email recipients.", Schema.String("Address."))), new[] { "topic" }, true),
                true, false,
                async (a, c, t) =>
                {
                    TopicDetailResponse d = await c.Topics.GetAsync(a.RequiredText("topicId", 64), t).ConfigureAwait(false);
                    return new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        { "topic", Shapes.Topic(d.Topic) },
                        { "brief", d.Topic.Brief },
                        { "dimensions", d.Dimensions.OrderBy(x => x.SortOrder).Select(x => new Dictionary<string, object?> { { "id", x.Id }, { "name", x.Name }, { "description", x.Description }, { "weight", x.Weight.ToString() } }).ToList() },
                        { "constraints", d.Constraints.Select(x => new Dictionary<string, object?> { { "id", x.Id }, { "kind", x.Kind.ToString() }, { "value", x.Value } }).ToList() },
                        { "recipients", d.Recipients.Select(x => x.Email).ToList() }
                    };
                }));

            tools.Add(new McpTool("create_topic", "Create a topic from a description",
                "Create and activate a research topic from a plain description, using the refinement wizard's defaults without asking questions: "
                + "it proposes dimensions and drafts the brief and constraints, then activates the topic. This makes model calls on your account. "
                + "Use refine_topic (a prompt) afterwards to adjust it conversationally.",
                Schema.Object(Schema.Props(
                    "description", Schema.String("What to research, in your own words (10 to 2000 characters).", 2000),
                    "goal", Schema.String("What you want to get out of it, for example 'decide whether to buy'.", 500),
                    "depth", Schema.Choice("How deep to go.", "overview", "balanced", "deep"),
                    "horizon", Schema.String("Time horizon, for example 'next 6 months'.", 200)), new[] { "description" }),
                Schema.Object(Schema.Props(
                    "topic", Shapes.TopicSchema,
                    "brief", Schema.NullableString("The drafted brief."),
                    "dimensions", Schema.Array("Dimension names.", Schema.String("Name."))), new[] { "topic" }, true),
                false, false,
                async (a, c, t) =>
                {
                    string description = a.RequiredText("description", 2000);
                    if (description.Length < 10) throw new McpToolException("description must be at least 10 characters.");
                    RefinementStartRequest start = new RefinementStartRequest { SeedText = description, Goal = a.OptionalText("goal", 500), Depth = a.Text("depth"), Horizon = a.OptionalText("horizon", 200) };
                    RefinementStateResponse s = await c.Refinement.StartAsync(start, t).ConfigureAwait(false);
                    string sessionId = s.Session.Id;
                    try
                    {
                        s = await c.Refinement.GenerateDimensionsAsync(sessionId, null, t).ConfigureAwait(false);
                        s = await c.Refinement.GenerateContextAsync(sessionId, t).ConfigureAwait(false);
                        s = await c.Refinement.CompleteAsync(sessionId, t).ConfigureAwait(false);
                    }
                    catch (MyMorningNewsException)
                    {
                        try
                        {
                            await c.Refinement.AbandonAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
                        }
                        catch (MyMorningNewsException)
                        {
                        }

                        throw;
                    }

                    return new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        { "topic", Shapes.Topic(s.Topic) }, { "brief", s.Topic.Brief }, { "dimensions", s.Dimensions.OrderBy(x => x.SortOrder).Select(x => x.Name).ToList() }
                    };
                }));

            tools.Add(new McpTool("run_topic", "Research a topic now",
                "Queue a research run for a topic now. Set wait to have the tool wait for the run to finish (up to waitSeconds) and return its result. "
                + "A run searches, reads sources, verifies quotes, ranks findings, and may email the topic's recipients. It makes model calls on your account.",
                Schema.Object(Schema.Props(
                    "topicId", Schema.String("Topic ID (top_...).", 64),
                    "dryRun", Schema.Boolean("Research without saving findings for delivery or sending email. Default false."),
                    "wait", Schema.Boolean("Wait for the run to finish. Default false."),
                    "waitSeconds", Schema.Integer("How long to wait when wait is true. Default 300.", 10, 900)), new[] { "topicId" }),
                Schema.Object(Schema.Props("run", Shapes.RunSchema, "finished", Schema.Boolean("True when the run reached a final status.")), new[] { "run", "finished" }, true),
                false, false,
                async (a, c, t) =>
                {
                    Run run = await c.Topics.RunAsync(a.RequiredText("topicId", 64), a.Flag("dryRun") == true, t).ConfigureAwait(false);
                    bool finished = false;
                    if (a.Flag("wait") == true)
                    {
                        try
                        {
                            run = await c.Runs.WaitAsync(run.Id, TimeSpan.FromSeconds(a.Integer("waitSeconds", 300, 10, 900)), TimeSpan.FromSeconds(3), t).ConfigureAwait(false);
                            finished = true;
                        }
                        catch (TimeoutException)
                        {
                            run = await c.Runs.GetAsync(run.Id, t).ConfigureAwait(false);
                        }
                    }

                    return new Dictionary<string, object?>(StringComparer.Ordinal) { { "run", Shapes.Run(run) }, { "finished", finished || IsFinal(run.Status) } };
                }));

            tools.Add(new McpTool("get_run", "Read a run",
                "Read a research run: status, counts, cost, and summary, with each pipeline stage's status.",
                Schema.Object(Schema.Props("runId", Schema.String("Run ID (run_...).", 64)), new[] { "runId" }),
                Schema.Object(Schema.Props(
                    "run", Shapes.RunSchema,
                    "stages", Schema.Array("Pipeline stages in order.", Schema.Record("Stage: stage, status, itemsIn, itemsOut, durationMs, error."))), new[] { "run" }, true),
                true, false,
                async (a, c, t) =>
                {
                    string id = a.RequiredText("runId", 64);
                    Run run = await c.Runs.GetAsync(id, t).ConfigureAwait(false);
                    List<RunStage> stages = await c.Runs.ListStagesAsync(id, t).ConfigureAwait(false);
                    return new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        { "run", Shapes.Run(run) },
                        { "stages", stages.OrderBy(s => s.SortOrder).Select(s => new Dictionary<string, object?>
                            {
                                { "stage", s.Stage }, { "status", s.Status.ToString() }, { "itemsIn", s.ItemsIn }, { "itemsOut", s.ItemsOut }, { "durationMs", Math.Round(s.DurationMs, 0) }, { "error", s.ErrorMessage }
                            }).ToList() }
                    };
                }));

            return tools;
        }

        private static bool IsFinal(RunStatusEnum status)
        {
            return status == RunStatusEnum.Completed || status == RunStatusEnum.PartiallySucceeded || status == RunStatusEnum.Failed || status == RunStatusEnum.Cancelled || status == RunStatusEnum.Skipped;
        }
    }
}
