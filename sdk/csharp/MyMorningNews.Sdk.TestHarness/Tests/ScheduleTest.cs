namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Schedules: preview (valid and invalid), create a disabled schedule, read, update, list, delete, 404 after delete.
    /// </summary>
    public class ScheduleTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Schedules CRUD and preview";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ScheduleTest()
        {
        }

        /// <summary>
        /// Run the case.
        /// </summary>
        /// <param name="context">Shared state.</param>
        /// <param name="result">Result.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public override async Task RunAsync(TestContext context, TestResult result, CancellationToken token)
        {
            MyMorningNewsClient c = context.RequireAdmin();
            SchedulePreviewResponse preview = await StepAsync(context, result, "POST schedules/preview", () => c.Schedules.PreviewAsync(new ScheduleUpsertRequest
            {
                IntervalType = IntervalTypeEnum.DaysOfWeek,
                DaysOfWeekMask = 62,
                TimesOfDay = new List<string> { "07:00" },
                TimeZone = "America/New_York",
                PreviewCount = 3
            }, token)).ConfigureAwait(false);
            Helpers.True(preview.Valid, "weekday schedule is valid");
            Helpers.Equal(3, preview.NextRunsUtc.Count, "preview count");

            SchedulePreviewResponse invalid = await StepAsync(context, result, "POST schedules/preview (invalid cron)", () => c.Schedules.PreviewAsync(new ScheduleUpsertRequest
            {
                IntervalType = IntervalTypeEnum.Cron,
                CronExpression = "not a cron"
            }, token)).ConfigureAwait(false);
            Helpers.True(!invalid.Valid && invalid.Errors.Count > 0, "invalid cron is reported");

            Topic topic = await StepAsync(context, result, "POST topic", () => Fixtures.CreateTopicAsync(context, c, "schedule", token)).ConfigureAwait(false);
            await StepAsync(context, result, "POST schedule without intervalType (400)", () => Helpers.ExpectErrorAsync(() => c.Schedules.CreateAsync(new ScheduleUpsertRequest { OwnerId = topic.Id }, token), 400)).ConfigureAwait(false);
            Schedule s = await StepAsync(context, result, "POST schedule (disabled)", () => c.Schedules.CreateAsync(new ScheduleUpsertRequest
            {
                OwnerType = ScheduleOwnerTypeEnum.Topic,
                OwnerId = topic.Id,
                Name = context.Name("schedule"),
                IntervalType = IntervalTypeEnum.EveryNDays,
                IntervalValue = 1,
                TimesOfDay = new List<string> { "06:30" },
                Enabled = false
            }, token)).ConfigureAwait(false);
            context.Cleanup.Add("schedule:" + s.Id, ct => c.Schedules.DeleteAsync(s.Id, ct));
            Helpers.True(!s.Enabled, "schedule is disabled");

            Schedule read = await StepAsync(context, result, "GET schedule", () => c.Schedules.GetAsync(s.Id, token)).ConfigureAwait(false);
            Helpers.Equal(topic.Id, read.OwnerId, "schedule owner");
            Schedule updated = await StepAsync(context, result, "PUT schedule", () => c.Schedules.UpdateAsync(s.Id, new ScheduleUpsertRequest { IntervalValue = 2 }, token)).ConfigureAwait(false);
            Helpers.Equal(2, updated.IntervalValue, "interval value");
            EnumerationResult<Schedule> list = await StepAsync(context, result, "GET schedules (owner filter)",
                () => c.Schedules.ListAsync(new ScheduleListOptions { OwnerType = ScheduleOwnerTypeEnum.Topic, OwnerId = topic.Id }, token)).ConfigureAwait(false);
            Helpers.Contains(list.Objects, x => x.Id == s.Id, "the schedule");

            await StepAsync(context, result, "DELETE schedule", () => c.Schedules.DeleteAsync(s.Id, token)).ConfigureAwait(false);
            context.Cleanup.Remove("schedule:" + s.Id);
            await StepAsync(context, result, "GET deleted schedule (404)", () => Helpers.ExpectErrorAsync(() => c.Schedules.GetAsync(s.Id, token), 404)).ConfigureAwait(false);
            await StepAsync(context, result, "DELETE topic", () => Fixtures.DeleteTopicAsync(context, c, topic.Id, token)).ConfigureAwait(false);
        }
    }
}
