namespace MyMorningNews.Core.Settings
{
    using System;

    /// <summary>
    /// Scheduler and run worker settings (plan section 14.2).
    /// </summary>
    public class SchedulerSettings
    {
        #region Public-Members

        /// <summary>
        /// Run the scheduler and run workers in this process. Default true.
        /// </summary>
        [Setting(Description = "Run the scheduler and run workers.", RestartRequired = true, EnvironmentVariable = "SCHEDULER_ENABLED")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Seconds between scheduler polls. Default 15. Minimum 1, maximum 300.
        /// </summary>
        [Setting(Description = "Seconds between scheduler polls.", Minimum = 1, Maximum = 300)]
        public int PollIntervalSeconds
        {
            get { return _PollIntervalSeconds; }
            set { _PollIntervalSeconds = Math.Clamp(value, 1, 300); }
        }

        /// <summary>
        /// Smallest interval for every-n-minutes schedules. Default 5. Minimum 1, maximum 1440.
        /// </summary>
        [Setting(Description = "Smallest every-n-minutes interval.", Minimum = 1, Maximum = 1440)]
        public int MinimumIntervalMinutes
        {
            get { return _MinimumIntervalMinutes; }
            set { _MinimumIntervalMinutes = Math.Clamp(value, 1, 1440); }
        }

        /// <summary>
        /// A due time older than this (minutes) counts as missed while the server was down. Default 10. Minimum 1, maximum 1440.
        /// </summary>
        [Setting(Description = "Minutes after which a due run counts as missed.", Minimum = 1, Maximum = 1440)]
        public int MissedRunGraceMinutes
        {
            get { return _MissedRunGraceMinutes; }
            set { _MissedRunGraceMinutes = Math.Clamp(value, 1, 1440); }
        }

        /// <summary>
        /// Seconds between run queue polls. Default 2. Minimum 1, maximum 60.
        /// </summary>
        [Setting(Description = "Seconds between run queue polls.", Minimum = 1, Maximum = 60)]
        public int RunPollSeconds
        {
            get { return _RunPollSeconds; }
            set { _RunPollSeconds = Math.Clamp(value, 1, 60); }
        }

        #endregion

        #region Private-Members

        private int _PollIntervalSeconds = 15;
        private int _MinimumIntervalMinutes = 5;
        private int _MissedRunGraceMinutes = 10;
        private int _RunPollSeconds = 2;

        #endregion
    }
}
