namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// My email preferences.
    /// </summary>
    public class EmailPreferencesResponse
    {
        /// <summary>
        /// Address the preferences apply to.
        /// </summary>
        public string Address { get; set; } = String.Empty;

        /// <summary>
        /// Run digests (all topics).
        /// </summary>
        public bool RunEmails { get; set; } = true;

        /// <summary>
        /// Briefing emails.
        /// </summary>
        public bool BriefingEmails { get; set; } = true;

        /// <summary>
        /// Run failure notices.
        /// </summary>
        public bool FailureEmails { get; set; } = true;

        /// <summary>
        /// Topics whose run emails are off.
        /// </summary>
        public List<string> UnsubscribedTopicIds { get; set; } = new List<string>();
    }
}
