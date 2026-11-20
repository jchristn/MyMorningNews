namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Change my email preferences. Null fields are unchanged.
    /// </summary>
    public class EmailPreferencesRequest
    {
        /// <summary>
        /// Run digests.
        /// </summary>
        public bool? RunEmails { get; set; } = null;

        /// <summary>
        /// Briefing emails.
        /// </summary>
        public bool? BriefingEmails { get; set; } = null;

        /// <summary>
        /// Run failure notices.
        /// </summary>
        public bool? FailureEmails { get; set; } = null;

        /// <summary>
        /// Topics whose run emails to turn back on.
        /// </summary>
        public List<string>? ResubscribeTopicIds { get; set; } = null;

        /// <summary>
        /// Topics whose run emails to turn off.
        /// </summary>
        public List<string>? UnsubscribeTopicIds { get; set; } = null;
    }
}
