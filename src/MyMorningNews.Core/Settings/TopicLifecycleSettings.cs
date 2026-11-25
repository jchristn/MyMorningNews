namespace MyMorningNews.Core.Settings
{
    using System;

    /// <summary>
    /// Keep-or-delete reviews of recurring topics that stop producing new findings.
    /// </summary>
    public class TopicLifecycleSettings
    {
        /// <summary>
        /// Ask owners to keep or delete quiet topics, and delete the ones nobody keeps. Default true.
        /// </summary>
        [Setting(Description = "Ask owners to keep or delete topics without new findings.", EnvironmentVariable = "TOPIC_LIFECYCLE_ENABLED")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Days without a run that has new findings before the owner is asked. Default 7, minimum 1, maximum 365.
        /// </summary>
        [Setting(Description = "Days without new findings before asking.", Minimum = 1, Maximum = 365)]
        public int StaleDays
        {
            get { return _StaleDays; }
            set { _StaleDays = Math.Clamp(value, 1, 365); }
        }

        /// <summary>
        /// Days the owner has to choose Keep before the topic is deleted. Default 14, minimum 1, maximum 365.
        /// </summary>
        [Setting(Description = "Days to answer before the topic is deleted.", Minimum = 1, Maximum = 365)]
        public int GraceDays
        {
            get { return _GraceDays; }
            set { _GraceDays = Math.Clamp(value, 1, 365); }
        }

        /// <summary>
        /// Days before the deletion date that a reminder is sent. Default 2, minimum 0 (no reminder), maximum 30.
        /// </summary>
        [Setting(Description = "Days before deletion to send a reminder (0 for none).", Minimum = 0, Maximum = 30)]
        public int ReminderDays
        {
            get { return _ReminderDays; }
            set { _ReminderDays = Math.Clamp(value, 0, 30); }
        }

        /// <summary>
        /// Days a Keep answer holds off the next prompt. Default 7, minimum 1, maximum 365.
        /// </summary>
        [Setting(Description = "Days a Keep answer holds off the next prompt.", Minimum = 1, Maximum = 365)]
        public int KeepDays
        {
            get { return _KeepDays; }
            set { _KeepDays = Math.Clamp(value, 1, 365); }
        }

        /// <summary>
        /// Minutes between checks. Default 60, minimum 5, maximum 1440.
        /// </summary>
        [Setting(Description = "Minutes between stale topic checks.", Minimum = 5, Maximum = 1440)]
        public int IntervalMinutes
        {
            get { return _IntervalMinutes; }
            set { _IntervalMinutes = Math.Clamp(value, 5, 1440); }
        }

        private int _StaleDays = 7;
        private int _GraceDays = 14;
        private int _ReminderDays = 2;
        private int _KeepDays = 7;
        private int _IntervalMinutes = 60;
    }
}
