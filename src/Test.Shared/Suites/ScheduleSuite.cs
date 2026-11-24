namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Pipeline;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Scheduling;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Server;
    using SyslogLogging;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Schedule calculation (including daylight-saving transitions), schedules API, the scheduler, and the run queue.
    /// </summary>
    public static class ScheduleSuite
    {
        private const string S = "Schedules";

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _Tenants = new System.Collections.Concurrent.ConcurrentDictionary<string, bool>(StringComparer.Ordinal);

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _Topics = new System.Collections.Concurrent.ConcurrentDictionary<string, bool>(StringComparer.Ordinal);

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Schedules, scheduler, and runs", new[]
            {
                Cases.Case(S, "Calc.Intervals", "Minutes, hours, days, weeks, days of month, once, and cron intervals compute the right times", IntervalsAsync, "fast"),
                Cases.Case(S, "Calc.DaylightSaving", "A nonexistent local time moves forward and an ambiguous one fires once", DstAsync, "fast"),
                Cases.Case(S, "Calc.Validation", "Invalid schedules report every problem and values are normalized", ValidationAsync, "fast"),
                Cases.Case(S, "Api.Schedules", "Schedules are created, previewed, updated, and private to the topic owner", ApiAsync),
                Cases.Case(S, "Scheduler.Tick", "Due schedules queue one run, overlapping and paused cases are skipped, missed runs follow the policy", TickAsync),
                Cases.Case(S, "Runs.Execute", "Queued runs execute, fail, time out, and respect the concurrency limit", ExecuteAsync),
                Cases.Case(S, "Runs.Signal", "A work signal collapses repeats, is kept until waited on, and times out when unset", WorkSignalAsync, "fast"),
                Cases.Case(S, "Runs.NoPollWait", "Queued runs start when queued and when a slot frees, without waiting for the poll, and never pass the concurrency limit even with competing claimers", NoPollWaitAsync),
                Cases.Case(S, "Runs.Api", "Run now, dry run, conflicts, and cancel work through the API", RunApiAsync),
                Cases.Case(S, "Requests.Due", "A confirmed one-time request is queued when due and completes when delivered", RequestDueAsync)
            });
        }

        private static Schedule Make(IntervalTypeEnum type, string tz = "UTC", params string[] times)
        {
            return new Schedule { IntervalType = type, TimeZone = tz, TimesOfDay = times.ToList(), CreatedUtc = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc) };
        }

        private static DateTime U(int y, int mo, int d, int h, int mi)
        {
            return new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Utc);
        }

        private static Task IntervalsAsync(CancellationToken token)
        {
            Schedule m = Make(IntervalTypeEnum.EveryNMinutes);
            m.IntervalValue = 15;
            Assert.Equal(U(2026, 11, 19, 10, 15), ScheduleCalculator.NextOccurrence(m, U(2026, 11, 19, 10, 7))!.Value, "every 15 minutes aligns to the anchor");
            Assert.Equal(U(2026, 11, 19, 10, 30), ScheduleCalculator.NextOccurrence(m, U(2026, 11, 19, 10, 15))!.Value, "strictly after");

            Schedule h = Make(IntervalTypeEnum.EveryNHours);
            h.IntervalValue = 4;
            h.MinuteOffset = 15;
            List<DateTime> hours = ScheduleCalculator.NextOccurrences(h, U(2026, 11, 19, 9, 0), 3);
            Assert.True(hours.SequenceEqual(new[] { U(2026, 11, 19, 12, 15), U(2026, 11, 19, 16, 15), U(2026, 11, 19, 20, 15) }), "every 4 hours at :15: " + String.Join(", ", hours));

            Schedule wk = Make(IntervalTypeEnum.DaysOfWeek, "America/Los_Angeles", "06:30");
            wk.DaysOfWeekMask = 62;
            Assert.Equal(U(2026, 11, 23, 14, 30), ScheduleCalculator.NextOccurrence(wk, U(2026, 11, 20, 15, 0))!.Value, "weekdays at 06:30 Pacific skip the weekend");

            Schedule days = Make(IntervalTypeEnum.EveryNDays, "UTC", "18:00", "07:00");
            days.IntervalValue = 2;
            ScheduleCalculator.Validate(days, 5);
            List<DateTime> d = ScheduleCalculator.NextOccurrences(days, U(2026, 11, 1, 8, 0), 3);
            Assert.True(d.SequenceEqual(new[] { U(2026, 11, 1, 18, 0), U(2026, 11, 3, 7, 0), U(2026, 11, 3, 18, 0) }), "every 2 days at two times: " + String.Join(", ", d));

            Schedule biweekly = Make(IntervalTypeEnum.EveryNWeeks, "UTC", "08:00");
            biweekly.IntervalValue = 2;
            biweekly.DaysOfWeekMask = (1 << (int)DayOfWeek.Monday) | (1 << (int)DayOfWeek.Thursday);
            List<DateTime> w = ScheduleCalculator.NextOccurrences(biweekly, U(2026, 11, 1, 0, 0), 4);
            Assert.True(w.SequenceEqual(new[] { U(2026, 11, 2, 8, 0), U(2026, 11, 5, 8, 0), U(2026, 11, 16, 8, 0), U(2026, 11, 19, 8, 0) }), "every 2 weeks Monday and Thursday: " + String.Join(", ", w));

            Schedule month = Make(IntervalTypeEnum.DaysOfMonth, "UTC", "09:00");
            month.DaysOfMonth = new List<int> { 15, -1 };
            List<DateTime> mo = ScheduleCalculator.NextOccurrences(month, U(2027, 2, 1, 0, 0), 3);
            Assert.True(mo.SequenceEqual(new[] { U(2027, 2, 15, 9, 0), U(2027, 2, 28, 9, 0), U(2027, 3, 15, 9, 0) }), "15th and last day: " + String.Join(", ", mo));

            Schedule once = Make(IntervalTypeEnum.Once);
            once.RunOnceUtc = U(2026, 12, 1, 10, 0);
            Assert.Equal(U(2026, 12, 1, 10, 0), ScheduleCalculator.NextOccurrence(once, U(2026, 11, 19, 0, 0))!.Value, "once in the future");
            Assert.True(ScheduleCalculator.NextOccurrence(once, U(2026, 12, 1, 10, 0)) == null, "once only once");

            Schedule cron = Make(IntervalTypeEnum.Cron);
            cron.CronExpression = "0 */6 * * 1-5";
            Assert.Equal(U(2026, 11, 23, 0, 0), ScheduleCalculator.NextOccurrence(cron, U(2026, 11, 20, 18, 30))!.Value, "cron skips the weekend");

            Schedule ended = Make(IntervalTypeEnum.DaysOfWeek, "UTC", "06:00");
            ended.DaysOfWeekMask = 127;
            ended.EndUtc = U(2026, 11, 20, 0, 0);
            Assert.True(ScheduleCalculator.NextOccurrence(ended, U(2026, 11, 20, 6, 0)) == null, "end date stops the schedule");
            Assert.True(ScheduleCalculator.NextOccurrence(Make(IntervalTypeEnum.Manual), U(2026, 11, 20, 6, 0)) == null, "manual never fires");
            return Task.CompletedTask;
        }

        private static Task DstAsync(CancellationToken token)
        {
            Schedule spring = Make(IntervalTypeEnum.DaysOfWeek, "America/Los_Angeles", "02:30");
            spring.DaysOfWeekMask = 1 << (int)DayOfWeek.Sunday;
            Assert.Equal(U(2026, 3, 8, 10, 30), ScheduleCalculator.NextOccurrence(spring, U(2026, 3, 7, 0, 0))!.Value, "02:30 does not exist on 2026-03-08 and moves to 03:30 PDT");

            Schedule fall = Make(IntervalTypeEnum.DaysOfWeek, "America/Los_Angeles", "01:30");
            fall.DaysOfWeekMask = 1 << (int)DayOfWeek.Sunday;
            DateTime first = ScheduleCalculator.NextOccurrence(fall, U(2026, 10, 31, 0, 0))!.Value;
            Assert.Equal(U(2026, 11, 1, 8, 30), first, "ambiguous 01:30 fires at the first occurrence (PDT)");
            Assert.Equal(U(2026, 11, 8, 9, 30), ScheduleCalculator.NextOccurrence(fall, first)!.Value, "and not again an hour later");

            Schedule daily = Make(IntervalTypeEnum.EveryNDays, "Europe/London", "06:30");
            daily.IntervalValue = 1;
            daily.CreatedUtc = U(2026, 1, 1, 0, 0);
            List<DateTime> london = ScheduleCalculator.NextOccurrences(daily, U(2026, 3, 28, 12, 0), 2);
            Assert.True(london.SequenceEqual(new[] { U(2026, 3, 29, 5, 30), U(2026, 3, 30, 5, 30) }), "06:30 local stays 06:30 across the UK change: " + String.Join(", ", london));
            return Task.CompletedTask;
        }

        private static Task ValidationAsync(CancellationToken token)
        {
            Schedule bad = Make(IntervalTypeEnum.DaysOfWeek, "Mars/Olympus", "25:00");
            bad.DaysOfWeekMask = 0;
            List<string> errors = ScheduleCalculator.Validate(bad, 5);
            Assert.True(errors.Count >= 3 && errors.Any(e => e.Contains("time zone", StringComparison.Ordinal)) && errors.Any(e => e.Contains("HH:mm", StringComparison.Ordinal)) && errors.Any(e => e.Contains("daysOfWeekMask", StringComparison.Ordinal)),
                "every problem listed: " + String.Join(" | ", errors));
            Schedule cron = Make(IntervalTypeEnum.Cron);
            cron.CronExpression = "not cron";
            Assert.True(ScheduleCalculator.Validate(cron, 5).Any(e => e.Contains("cronExpression", StringComparison.Ordinal)), "cron validated");
            Assert.True(ScheduleCalculator.Validate(Make(IntervalTypeEnum.Once), 5).Any(e => e.Contains("runOnceUtc", StringComparison.Ordinal)), "once needs a time");
            Schedule fast = Make(IntervalTypeEnum.EveryNMinutes);
            fast.IntervalValue = 1;
            Assert.True(ScheduleCalculator.Validate(fast, 5).Count == 0 && fast.IntervalValue == 5, "interval clamped to the minimum");
            Schedule times = Make(IntervalTypeEnum.DaysOfWeek, "UTC", "7:05", "07:05", " 18:00 ");
            Assert.True(ScheduleCalculator.Validate(times, 5).Count == 0 && times.TimesOfDay.SequenceEqual(new[] { "07:05", "18:00" }), "times normalized");
            return Task.CompletedTask;
        }

        private static async Task<TopicFixture> ActiveTopicAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser u = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            Topic topic = (await u.Client.PostAsync("/v1.0/api/topics", new { name = "Sched " + Guid.NewGuid().ToString("N").Substring(0, 6), brief = "Brief.", status = "Active" }, token).ConfigureAwait(false))
                .Expect(201, "topic").Json<Topic>();
            _Topics[topic.Id] = true;
            _Tenants[t.Id] = true;
            return new TopicFixture { User = u, Topic = topic };
        }

        private static async Task ApiAsync(CancellationToken token)
        {
            TopicFixture fx1 = await ActiveTopicAsync(token).ConfigureAwait(false);
            TestUser u = fx1.User;
            Topic topic = fx1.Topic;
            SchedulePreviewResponse invalid = (await u.Client.PostAsync("/v1.0/api/schedules/preview", new { intervalType = "DaysOfWeek", daysOfWeekMask = 0, timeZone = "Nowhere/City" }, token).ConfigureAwait(false))
                .Expect(200, "preview invalid").Json<SchedulePreviewResponse>();
            Assert.True(!invalid.Valid && invalid.Errors.Count >= 2 && invalid.NextRunsUtc.Count == 0, "invalid preview");
            SchedulePreviewResponse preview = (await u.Client.PostAsync("/v1.0/api/schedules/preview", new { intervalType = "DaysOfWeek", daysOfWeekMask = 62, timesOfDay = new[] { "06:30" }, timeZone = "America/New_York", previewCount = 5 }, token).ConfigureAwait(false))
                .Expect(200, "preview").Json<SchedulePreviewResponse>();
            Assert.True(preview.Valid && preview.NextRunsUtc.Count == 5 && preview.NextRunsLocal.All(l => l.Contains("T06:30:00", StringComparison.Ordinal)), "five weekday runs at 06:30 local: " + String.Join(", ", preview.NextRunsLocal));

            (await u.Client.PostAsync("/v1.0/api/schedules", new { ownerId = topic.Id, intervalType = "DaysOfWeek", daysOfWeekMask = 0 }, token).ConfigureAwait(false)).Expect(400, "invalid schedule rejected");
            Schedule s = (await u.Client.PostAsync("/v1.0/api/schedules", new { ownerId = topic.Id, intervalType = "EveryNMinutes", intervalValue = 1 }, token).ConfigureAwait(false))
                .Expect(201, "create").Json<Schedule>();
            Assert.True(s.IntervalValue == 5 && s.NextRunUtc.HasValue && s.NextRunUtc.Value > DateTime.UtcNow && s.UserId == u.User.Id, "clamped and next run computed");
            Schedule upd = (await u.Client.PutAsync("/v1.0/api/schedules/" + s.Id, new { enabled = false }, token).ConfigureAwait(false)).Expect(200, "disable").Json<Schedule>();
            Assert.True(!upd.Enabled && upd.NextRunUtc == null, "disabled schedules have no next run");
            EnumerationResult<Schedule> list = (await u.Client.GetAsync("/v1.0/api/schedules?ownerId=" + topic.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Schedule>>();
            Assert.Equal(1, list.Objects.Count, "listed by owner");

            TestUser other = await TestFixtures.CreateUserAsync(await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false), topic.TenantId, false, token).ConfigureAwait(false);
            (await other.Client.PostAsync("/v1.0/api/schedules", new { ownerId = topic.Id, intervalType = "Manual" }, token).ConfigureAwait(false)).Expect(404, "cannot schedule another user's topic");
            (await other.Client.GetAsync("/v1.0/api/schedules/" + s.Id, token).ConfigureAwait(false)).Expect(404, "cannot read another user's schedule");
            (await u.Client.DeleteAsync("/v1.0/api/schedules/" + s.Id, token).ConfigureAwait(false)).Expect(204, "delete");
        }

        private static async Task<Schedule> DueScheduleAsync(MyMorningNewsServer server, TestUser u, Topic topic, DateTime dueUtc, MissedRunPolicyEnum policy, CancellationToken token)
        {
            Schedule s = (await u.Client.PostAsync("/v1.0/api/schedules", new { ownerId = topic.Id, intervalType = "EveryNMinutes", intervalValue = 5, missedRunPolicy = policy.ToString() }, token).ConfigureAwait(false))
                .Expect(201, "schedule").Json<Schedule>();
            Schedule stored = (await server.Context.Database.Schedules.ReadAsync(s.TenantId, s.Id, token).ConfigureAwait(false))!;
            stored.NextRunUtc = dueUtc;
            return await server.Context.Database.Schedules.UpdateAsync(stored, token).ConfigureAwait(false);
        }

        private static async Task<List<Run>> RunsAsync(MyMorningNewsServer server, Topic topic, CancellationToken token)
        {
            return (await server.Context.Database.Runs.EnumerateAsync(new RunFilter { TenantId = topic.TenantId, TopicId = topic.Id, PageSize = 100 }, token).ConfigureAwait(false)).Objects;
        }

        private static async Task TickAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TopicFixture fx2 = await ActiveTopicAsync(token).ConfigureAwait(false);
            TestUser u = fx2.User;
            Topic topic = fx2.Topic;
            DateTime now = DateTime.UtcNow;
            Schedule s = await DueScheduleAsync(server, u, topic, now.AddMinutes(-1), MissedRunPolicyEnum.RunOnceOnRecovery, token).ConfigureAwait(false);
            await server.Context.Scheduling.TickAsync(now, token).ConfigureAwait(false);
            List<Run> runs = await RunsAsync(server, topic, token).ConfigureAwait(false);
            Assert.True(runs.Count == 1 && runs[0].Status == RunStatusEnum.Queued && runs[0].ScheduleId == s.Id && runs[0].Trigger == RunTriggerEnum.Schedule, "one queued run");
            Schedule after = (await server.Context.Database.Schedules.ReadAsync(s.TenantId, s.Id, token).ConfigureAwait(false))!;
            Assert.True(after.NextRunUtc > now && after.LastRunUtc.HasValue && after.LockedUntilUtc == null, "next run advanced and lock released");
            await server.Context.Scheduling.TickAsync(now, token).ConfigureAwait(false);
            Assert.Equal(1, (await RunsAsync(server, topic, token).ConfigureAwait(false)).Count, "no double fire");

            after.NextRunUtc = now.AddMinutes(-1);
            await server.Context.Database.Schedules.UpdateAsync(after, token).ConfigureAwait(false);
            await server.Context.Scheduling.TickAsync(now, token).ConfigureAwait(false);
            runs = await RunsAsync(server, topic, token).ConfigureAwait(false);
            Assert.True(runs.Count == 2 && runs.Count(r => r.Status == RunStatusEnum.Skipped && r.SkippedReason!.Contains("still active", StringComparison.Ordinal)) == 1, "overlap skipped");

            TopicFixture fx3 = await ActiveTopicAsync(token).ConfigureAwait(false);
            TestUser u2 = fx3.User;
            Topic paused = fx3.Topic;
            await DueScheduleAsync(server, u2, paused, now.AddMinutes(-1), MissedRunPolicyEnum.RunOnceOnRecovery, token).ConfigureAwait(false);
            await u2.Client.PostAsync("/v1.0/api/topics/" + paused.Id + "/pause", null, token).ConfigureAwait(false);
            await server.Context.Scheduling.TickAsync(now, token).ConfigureAwait(false);
            Assert.Equal(0, (await RunsAsync(server, paused, token).ConfigureAwait(false)).Count, "paused topic does not run");

            TopicFixture fx4 = await ActiveTopicAsync(token).ConfigureAwait(false);
            TestUser u3 = fx4.User;
            Topic skipTopic = fx4.Topic;
            await DueScheduleAsync(server, u3, skipTopic, now.AddHours(-2), MissedRunPolicyEnum.Skip, token).ConfigureAwait(false);
            await server.Context.Scheduling.TickAsync(now, token).ConfigureAwait(false);
            List<Run> skipRuns = await RunsAsync(server, skipTopic, token).ConfigureAwait(false);
            Assert.True(skipRuns.Count == 1 && skipRuns[0].Status == RunStatusEnum.Skipped && skipRuns[0].SkippedReason!.StartsWith("Missed", StringComparison.Ordinal), "missed run skipped by policy");

            TopicFixture fx5 = await ActiveTopicAsync(token).ConfigureAwait(false);
            TestUser u4 = fx5.User;
            Topic recover = fx5.Topic;
            await DueScheduleAsync(server, u4, recover, now.AddHours(-2), MissedRunPolicyEnum.RunOnceOnRecovery, token).ConfigureAwait(false);
            await server.Context.Scheduling.TickAsync(now, token).ConfigureAwait(false);
            List<Run> recovered = await RunsAsync(server, recover, token).ConfigureAwait(false);
            Assert.True(recovered.Count == 1 && recovered[0].Status == RunStatusEnum.Queued, "missed run recovered once");
        }

        private static async Task WorkSignalAsync(CancellationToken token)
        {
            using (WorkSignal signal = new WorkSignal())
            {
                Assert.True(!await signal.WaitAsync(TimeSpan.Zero, token).ConfigureAwait(false), "unset at first");
                signal.Signal();
                signal.Signal();
                signal.Signal();
                Assert.True(await signal.WaitAsync(TimeSpan.Zero, token).ConfigureAwait(false), "a signal sent before the wait is kept");
                Assert.True(!await signal.WaitAsync(TimeSpan.FromMilliseconds(20), token).ConfigureAwait(false), "repeats collapse into one wake and the wait times out");

                Task<bool> waiting = signal.WaitAsync(TimeSpan.FromSeconds(30), token);
                Assert.True(!waiting.IsCompleted, "waits while unset");
                signal.Signal();
                Assert.True(await waiting.ConfigureAwait(false), "a waiter wakes when signalled");

                using (CancellationTokenSource cancel = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    Task<bool> cancelled = signal.WaitAsync(Timeout.InfiniteTimeSpan, cancel.Token);
                    cancel.Cancel();
                    bool threw = false;
                    try
                    {
                        await cancelled.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        threw = true;
                    }

                    Assert.True(threw, "a wait ends on cancellation");
                }

                signal.Dispose();
                signal.Signal();
                bool disposed = false;
                try
                {
                    await signal.WaitAsync(TimeSpan.Zero, token).ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    disposed = true;
                }

                Assert.True(disposed, "signalling after disposal is harmless and waiting is refused");
            }
        }

        private static async Task NoPollWaitAsync(CancellationToken token)
        {
            // Its own database: the shared server's queue and concurrency count belong to every suite at once, and this
            // case needs to know that only its own runs hold slots.
            DatabaseSettings databaseSettings = TestDatabase.NewSettings("mmn_queue");
            DatabaseDriverBase db = await DatabaseDriverFactory.CreateAndInitializeAsync(databaseSettings, token).ConfigureAwait(false);
            try
            {
                using (LoggingModule logging = new LoggingModule())
                {
                    logging.Settings.EnableConsole = false;
                    SeedService seed = new SeedService(db, logging);
                    await seed.EnsureAsync(token).ConfigureAwait(false);
                    ServerSettings settings = new ServerSettings();
                    settings.Pipeline.MaxConcurrentRuns = 2;
                    int running = 0;
                    int peak = 0;
                    int finished = 0;
                    StubRunExecutor slow = new StubRunExecutor
                    {
                        Body = async (r, tk) =>
                        {
                            int now = Interlocked.Increment(ref running);
                            int seen = Volatile.Read(ref peak);
                            while (now > seen)
                            {
                                int was = Interlocked.CompareExchange(ref peak, now, seen);
                                if (was == seen) break;
                                seen = was;
                            }

                            await Task.Delay(150, tk).ConfigureAwait(false);
                            Interlocked.Decrement(ref running);
                            Interlocked.Increment(ref finished);
                            return new RunResult { Summary = "ok", Delivered = true };
                        }
                    };

                    using (SchedulingService first = new SchedulingService(db, settings, logging))
                    using (SchedulingService second = new SchedulingService(db, settings, logging))
                    {
                        // The run worker's loop, with a poll far longer than the test: only the signal can start a run in time.
                        TimeSpan poll = TimeSpan.FromSeconds(120);
                        using (CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(token))
                        {
                            Task worker = Task.Run(async () =>
                            {
                                try
                                {
                                    while (!stop.Token.IsCancellationRequested)
                                    {
                                        await first.DrainQueuedAsync(slow, "signal-test", null, stop.Token).ConfigureAwait(false);
                                        await first.RunSignal.WaitAsync(poll, stop.Token).ConfigureAwait(false);
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                }
                            });

                            await Task.Delay(400, token).ConfigureAwait(false);
                            Stopwatch sw = Stopwatch.StartNew();
                            for (int i = 0; i < 6; i++)
                                await first.RunNowAsync(new Topic { Id = "top_signal" + i, TenantId = seed.DefaultTenantId, Status = TopicStatusEnum.Active }, "usr_signal", false, DateTime.UtcNow, token).ConfigureAwait(false);
                            while (Volatile.Read(ref finished) < 6 && sw.Elapsed < TimeSpan.FromSeconds(30)) await Task.Delay(20, token).ConfigureAwait(false);
                            double seconds = sw.Elapsed.TotalSeconds;
                            Assert.True(Volatile.Read(ref finished) == 6, "six queued runs finish long before the " + poll.TotalSeconds + " second poll (" + Volatile.Read(ref finished) + " finished in " + seconds.ToString("F1") + " s)");
                            Assert.True(Volatile.Read(ref peak) == 2, "both slots are used and never more (peak " + Volatile.Read(ref peak) + ")");
                            // Three rounds of two 150 ms runs; a pass that waited for its whole batch and then a poll could not do this.
                            Assert.True(seconds < 10, "runs follow one another as slots free (" + seconds.ToString("F1") + " s)");

                            stop.Cancel();
                            await worker.ConfigureAwait(false);
                        }

                        List<Run> runs = (await db.Runs.EnumerateAsync(new RunFilter { TenantId = seed.DefaultTenantId, PageSize = 100 }, token).ConfigureAwait(false)).Objects;
                        Assert.True(runs.Count == 6 && runs.All(r => r.Status == RunStatusEnum.Completed && r.WorkerId == "signal-test"), "every run completed through the signalled worker");

                        // Competing claimers, as several server processes would be: two services, four loops each, all
                        // claiming at once. The limit is counted and claimed in one step, so the peak cannot pass it.
                        Volatile.Write(ref peak, 0);
                        Volatile.Write(ref finished, 0);
                        for (int i = 0; i < 12; i++)
                            await first.RunNowAsync(new Topic { Id = "top_compete" + i, TenantId = seed.DefaultTenantId, Status = TopicStatusEnum.Active }, "usr_signal", false, DateTime.UtcNow, token).ConfigureAwait(false);
                        int largestClaim = 0;
                        Stopwatch compete = Stopwatch.StartNew();
                        List<Task> claimers = new List<Task>();
                        for (int i = 0; i < 8; i++)
                        {
                            SchedulingService service = i % 2 == 0 ? first : second;
                            claimers.Add(Task.Run(async () =>
                            {
                                while (Volatile.Read(ref finished) < 12 && compete.Elapsed < TimeSpan.FromSeconds(60))
                                {
                                    int executed = await service.ExecuteQueuedAsync(slow, "compete-test", null, token).ConfigureAwait(false);
                                    int largest = Volatile.Read(ref largestClaim);
                                    while (executed > largest)
                                    {
                                        int was = Interlocked.CompareExchange(ref largestClaim, executed, largest);
                                        if (was == largest) break;
                                        largest = was;
                                    }
                                }
                            }));
                        }

                        await Task.WhenAll(claimers).ConfigureAwait(false);
                        Assert.True(Volatile.Read(ref finished) == 12, "competing claimers finish every run (" + Volatile.Read(ref finished) + ")");
                        Assert.True(Volatile.Read(ref peak) <= 2 && Volatile.Read(ref largestClaim) <= 2, "eight competing claimers never run more than two at once (peak " + Volatile.Read(ref peak) + ", largest claim " + Volatile.Read(ref largestClaim) + ")");
                        Assert.True(await db.Runs.CountRunningAsync(token).ConfigureAwait(false) == 0, "no slot is left held");
                    }
                }
            }
            finally
            {
                await db.DisposeAsync().ConfigureAwait(false);
                await TestDatabase.DropAsync(databaseSettings, CancellationToken.None).ConfigureAwait(false);
            }
        }

        private static async Task<Run> QueueAsync(TestUser u, Topic topic, CancellationToken token)
        {
            return (await u.Client.PostAsync("/v1.0/api/topics/" + topic.Id + "/run", null, token).ConfigureAwait(false)).Expect(202, "run now").Json<Run>();
        }

        private static async Task<Run> ReadRunAsync(MyMorningNewsServer server, Run run, CancellationToken token)
        {
            return (await server.Context.Database.Runs.ReadAsync(run.TenantId, run.Id, token).ConfigureAwait(false))!;
        }

        private static async Task DrainAsync(MyMorningNewsServer server, IRunExecutor executor, CancellationToken token)
        {
            IRunExecutor routed = Routed(server, executor);
            for (int i = 0; i < 20; i++)
                if (await server.Context.Scheduling.ExecuteQueuedAsync(routed, "test", _Tenants.Keys.ToList(), token).ConfigureAwait(false) == 0) break;
        }

        private static IRunExecutor Routed(MyMorningNewsServer server, IRunExecutor executor)
        {
            foreach (string topicId in _Topics.Keys) TestRuns.Route(topicId, executor);
            return TestRuns.Executor(server);
        }

        private static async Task ExecuteAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            StubRunExecutor ok = new StubRunExecutor();
            await DrainAsync(server, ok, token).ConfigureAwait(false);

            TopicFixture fx6 = await ActiveTopicAsync(token).ConfigureAwait(false);
            TestUser u = fx6.User;
            Topic topic = fx6.Topic;
            Run run = await QueueAsync(u, topic, token).ConfigureAwait(false);
            await DrainAsync(server, ok, token).ConfigureAwait(false);
            Run done = await ReadRunAsync(server, run, token).ConfigureAwait(false);
            Assert.True(done.Status == RunStatusEnum.Completed && done.Summary == "ok" && done.StartedUtc.HasValue && done.CompletedUtc.HasValue && done.WorkerId == "test", "completed");
            Topic t = (await server.Context.Database.Topics.ReadAsync(topic.TenantId, topic.Id, token).ConfigureAwait(false))!;
            Assert.True(t.LastRunUtc.HasValue, "topic last run recorded");

            Run failing = await QueueAsync(u, topic, token).ConfigureAwait(false);
            await DrainAsync(server, new StubRunExecutor { Body = (r, tk) => throw new InvalidOperationException("search provider exploded") }, token).ConfigureAwait(false);
            Run failed = await ReadRunAsync(server, failing, token).ConfigureAwait(false);
            Assert.True(failed.Status == RunStatusEnum.Failed && failed.ErrorMessage == "search provider exploded", "failure recorded");

            int before = server.Context.SettingsService.Current.Pipeline.MaxConcurrentRuns;
            server.Context.SettingsService.Current.Pipeline.MaxConcurrentRuns = 1;
            try
            {
                TopicFixture fx7 = await ActiveTopicAsync(token).ConfigureAwait(false);
                TestUser ua = fx7.User;
                Topic ta = fx7.Topic;
                TopicFixture fx8 = await ActiveTopicAsync(token).ConfigureAwait(false);
                TestUser ub = fx8.User;
                Topic tb = fx8.Topic;
                Run ra = await QueueAsync(ua, ta, token).ConfigureAwait(false);
                Run rb = await QueueAsync(ub, tb, token).ConfigureAwait(false);
                // The queue is shared with suites running in parallel, so another suite may claim these runs or hold the slot.
                // The rule under test: one pass never claims more runs than there are free slots, and both runs finish.
                IRunExecutor routed = Routed(server, ok);
                for (int i = 0; i < 400; i++)
                {
                    int executed = await server.Context.Scheduling.ExecuteQueuedAsync(routed, "test", _Tenants.Keys.ToList(), token).ConfigureAwait(false);
                    Assert.True(executed <= 1, "never more than one run at a time (" + executed + ")");
                    if ((await ReadRunAsync(server, ra, token).ConfigureAwait(false)).Status == RunStatusEnum.Completed && (await ReadRunAsync(server, rb, token).ConfigureAwait(false)).Status == RunStatusEnum.Completed) break;
                    if (executed == 0) await Task.Delay(25, token).ConfigureAwait(false);
                }

                Assert.True((await ReadRunAsync(server, ra, token).ConfigureAwait(false)).Status == RunStatusEnum.Completed && (await ReadRunAsync(server, rb, token).ConfigureAwait(false)).Status == RunStatusEnum.Completed, "both runs finish one at a time");
            }
            finally
            {
                server.Context.SettingsService.Current.Pipeline.MaxConcurrentRuns = before;
            }

            TopicFixture fx9 = await ActiveTopicAsync(token).ConfigureAwait(false);
            TestUser uc = fx9.User;
            Topic tc = fx9.Topic;
            Run slow = await QueueAsync(uc, tc, token).ConfigureAwait(false);
            StubRunExecutor waiting = new StubRunExecutor
            {
                Body = async (r, tk) =>
                {
                    await server.Context.Scheduling.CancelAsync((await ReadRunAsync(server, r, CancellationToken.None).ConfigureAwait(false)), DateTime.UtcNow, CancellationToken.None).ConfigureAwait(false);
                    throw new OperationCanceledException("cancelled by test");
                }
            };
            await DrainAsync(server, waiting, token).ConfigureAwait(false);
            Assert.Equal(RunStatusEnum.Cancelled, (await ReadRunAsync(server, slow, token).ConfigureAwait(false)).Status, "cancelled while running");
        }

        private static async Task RunApiAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TopicFixture fx10 = await ActiveTopicAsync(token).ConfigureAwait(false);
            TestUser u = fx10.User;
            Topic topic = fx10.Topic;
            Run run = await QueueAsync(u, topic, token).ConfigureAwait(false);
            Assert.True(run.Status == RunStatusEnum.Queued && run.Trigger == RunTriggerEnum.Manual && run.UserId == u.User.Id, "queued manually");
            (await u.Client.PostAsync("/v1.0/api/topics/" + topic.Id + "/run", null, token).ConfigureAwait(false)).Expect(409, "one active run per topic");
            Run cancelled = (await u.Client.PostAsync("/v1.0/api/runs/" + run.Id + "/cancel", null, token).ConfigureAwait(false)).Expect(200, "cancel").Json<Run>();
            Assert.True(cancelled.Status == RunStatusEnum.Cancelled && cancelled.CancelRequested, "queued run cancelled at once");
            (await u.Client.PostAsync("/v1.0/api/runs/" + run.Id + "/cancel", null, token).ConfigureAwait(false)).Expect(409, "already finished");

            Topic draft = (await u.Client.PostAsync("/v1.0/api/topics", new { name = "Draft topic" }, token).ConfigureAwait(false)).Json<Topic>();
            (await u.Client.PostAsync("/v1.0/api/topics/" + draft.Id + "/run", null, token).ConfigureAwait(false)).Expect(409, "drafts need refinement");
            Run dry = (await u.Client.PostAsync("/v1.0/api/topics/" + draft.Id + "/run?dryRun=true", null, token).ConfigureAwait(false)).Expect(202, "dry run allowed").Json<Run>();
            Assert.True(dry.IsDryRun, "dry run flag");
            EnumerationResult<Run> list = (await u.Client.GetAsync("/v1.0/api/runs?topicId=" + topic.Id, token).ConfigureAwait(false)).Expect(200, "list").Json<EnumerationResult<Run>>();
            Assert.Equal(1, list.Objects.Count, "runs by topic");
            TestUser other = await TestFixtures.CreateUserAsync(await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false), topic.TenantId, false, token).ConfigureAwait(false);
            (await other.Client.GetAsync("/v1.0/api/runs/" + run.Id, token).ConfigureAwait(false)).Expect(404, "runs are private");
            await server.Context.Scheduling.CancelAsync(dry, DateTime.UtcNow, token).ConfigureAwait(false);
        }

        private static async Task RequestDueAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser ta = await TestFixtures.CreateUserAsync(admin, t.Id, true, token).ConfigureAwait(false);
            ModelEndpoint e = (await ta.Client.PostAsync("/v1.0/api/model-endpoints", new { name = "stub", provider = "OpenAiCompatible", endpoint = StubModelServer.Shared.BaseUrl, model = "stub-refine", supportsChat = true }, token).ConfigureAwait(false))
                .Json<ModelEndpoint>();
            await ta.Client.PutAsync("/v1.0/api/model-assignments", new { task = "Refinement", modelEndpointId = e.Id }, token).ConfigureAwait(false);
            ResearchRequestResponse r = (await ta.Client.PostAsync("/v1.0/api/requests", new { requestText = "Podcast on the history of the shipping container", deliverables = new[] { "Podcast" }, skipQuestions = true }, token).ConfigureAwait(false))
                .Expect(201, "request").Json<ResearchRequestResponse>();
            await ta.Client.PostAsync("/v1.0/api/requests/" + r.Request.Id + "/confirm", null, token).ConfigureAwait(false);
            if (r.Request.TopicId != null) _Topics[r.Request.TopicId] = true;
            _Tenants[t.Id] = true;
            await server.Context.Scheduling.TickAsync(DateTime.UtcNow.AddSeconds(1), token).ConfigureAwait(false);
            ResearchRequest running = (await server.Context.Database.ResearchRequests.ReadAsync(t.Id, r.Request.Id, token).ConfigureAwait(false))!;
            Assert.True(running.Status == ResearchRequestStatusEnum.Running && running.RunId != null, "request claimed and queued");
            Run run = (await server.Context.Database.Runs.ReadAsync(t.Id, running.RunId!, token).ConfigureAwait(false))!;
            Assert.True(run.Trigger == RunTriggerEnum.Request && run.ResearchRequestId == r.Request.Id && run.TopicId == r.Request.TopicId, "run for the request");
            await DrainAsync(server, new StubRunExecutor { Body = (x, tk) => Task.FromResult(new RunResult { Summary = "delivered", BriefingId = "brf_test", Delivered = true }) }, token).ConfigureAwait(false);
            ResearchRequest done = (await server.Context.Database.ResearchRequests.ReadAsync(t.Id, r.Request.Id, token).ConfigureAwait(false))!;
            Assert.True(done.Status == ResearchRequestStatusEnum.Completed && done.BriefingId == "brf_test" && done.CompletedUtc.HasValue, "request completed when delivered");
        }
    }
}
