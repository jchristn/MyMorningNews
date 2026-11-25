namespace MyMorningNews.Core.Settings
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// The daily platform summary emailed to administrators.
    /// </summary>
    public class AdminReportSettings
    {
        /// <summary>
        /// Send the daily summary. Default true.
        /// </summary>
        [Setting(Description = "Email administrators a daily platform summary.", EnvironmentVariable = "ADMIN_REPORT_ENABLED")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Addresses that receive the summary. Default hello@latticetech.ai. Blank entries are dropped and addresses
        /// are trimmed and lowercased. The environment variable takes a comma-separated list. Never null.
        /// </summary>
        [Setting(Description = "Addresses that receive the daily summary (comma-separated in the environment).", EnvironmentVariable = "ADMIN_REPORT_RECIPIENTS")]
        public List<string> Recipients
        {
            get { return _Recipients; }
            set { _Recipients = (value ?? new List<string>()).Where(r => !String.IsNullOrWhiteSpace(r)).Select(r => r.Trim().ToLowerInvariant()).Distinct().ToList(); }
        }

        /// <summary>
        /// Hour of the day (UTC) the summary of the previous UTC day is sent. Default 14, minimum 0, maximum 23.
        /// </summary>
        [Setting(Description = "UTC hour the daily summary is sent.", Minimum = 0, Maximum = 23)]
        public int HourUtc
        {
            get { return _HourUtc; }
            set { _HourUtc = Math.Clamp(value, 0, 23); }
        }

        private List<string> _Recipients = new List<string> { "hello@latticetech.ai" };
        private int _HourUtc = 14;
    }
}
