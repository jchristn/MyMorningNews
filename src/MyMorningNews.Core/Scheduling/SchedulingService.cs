namespace MyMorningNews.Core.Scheduling
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Pipeline;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Core.Telemetry;
    using SyslogLogging;

    /// <summary>
    /// Schedules and the run queue (plan section 14.2): saves schedules with their next due time, fires due schedules and
    /// due one-time requests into queued runs (never two active runs per topic), and executes queued runs through an
    /// <see cref="IRunExecutor"/>. All claims use FOR UPDATE SKIP LOCKED, so several server instances can run the workers.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class SchedulingService
    {
        #region Public-Members

        /// <summary>
        /// Maximum schedules or requests claimed per tick. Default 100. Minimum 1, maximum 1000.
        /// </summary>
        public int ClaimBatchSize
        {
            get { return _ClaimBatchSize; }
            set { _ClaimBatchSize = Math.Clamp(value, 1, 1000); }
        }

        #endregion

        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[Scheduler] ";
        private int _ClaimBatchSize = 100;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="settings">Server settings (Scheduler and Pipeline groups are read on every call).</param>
        /// <param name="logging">Logging.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public SchedulingService(DatabaseDriverBase database, ServerSettings settings, LoggingModule logging)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Validate a schedule, compute its next due time from now, and create or update it.
        /// </summary>
        /// <param name="schedule">Schedule.</param>
        /// <param name="create">True to create, false to update an existing schedule.</param>
        /// <param name="nowUtc">Current time (UTC).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Saved schedule.</returns>
        /// <exception cref="ArgumentException">The schedule is invalid; the message lists every problem.</exception>
        public async Task<Schedule> SaveAsync(Schedule schedule, bool create, DateTime nowUtc, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(schedule);
            List<string> errors = ScheduleCalculator.Validate(schedule, _Settings.Scheduler.MinimumIntervalMinutes);
            if (errors.Count > 0) throw new ArgumentException(String.Join(" ", errors), nameof(schedule));
            if (create) schedule.CreatedUtc = nowUtc;
            schedule.NextRunUtc = schedule.Enabled ? ScheduleCalculator.NextOccurrence(schedule, nowUtc) : null;
            schedule.LockedUntilUtc = null;
            return create ? await _Database.Schedules.CreateAsync(schedule, token).ConfigureAwait(false) : await _Database.Schedules.UpdateAsync(schedule, token).ConfigureAwait(false);
        }

        /// <summary>
        /// One scheduler pass: fire due schedules and due one-time requests.
        /// </summary>
        /// <param name="nowUtc">Current time (UTC).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Runs queued.</returns>
        public async Task<int> TickAsync(DateTime nowUtc, CancellationToken token = default)
        {
            int queued = 0;
            DateTime lockUntil = nowUtc.AddMinutes(2);
            foreach (Schedule schedule in await _Database.Schedules.ClaimDueAsync(nowUtc, lockUntil, _ClaimBatchSize, token).ConfigureAwait(false))
            {
                try
                {
                    if (await FireAsync(schedule, nowUtc, token).ConfigureAwait(false)) queued++;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _Logging.Warn(_Header + "schedule " + schedule.Id + " failed to fire: " + ex.Message);
                }
                finally
                {
                    schedule.LastRunUtc = nowUtc;
                    schedule.NextRunUtc = ScheduleCalculator.NextOccurrence(schedule, nowUtc);
                    schedule.LockedUntilUtc = null;
                    await _Database.Schedules.UpdateAsync(schedule, token).ConfigureAwait(false);
                }
            }

            foreach (ResearchRequest request in await _Database.ResearchRequests.ClaimDueAsync(nowUtc, _ClaimBatchSize, token).ConfigureAwait(false))
            {
                if (String.IsNullOrEmpty(request.TopicId))
                {
                    request.Status = ResearchRequestStatusEnum.Failed;
                    request.ErrorMessage = "The request has no plan.";
                    await _Database.ResearchRequests.UpdateAsync(request, token).ConfigureAwait(false);
                    continue;
                }

                Run run = await EnqueueAsync(request.TenantId, request.TopicId, request.UserId, RunTriggerEnum.Request, null, request.Id, false, nowUtc, token).ConfigureAwait(false);
                request.RunId = run.Id;
                await _Database.ResearchRequests.UpdateAsync(request, token).ConfigureAwait(false);
                MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.SchedulerClaims, "outcome", "request");
                queued++;
            }

            return queued;
        }

        /// <summary>
        /// Queue a run now ("Run now" or a dry run).
        /// </summary>
        /// <param name="topic">Topic.</param>
        /// <param name="userId">User the run is attributed to.</param>
        /// <param name="dryRun">Dry run: no email and not counted in history.</param>
        /// <param name="nowUtc">Current time (UTC).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The queued run.</returns>
        /// <exception cref="ConflictException">The topic already has an active run, or it is a draft or archived.</exception>
        public async Task<Run> RunNowAsync(Topic topic, string userId, bool dryRun, DateTime nowUtc, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(topic);
            if (topic.Status == TopicStatusEnum.Archived) throw new ConflictException("Archived topics do not run. Resume the topic first.");
            if (topic.Status == TopicStatusEnum.Draft && !dryRun) throw new ConflictException("Finish refining the topic before running it, or start a dry run.");
            if (await _Database.Runs.HasActiveRunAsync(topic.TenantId, topic.Id, token).ConfigureAwait(false)) throw new ConflictException("This topic already has a run queued or in progress.");
            return await EnqueueAsync(topic.TenantId, topic.Id, userId, RunTriggerEnum.Manual, null, null, dryRun, nowUtc, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Request cancellation. A queued run is cancelled immediately; a running run stops at its next checkpoint.
        /// </summary>
        /// <param name="run">Run.</param>
        /// <param name="nowUtc">Current time (UTC).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated run.</returns>
        /// <exception cref="ConflictException">The run already finished.</exception>
        public async Task<Run> CancelAsync(Run run, DateTime nowUtc, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(run);
            if (run.Status != RunStatusEnum.Queued && run.Status != RunStatusEnum.Running) throw new ConflictException("The run already finished (" + run.Status + ").");
            run.CancelRequested = true;
            if (run.Status == RunStatusEnum.Queued)
            {
                run.Status = RunStatusEnum.Cancelled;
                run.CompletedUtc = nowUtc;
            }

            return await _Database.Runs.UpdateAsync(run, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Claim queued runs up to the free concurrency and execute them. Returns after the claimed runs finish.
        /// </summary>
        /// <param name="executor">Executor.</param>
        /// <param name="workerId">Worker identity.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Runs executed.</returns>
        public async Task<int> ExecuteQueuedAsync(IRunExecutor executor, string workerId, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(executor);
            DateTime now = DateTime.UtcNow;
            await _Database.Runs.FailAbandonedAsync(now, token).ConfigureAwait(false);
            long running = await _Database.Runs.CountRunningAsync(token).ConfigureAwait(false);
            int free = (int)Math.Max(0, _Settings.Pipeline.MaxConcurrentRuns - running);
            if (free == 0) return 0;
            List<Run> runs = await _Database.Runs.ClaimQueuedAsync(now, now.AddMinutes(_Settings.Pipeline.RunTimeoutMinutes + 5), free, workerId, token).ConfigureAwait(false);
            await Task.WhenAll(runs.Select(r => ExecuteAsync(r, executor, token))).ConfigureAwait(false);
            return runs.Count;
        }

        #endregion

        #region Private-Methods

        private async Task<bool> FireAsync(Schedule schedule, DateTime nowUtc, CancellationToken token)
        {
            if (schedule.OwnerType != ScheduleOwnerTypeEnum.Topic)
            {
                MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.SchedulerClaims, "outcome", "unsupported_owner");
                return false;
            }

            Topic? topic = await _Database.Topics.ReadAsync(schedule.TenantId, schedule.OwnerId, token).ConfigureAwait(false);
            if (topic == null || topic.Status != TopicStatusEnum.Active)
            {
                MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.SchedulerClaims, "outcome", "topic_inactive");
                return false;
            }

            DateTime due = schedule.NextRunUtc ?? nowUtc;
            bool missed = nowUtc - due > TimeSpan.FromMinutes(_Settings.Scheduler.MissedRunGraceMinutes);
            if (missed && schedule.MissedRunPolicy == MissedRunPolicyEnum.Skip)
            {
                await RecordSkippedAsync(topic, schedule, "Missed while the server was offline (due " + due.ToString("u") + ").", nowUtc, token).ConfigureAwait(false);
                MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.SchedulerClaims, "outcome", "missed");
                return false;
            }

            if (await _Database.Runs.HasActiveRunAsync(topic.TenantId, topic.Id, token).ConfigureAwait(false))
            {
                await RecordSkippedAsync(topic, schedule, "The previous run was still active.", nowUtc, token).ConfigureAwait(false);
                MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.SchedulerClaims, "outcome", "skipped_active");
                return false;
            }

            await EnqueueAsync(topic.TenantId, topic.Id, schedule.UserId, RunTriggerEnum.Schedule, schedule.Id, null, false, nowUtc, token).ConfigureAwait(false);
            MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.SchedulerClaims, "outcome", missed ? "recovered" : "enqueued");
            return true;
        }

        private async Task RecordSkippedAsync(Topic topic, Schedule schedule, string reason, DateTime nowUtc, CancellationToken token)
        {
            await _Database.Runs.CreateAsync(new Run
            {
                TenantId = topic.TenantId,
                TopicId = topic.Id,
                UserId = schedule.UserId,
                ScheduleId = schedule.Id,
                Trigger = RunTriggerEnum.Schedule,
                Status = RunStatusEnum.Skipped,
                QueuedUtc = nowUtc,
                CompletedUtc = nowUtc,
                SkippedReason = reason
            }, token).ConfigureAwait(false);
        }

        private async Task<Run> EnqueueAsync(string tenantId, string topicId, string userId, RunTriggerEnum trigger, string? scheduleId, string? requestId, bool dryRun, DateTime nowUtc, CancellationToken token)
        {
            return await _Database.Runs.CreateAsync(new Run
            {
                TenantId = tenantId,
                TopicId = topicId,
                UserId = userId,
                ScheduleId = scheduleId,
                ResearchRequestId = requestId,
                Trigger = trigger,
                Status = RunStatusEnum.Queued,
                IsDryRun = dryRun,
                QueuedUtc = nowUtc
            }, token).ConfigureAwait(false);
        }

        private async Task ExecuteAsync(Run run, IRunExecutor executor, CancellationToken token)
        {
            Stopwatch sw = Stopwatch.StartNew();
            MyMorningNewsTelemetry.Record(MyMorningNewsTelemetry.RunQueueWait, Math.Max(0, ((run.StartedUtc ?? DateTime.UtcNow) - run.QueuedUtc).TotalSeconds), "trigger", run.Trigger.ToString());
            string outcome;
            RunResult? result = null;
            string? error = null;
            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(TimeSpan.FromMinutes(_Settings.Pipeline.RunTimeoutMinutes));
                try
                {
                    Run? fresh = await _Database.Runs.ReadAsync(run.TenantId, run.Id, token).ConfigureAwait(false);
                    if (fresh != null && fresh.CancelRequested) throw new OperationCanceledException("Cancelled before start.");
                    result = await executor.ExecuteAsync(run, timeout.Token).ConfigureAwait(false);
                    outcome = "completed";
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    bool cancelled = (await _Database.Runs.ReadAsync(run.TenantId, run.Id, token).ConfigureAwait(false))?.CancelRequested == true;
                    outcome = cancelled ? "cancelled" : "timeout";
                    error = cancelled ? "Cancelled." : "The run exceeded the " + _Settings.Pipeline.RunTimeoutMinutes + " minute limit.";
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    outcome = "failed";
                    error = ex.Message;
                    _Logging.Warn(_Header + "run " + run.Id + " failed: " + ex.Message);
                }
            }

            DateTime done = DateTime.UtcNow;
            run.Status = outcome == "completed" ? (result != null && result.PartiallySucceeded ? RunStatusEnum.PartiallySucceeded : RunStatusEnum.Completed)
                : outcome == "cancelled" ? RunStatusEnum.Cancelled : RunStatusEnum.Failed;
            run.CompletedUtc = done;
            run.LockedUntilUtc = null;
            run.Summary = result?.Summary;
            run.ErrorMessage = error;
            await _Database.Runs.UpdateAsync(run, CancellationToken.None).ConfigureAwait(false);

            if (!run.IsDryRun)
            {
                Topic? topic = await _Database.Topics.ReadAsync(run.TenantId, run.TopicId, CancellationToken.None).ConfigureAwait(false);
                if (topic != null)
                {
                    topic.LastRunUtc = done;
                    await _Database.Topics.UpdateAsync(topic, CancellationToken.None).ConfigureAwait(false);
                }
            }

            if (!String.IsNullOrEmpty(run.ResearchRequestId))
            {
                ResearchRequest? request = await _Database.ResearchRequests.ReadAsync(run.TenantId, run.ResearchRequestId, CancellationToken.None).ConfigureAwait(false);
                if (request != null)
                {
                    bool delivered = (run.Status == RunStatusEnum.Completed || run.Status == RunStatusEnum.PartiallySucceeded) && result != null && result.Delivered;
                    request.Status = delivered ? ResearchRequestStatusEnum.Completed : run.Status == RunStatusEnum.Cancelled ? ResearchRequestStatusEnum.Cancelled : ResearchRequestStatusEnum.Failed;
                    request.BriefingId = result?.BriefingId;
                    request.CompletedUtc = done;
                    request.ErrorMessage = delivered ? null : error ?? result?.Summary ?? "Nothing was delivered.";
                    await _Database.ResearchRequests.UpdateAsync(request, CancellationToken.None).ConfigureAwait(false);
                }
            }

            MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.Runs, "trigger", run.Trigger.ToString(), "outcome", outcome);
            MyMorningNewsTelemetry.Record(MyMorningNewsTelemetry.RunDuration, sw.Elapsed.TotalSeconds, "trigger", run.Trigger.ToString());
        }

        #endregion
    }
}
