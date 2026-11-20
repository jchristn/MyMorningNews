namespace MyMorningNews.McpServer.Prompts
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using MyMorningNews.McpServer.Resources;
    using MyMorningNews.McpServer.Tools;
    using MyMorningNews.Sdk.Models;
    using Voltaic.Mcp;

    /// <summary>
    /// The prompts: review the morning briefing and collect feedback, and refine a topic conversationally.
    /// </summary>
    public static class PromptCatalog
    {
        /// <summary>
        /// Build the prompts.
        /// </summary>
        /// <returns>Prompts.</returns>
        public static List<McpPromptSpec> Build()
        {
            return new List<McpPromptSpec>
            {
                new McpPromptSpec("review_morning_briefing", "Review the morning briefing",
                    "Walk through today's briefing with me, item by item, and record my ratings and reasons as feedback.",
                    new List<McpPromptArgument> { new McpPromptArgument { Name = "briefingId", Description = "Briefing to review (brf_...). Default: the newest ready briefing.", Required = false } },
                    async (a, c, t) =>
                    {
                        string? id = a.OptionalText("briefingId", 64);
                        BriefingDetailResponse? d = id == null ? await BriefingTools.LatestAsync(c, t).ConfigureAwait(false) : await c.Briefings.GetAsync(id, t).ConfigureAwait(false);
                        StringBuilder sb = new StringBuilder();
                        if (d == null)
                        {
                            sb.Append("I wanted to review my My Morning News briefing, but none is ready yet. Tell me that, then offer to list my topics (list_topics) and run one now (run_topic).");
                            return Result("Review the morning briefing", sb.ToString());
                        }

                        sb.Append("Help me review my My Morning News briefing \"").Append(d.Briefing.Title).Append("\" (").Append(d.Briefing.Id).Append(").\n\n");
                        sb.Append("Go through it one item at a time. For each finding, prediction, or suggestion: give me a one-line recap, ask whether it was relevant ");
                        sb.Append("(or, for predictions, how accurate it looks; for suggestions, whether it is useful), and ask why in a few words. ");
                        sb.Append("Record each answer with submit_feedback, using the item's type and ID from the list below and my reason in my own words. ");
                        sb.Append("If I skip an item, move on without recording anything. Use get_finding when I ask for the evidence behind a finding. ");
                        sb.Append("At the end, summarize what I liked and disliked, and mention any proposed preferences from list_learned_preferences with status Proposed.\n\n");
                        sb.Append("Items in order:\n");
                        foreach (BriefingItem i in d.Items) sb.Append("- ").Append(i.ItemType).Append(' ').Append(i.ItemId).Append(i.CitationNumber.HasValue ? " [" + i.CitationNumber.Value + "]" : String.Empty).Append('\n');
                        sb.Append("\nThe briefing:\n\n").Append(Shapes.Clip(ResourceCatalog.BriefingMarkdown(d), 40000));
                        return Result("Review the morning briefing", sb.ToString());
                    }),
                new McpPromptSpec("refine_topic", "Refine a topic",
                    "Refine one of my research topics in conversation: review its brief, dimensions, and constraints, and change them with me.",
                    new List<McpPromptArgument> { new McpPromptArgument { Name = "topicId", Description = "Topic to refine (top_...).", Required = true } },
                    async (a, c, t) =>
                    {
                        TopicDetailResponse d = await c.Topics.GetAsync(a.RequiredText("topicId", 64), t).ConfigureAwait(false);
                        StringBuilder sb = new StringBuilder();
                        sb.Append("Help me refine my My Morning News research topic \"").Append(d.Topic.Name).Append("\" (").Append(d.Topic.Id).Append(").\n\n");
                        sb.Append("First, in a few sentences, tell me what the topic currently covers and point out gaps, overlaps, or vague parts. ");
                        sb.Append("Then ask me, one question at a time, what I want more of, less of, or to stop seeing. ");
                        sb.Append("Propose concrete changes and apply them only after I agree: update_topic for the name, description, or brief; ");
                        sb.Append("set_topic_dimension and remove_topic_dimension for dimensions; add_topic_constraint and remove_topic_constraint for keywords, entities, domains, tracked questions, and feeds. ");
                        sb.Append("Keep the brief focused on research and news monitoring. When we are done, offer a dry run with run_topic (dryRun true) to check the result.\n\n");
                        sb.Append(ResourceCatalog.TopicMarkdown(d));
                        return Result("Refine a topic", sb.ToString());
                    })
            };
        }

        private static McpGetPromptResult Result(string description, string text)
        {
            return new McpGetPromptResult
            {
                Description = description,
                Messages = new List<McpPromptMessage> { new McpPromptMessage { Role = "user", Content = new McpTextContent { Text = text } } }
            };
        }
    }
}
