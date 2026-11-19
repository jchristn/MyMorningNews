namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Confirm a one-time request so it runs.
    /// </summary>
    public class ResearchRequestConfirmRequest
    {
        /// <summary>
        /// Run once at this time (UTC); null runs as soon as possible.
        /// </summary>
        public DateTime? RunAtUtc { get; set; } = null;
    }
}
