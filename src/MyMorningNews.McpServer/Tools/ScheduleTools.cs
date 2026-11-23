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
    /// Schedule tools: list the schedules of a topic or an edition, add or change one, and remove one. A topic schedule
    /// starts research runs; an edition schedule composes the edition's briefing.
    /// </summary>
    public static class ScheduleTools
    {
        /// <summary>
        /// Build the tools.
        /// </summary>
        /// <returns>Tools.</returns>
        public static List<McpTool> Build()
        {
            List<McpTool> tools = new List<McpTool>();

            tools.Add(new McpTool("list_schedules", "List schedules",
                "List your schedules, or only those of one topic or one edition, with what each does and when it runs next. A topic schedule starts research runs; an edition schedule composes the edition's briefing.",
                Schema.Object(Schema.Props(
                    "topicId", Schema.String("Only this topic's schedules (top_...).", 64),
                    "editionId", Schema.String("Only this edition's schedules (edn_...).", 64),
                    "pageSize", Schema.Integer("Schedules per page. Default 25.", 1, 100),
                    "pageNumber", Schema.Integer("Page (1-based). Default 1.", 1, 10000))),
                Schema.Page(Shapes.ScheduleSchema), true, false,
                async (a, c, t) =>
                {
                    string? topicId = a.OptionalText("topicId", 64);
                    string? editionId = a.OptionalText("editionId", 64);
                    if (topicId != null && editionId != null) throw new McpToolException("Send topicId or editionId, not both.");
                    ScheduleListOptions o = new ScheduleListOptions { PageSize = a.Integer("pageSize", 25, 1, 100), PageNumber = a.Integer("pageNumber", 1, 1, 10000), Mine = true };
                    if (topicId != null || editionId != null)
                    {
                        o.OwnerType = topicId != null ? ScheduleOwnerTypeEnum.Topic : ScheduleOwnerTypeEnum.Edition;
                        o.OwnerId = topicId ?? editionId;
                    }

                    return Shapes.Page(await c.Schedules.ListAsync(o, t).ConfigureAwait(false), Shapes.Schedule);
                }));

            tools.Add(new McpTool("set_schedule", "Add or change a schedule",
                "Add a schedule to a topic (topicId) or an edition (editionId), or change one (scheduleId; only the fields sent change). "
                + "Pick intervalType and send the fields it uses: DaysOfWeek (daysOfWeek, timesOfDay), EveryNDays (intervalValue, timesOfDay), "
                + "EveryNWeeks (intervalValue, daysOfWeek, timesOfDay), DaysOfMonth (daysOfMonth, timesOfDay), EveryNHours (intervalValue, minuteOffset), "
                + "EveryNMinutes (intervalValue), Once (runOnceUtc), Cron (cronExpression), or Manual (never fires by itself). "
                + "Times of day are local to timeZone. The result's nextRunUtc shows when it fires next. The plan may limit how often a topic runs.",
                Schema.Object(Schema.Props(
                    "scheduleId", Schema.String("Schedule to change (sch_...); omit to add one.", 64),
                    "topicId", Schema.String("Topic to schedule (top_...), when adding.", 64),
                    "editionId", Schema.String("Edition to schedule (edn_...), when adding.", 64),
                    "name", Schema.String("Label, for example 'Weekday mornings'.", 200),
                    "intervalType", Schema.Choice<IntervalTypeEnum>("How it repeats. Required when adding."),
                    "intervalValue", Schema.Integer("Every how many minutes, hours, days, or weeks.", 1, 1440),
                    "minuteOffset", Schema.Integer("Minute past the hour, for EveryNHours.", 0, 59),
                    "daysOfWeek", Schema.Array("Days it runs on, for DaysOfWeek and EveryNWeeks.", Schema.Choice<DayOfWeek>("Day."), 7),
                    "daysOfMonth", Schema.Array("Days of the month, for DaysOfMonth: 1 to 31, or -1 for the last day.", Schema.Integer("Day.", -1, 31), 32),
                    "timesOfDay", Schema.Array("Local times of day as 24-hour HH:mm, for example 07:00.", Schema.String("HH:mm.", 5), 24),
                    "cronExpression", Schema.String("Five-field cron expression, for Cron.", 200),
                    "timeZone", Schema.String("IANA time zone the times are in, for example America/New_York. Default UTC.", 64),
                    "runOnceUtc", Schema.String("When to run, for Once (ISO 8601, UTC).", 40),
                    "startUtc", Schema.String("Do not run before this time (ISO 8601, UTC).", 40),
                    "endUtc", Schema.String("Do not run after this time (ISO 8601, UTC).", 40),
                    "enabled", Schema.Boolean("False pauses the schedule without removing it. Default true."),
                    "missedRunPolicy", Schema.Choice<MissedRunPolicyEnum>("What to do with a run missed while the server was down. Default RunOnceOnRecovery."))),
                Shapes.ScheduleSchema, false, false,
                async (a, c, t) =>
                {
                    string? scheduleId = a.OptionalText("scheduleId", 64);
                    string? topicId = a.OptionalText("topicId", 64);
                    string? editionId = a.OptionalText("editionId", 64);
                    List<string> days = a.TextList("daysOfWeek", 7);
                    List<string> times = a.TextList("timesOfDay", 24);
                    ScheduleUpsertRequest req = new ScheduleUpsertRequest
                    {
                        Name = a.OptionalText("name", 200),
                        IntervalType = a.Choice<IntervalTypeEnum>("intervalType"),
                        IntervalValue = a.OptionalInteger("intervalValue", 1, 1440),
                        MinuteOffset = a.OptionalInteger("minuteOffset", 0, 59),
                        DaysOfWeekMask = days.Count > 0 ? DayMask(days) : null,
                        DaysOfMonth = a.IntegerList("daysOfMonth", -1, 31, 32),
                        TimesOfDay = times.Count > 0 ? times : null,
                        CronExpression = a.OptionalText("cronExpression", 200),
                        TimeZone = a.OptionalText("timeZone", 64),
                        RunOnceUtc = a.Date("runOnceUtc"),
                        StartUtc = a.Date("startUtc"),
                        EndUtc = a.Date("endUtc"),
                        Enabled = a.Flag("enabled"),
                        MissedRunPolicy = a.Choice<MissedRunPolicyEnum>("missedRunPolicy")
                    };
                    if (req.DaysOfMonth != null && req.DaysOfMonth.Contains(0)) throw new McpToolException("daysOfMonth holds 1 to 31, or -1 for the last day.");
                    if (scheduleId != null)
                    {
                        if (topicId != null || editionId != null) throw new McpToolException("A schedule stays with its topic or edition; send scheduleId without topicId or editionId.");
                        return Shapes.Schedule(await c.Schedules.UpdateAsync(scheduleId, req, t).ConfigureAwait(false));
                    }

                    if ((topicId == null) == (editionId == null)) throw new McpToolException("Send topicId or editionId to add a schedule, or scheduleId to change one.");
                    if (req.IntervalType == null) throw new McpToolException("intervalType is required to add a schedule.");
                    req.OwnerType = topicId != null ? ScheduleOwnerTypeEnum.Topic : ScheduleOwnerTypeEnum.Edition;
                    req.OwnerId = topicId ?? editionId;
                    return Shapes.Schedule(await c.Schedules.CreateAsync(req, t).ConfigureAwait(false));
                }));

            tools.Add(new McpTool("remove_schedule", "Remove a schedule",
                "Remove a schedule. The topic or edition stays and can still be run by hand. To pause instead, use set_schedule with enabled false.",
                Schema.Object(Schema.Props("scheduleId", Schema.String("Schedule ID (sch_...).", 64)), new[] { "scheduleId" }),
                Schema.Object(Schema.Props("removed", Schema.Boolean("True when removed.")), new[] { "removed" }), false, true,
                async (a, c, t) =>
                {
                    await c.Schedules.DeleteAsync(a.RequiredText("scheduleId", 64), t).ConfigureAwait(false);
                    return new Dictionary<string, object?>(StringComparer.Ordinal) { { "removed", true } };
                }));

            return tools;
        }

        private static int DayMask(List<string> days)
        {
            int mask = 0;
            foreach (string day in days)
            {
                if (!Enum.TryParse<DayOfWeek>(day, true, out DayOfWeek parsed) || !Enum.IsDefined(parsed))
                    throw new McpToolException("daysOfWeek must hold day names: " + String.Join(", ", Enum.GetNames<DayOfWeek>()) + ".");
                mask |= 1 << (int)parsed;
            }

            return mask;
        }
    }
}
