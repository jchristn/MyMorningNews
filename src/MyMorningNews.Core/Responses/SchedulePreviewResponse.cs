namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Validation result and upcoming runs for a schedule.
    /// </summary>
    public class SchedulePreviewResponse
    {
        /// <summary>
        /// Whether the schedule is valid.
        /// </summary>
        public bool Valid { get; set; } = false;

        /// <summary>
        /// Validation problems.
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Upcoming runs (UTC).
        /// </summary>
        public List<DateTime> NextRunsUtc { get; set; } = new List<DateTime>();

        /// <summary>
        /// Upcoming runs in the schedule time zone, ISO 8601 with offset.
        /// </summary>
        public List<string> NextRunsLocal { get; set; } = new List<string>();
    }
}
