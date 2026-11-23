namespace MyMorningNews.McpServer.Tools
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Requests;
    using Voltaic.Mcp;

    /// <summary>
    /// Topic editing tools used by the refine_topic prompt: update the topic, and add, change, or remove dimensions and
    /// constraints.
    /// </summary>
    public static class TopicEditTools
    {
        /// <summary>
        /// Build the tools.
        /// </summary>
        /// <returns>Tools.</returns>
        public static List<McpTool> Build()
        {
            List<McpTool> tools = new List<McpTool>();
            Dictionary<string, object> dimensionSchema = Schema.Object(Schema.Props(
                "id", Schema.String("Dimension ID."), "name", Schema.String("Name."), "description", Schema.NullableString("Description."), "weight", Schema.String(Shapes.Names<DimensionWeightEnum>())),
                new[] { "id", "name" }, true);
            Dictionary<string, object> constraintSchema = Schema.Object(Schema.Props(
                "id", Schema.String("Constraint ID."), "kind", Schema.String(Shapes.Names<ConstraintKindEnum>()), "value", Schema.String("Value."), "note", Schema.NullableString("Note.")),
                new[] { "id", "kind", "value" }, true);

            tools.Add(new McpTool("update_topic", "Update a topic",
                "Change a topic's name, description, brief, status (pause with Paused, resume with Active), whether it emails after every run, or whether every run briefing is turned into audio automatically.",
                Schema.Object(Schema.Props(
                    "topicId", Schema.String("Topic ID (top_...).", 64),
                    "name", Schema.String("New name.", 200),
                    "description", Schema.String("New description.", 2000),
                    "brief", Schema.String("New research brief: what to look for and why.", 8000),
                    "status", Schema.Choice("New status.", "Active", "Paused", "Archived"),
                    "emailOnRun", Schema.Boolean("Email after every run."),
                    "audioEnabled", Schema.Boolean("Turn every briefing of the topic's research runs into audio automatically, read in the topic's voice. Uses the plan's audio minutes.")), new[] { "topicId" }),
                Shapes.TopicSchema, false, false,
                async (a, c, t) =>
                {
                    TopicStatusEnum? status = a.Choice<TopicStatusEnum>("status");
                    if (status == TopicStatusEnum.Draft) throw new McpToolException("status must be Active, Paused, or Archived.");
                    TopicUpsertRequest req = new TopicUpsertRequest
                    {
                        Name = a.OptionalText("name", 200), Description = a.OptionalText("description", 2000), Brief = a.OptionalText("brief", 8000), Status = status, EmailOnRun = a.Flag("emailOnRun"), AudioEnabled = a.Flag("audioEnabled")
                    };
                    if (req.Name == null && req.Description == null && req.Brief == null && req.Status == null && req.EmailOnRun == null && req.AudioEnabled == null) throw new McpToolException("Send at least one field to change.");
                    return Shapes.Topic(await c.Topics.UpdateAsync(a.RequiredText("topicId", 64), req, t).ConfigureAwait(false));
                }));

            tools.Add(new McpTool("set_topic_dimension", "Add or change a dimension",
                "Add a research dimension (an angle the research covers) to a topic, or change one when dimensionId is given. Weight High gets more attention; Exclude rules an angle out.",
                Schema.Object(Schema.Props(
                    "topicId", Schema.String("Topic ID (top_...).", 64),
                    "dimensionId", Schema.String("Dimension to change; omit to add one.", 64),
                    "name", Schema.String("Name, for example 'Pricing changes'.", 120),
                    "description", Schema.String("What it covers.", 1000),
                    "weight", Schema.Choice<DimensionWeightEnum>("How much it matters. Default Normal.")), new[] { "topicId" }),
                dimensionSchema, false, false,
                async (a, c, t) =>
                {
                    string topicId = a.RequiredText("topicId", 64);
                    string? dimensionId = a.OptionalText("dimensionId", 64);
                    DimensionUpsertRequest req = new DimensionUpsertRequest { Name = a.OptionalText("name", 120), Description = a.OptionalText("description", 1000), Weight = a.Choice<DimensionWeightEnum>("weight") };
                    if (dimensionId == null && req.Name == null) throw new McpToolException("name is required to add a dimension.");
                    TopicDimension d = dimensionId == null
                        ? await c.Topics.CreateDimensionAsync(topicId, req, t).ConfigureAwait(false)
                        : await c.Topics.UpdateDimensionAsync(topicId, dimensionId, req, t).ConfigureAwait(false);
                    return new Dictionary<string, object?>(StringComparer.Ordinal) { { "id", d.Id }, { "name", d.Name }, { "description", d.Description }, { "weight", d.Weight.ToString() } };
                }));

            tools.Add(new McpTool("remove_topic_dimension", "Remove a dimension",
                "Remove a research dimension from a topic.",
                Schema.Object(Schema.Props("topicId", Schema.String("Topic ID (top_...).", 64), "dimensionId", Schema.String("Dimension ID.", 64)), new[] { "topicId", "dimensionId" }),
                Schema.Object(Schema.Props("removed", Schema.Boolean("True when removed.")), new[] { "removed" }), false, true,
                async (a, c, t) =>
                {
                    await c.Topics.DeleteDimensionAsync(a.RequiredText("topicId", 64), a.RequiredText("dimensionId", 64), t).ConfigureAwait(false);
                    return new Dictionary<string, object?>(StringComparer.Ordinal) { { "removed", true } };
                }));

            tools.Add(new McpTool("add_topic_constraint", "Add a constraint",
                "Add a constraint to a topic: keywords or entities to include or exclude, preferred or blocked domains, a question to track, or a feed or seed URL.",
                Schema.Object(Schema.Props(
                    "topicId", Schema.String("Topic ID (top_...).", 64),
                    "kind", Schema.Choice<ConstraintKindEnum>("Kind of constraint."),
                    "value", Schema.String("The keyword, entity, domain, question, or URL.", 500),
                    "note", Schema.String("Why, briefly.", 500)), new[] { "topicId", "kind", "value" }),
                constraintSchema, false, false,
                async (a, c, t) =>
                {
                    ConstraintUpsertRequest req = new ConstraintUpsertRequest
                    {
                        Kind = a.Choice<ConstraintKindEnum>("kind") ?? throw new McpToolException("kind is required."), Value = a.RequiredText("value", 500), Note = a.OptionalText("note", 500)
                    };
                    TopicConstraint x = await c.Topics.CreateConstraintAsync(a.RequiredText("topicId", 64), req, t).ConfigureAwait(false);
                    return new Dictionary<string, object?>(StringComparer.Ordinal) { { "id", x.Id }, { "kind", x.Kind.ToString() }, { "value", x.Value }, { "note", x.Note } };
                }));

            tools.Add(new McpTool("remove_topic_constraint", "Remove a constraint",
                "Remove a constraint from a topic.",
                Schema.Object(Schema.Props("topicId", Schema.String("Topic ID (top_...).", 64), "constraintId", Schema.String("Constraint ID.", 64)), new[] { "topicId", "constraintId" }),
                Schema.Object(Schema.Props("removed", Schema.Boolean("True when removed.")), new[] { "removed" }), false, true,
                async (a, c, t) =>
                {
                    await c.Topics.DeleteConstraintAsync(a.RequiredText("topicId", 64), a.RequiredText("constraintId", 64), t).ConfigureAwait(false);
                    return new Dictionary<string, object?>(StringComparer.Ordinal) { { "removed", true } };
                }));

            return tools;
        }
    }
}
