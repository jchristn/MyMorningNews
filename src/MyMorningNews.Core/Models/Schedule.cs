namespace MyMorningNews.Core.Models
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// When research runs for a topic, an edition composes, or a one-time request runs. Times are computed in the schedule time zone.
    /// </summary>
    public class Schedule
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix sch_).
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
        /// Owner kind.
        /// </summary>
        public ScheduleOwnerTypeEnum OwnerType { get; set; } = ScheduleOwnerTypeEnum.Topic;

        /// <summary>
        /// Owning topic, edition, or request.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string OwnerId
        {
            get
            {
                return _OwnerId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(OwnerId));
                _OwnerId = value;
            }
        }

        /// <summary>
        /// User the schedule belongs to.
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
        /// Optional label. May be null.
        /// </summary>
        public string? Name { get; set; } = null;

        /// <summary>
        /// Interval type.
        /// </summary>
        public IntervalTypeEnum IntervalType { get; set; } = IntervalTypeEnum.DaysOfWeek;

        /// <summary>
        /// Interval size (minutes, hours, days, or weeks).
        /// </summary>
        public int IntervalValue { get; set; } = 1;

        /// <summary>
        /// Minute past the hour for EveryNHours. Minimum 0, maximum 59; values outside the range are clamped.
        /// </summary>
        public int MinuteOffset
        {
            get
            {
                return _MinuteOffset;
            }
            set
            {
                _MinuteOffset = Math.Clamp(value, 0, 59);
            }
        }

        /// <summary>
        /// Bit mask of days of week (Sunday = 1, Monday = 2, ... Saturday = 64).
        /// </summary>
        public int DaysOfWeekMask { get; set; } = 62;

        /// <summary>
        /// Days of month (1 to 31, -1 for the last day).
        /// </summary>
        public List<int> DaysOfMonth
        {
            get
            {
                return _DaysOfMonth;
            }
            set
            {
                _DaysOfMonth = value ?? new List<int>();
            }
        }

        /// <summary>
        /// Local times of day as HH:mm.
        /// </summary>
        public List<string> TimesOfDay
        {
            get
            {
                return _TimesOfDay;
            }
            set
            {
                _TimesOfDay = value ?? new List<string>();
            }
        }

        /// <summary>
        /// Cron expression (Cronos syntax) for the Cron type. May be null.
        /// </summary>
        public string? CronExpression { get; set; } = null;

        /// <summary>
        /// IANA time zone.
        /// </summary>
        public string TimeZone
        {
            get
            {
                return _TimeZone;
            }
            set
            {
                _TimeZone = value ?? String.Empty;
            }
        }

        /// <summary>
        /// Not before this time. May be null.
        /// </summary>
        public DateTime? StartUtc { get; set; } = null;

        /// <summary>
        /// Not after this time. May be null.
        /// </summary>
        public DateTime? EndUtc { get; set; } = null;

        /// <summary>
        /// Run time for the Once type. May be null.
        /// </summary>
        public DateTime? RunOnceUtc { get; set; } = null;

        /// <summary>
        /// Enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// What to do with runs missed while the server was down.
        /// </summary>
        public MissedRunPolicyEnum MissedRunPolicy { get; set; } = MissedRunPolicyEnum.RunOnceOnRecovery;

        /// <summary>
        /// Next due time (UTC); null when nothing is due. May be null.
        /// </summary>
        public DateTime? NextRunUtc { get; set; } = null;

        /// <summary>
        /// Last time the schedule fired. May be null.
        /// </summary>
        public DateTime? LastRunUtc { get; set; } = null;

        /// <summary>
        /// Claim lock held by a scheduler instance. May be null.
        /// </summary>
        public DateTime? LockedUntilUtc { get; set; } = null;

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

        #endregion

        #region Private-Members

        private string _Id = IdGenerator.GenerateScheduleId();
        private string _TenantId = String.Empty;
        private string _OwnerId = String.Empty;
        private string _UserId = String.Empty;
        private int _MinuteOffset = 0;
        private List<int> _DaysOfMonth = new List<int>();
        private List<string> _TimesOfDay = new List<string>();
        private string _TimeZone = "UTC";

        #endregion
    }
}
