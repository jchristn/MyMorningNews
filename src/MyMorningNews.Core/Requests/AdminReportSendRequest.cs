namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Send the daily administrator summary now.
    /// </summary>
    public class AdminReportSendRequest
    {
        /// <summary>
        /// UTC day to summarize, or null for yesterday.
        /// </summary>
        public DateTime? DayUtc { get; set; } = null;

        /// <summary>
        /// Addresses, or null for adminReports.recipients.
        /// </summary>
        public List<string>? Recipients { get; set; } = null;
    }
}
