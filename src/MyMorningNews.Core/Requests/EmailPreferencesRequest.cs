namespace MyMorningNews.Core.Requests
{
    using System;

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
    }
}
