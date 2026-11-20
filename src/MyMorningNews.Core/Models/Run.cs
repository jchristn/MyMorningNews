namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A research run for a topic or one-time request, from queued to finished.
    /// </summary>
    public class Run
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix run_).
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Id
        {
            get
            {
                return _Id;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Id));
                _Id = value;
            }
        }

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string TenantId
        {
            get
            {
                return _TenantId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(TenantId));
                _TenantId = value;
            }
        }

        /// <summary>
        /// Topic.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string TopicId
        {
            get
            {
                return _TopicId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(TopicId));
                _TopicId = value;
            }
        }

        /// <summary>
        /// User the run is attributed to.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string UserId
        {
            get
            {
                return _UserId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(UserId));
                _UserId = value;
            }
        }

        /// <summary>
        /// Schedule that triggered it, when any. May be null.
        /// </summary>
        public string? ScheduleId { get; set; } = null;

        /// <summary>
        /// One-time request, when any. May be null.
        /// </summary>
        public string? ResearchRequestId { get; set; } = null;

        /// <summary>
        /// What started the run.
        /// </summary>
        public RunTriggerEnum Trigger { get; set; } = RunTriggerEnum.Schedule;

        /// <summary>
        /// Status.
        /// </summary>
        public RunStatusEnum Status { get; set; } = RunStatusEnum.Queued;

        /// <summary>
        /// Dry run: no email and not counted in history.
        /// </summary>
        public bool IsDryRun { get; set; } = false;

        /// <summary>
        /// Cancellation requested.
        /// </summary>
        public bool CancelRequested { get; set; } = false;

        /// <summary>
        /// UTC time queued.
        /// </summary>
        public DateTime QueuedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC time started. May be null.
        /// </summary>
        public DateTime? StartedUtc { get; set; } = null;

        /// <summary>
        /// UTC time finished. May be null.
        /// </summary>
        public DateTime? CompletedUtc { get; set; } = null;

        /// <summary>
        /// Claim lock held by a run worker. May be null.
        /// </summary>
        public DateTime? LockedUntilUtc { get; set; } = null;

        /// <summary>
        /// Worker that claimed the run. May be null.
        /// </summary>
        public string? WorkerId { get; set; } = null;

        /// <summary>
        /// Why a due run was skipped. May be null.
        /// </summary>
        public string? SkippedReason { get; set; } = null;

        /// <summary>
        /// Failure reason. May be null.
        /// </summary>
        public string? ErrorMessage { get; set; } = null;

        /// <summary>
        /// Short outcome summary. May be null.
        /// </summary>
        public string? Summary { get; set; } = null;

        /// <summary>
        /// Execution time in milliseconds.
        /// </summary>
        public double DurationMs { get; set; } = 0;

        /// <summary>
        /// New findings.
        /// </summary>
        public int NewFindingCount { get; set; } = 0;

        /// <summary>
        /// Duplicate findings.
        /// </summary>
        public int DuplicateCount { get; set; } = 0;

        /// <summary>
        /// Sources read.
        /// </summary>
        public int SourceCount { get; set; } = 0;

        /// <summary>
        /// Model input tokens.
        /// </summary>
        public int InputTokens { get; set; } = 0;

        /// <summary>
        /// Model output tokens.
        /// </summary>
        public int OutputTokens { get; set; } = 0;

        /// <summary>
        /// Estimated model cost. May be null.
        /// </summary>
        public decimal? EstimatedCostUsd { get; set; } = null;

        /// <summary>
        /// Hash of the assembled context. May be null.
        /// </summary>
        public string? ContextSnapshotHash { get; set; } = null;

        /// <summary>
        /// Whether the record is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// UTC creation time.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC time of the last update.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When set, the next execution reruns the pipeline from this stage on the same run, reusing its saved findings and
        /// summary (POST /runs/{id}/retry). Cleared when execution starts. Null for a normal run.
        /// </summary>
        public string? ResumeFromStage { get; set; } = null;

        #endregion

        #region Private-Members

        private string _Id = IdGenerator.GenerateRunId();
        private string _TenantId = String.Empty;
        private string _TopicId = String.Empty;
        private string _UserId = String.Empty;

        #endregion
    }
}
