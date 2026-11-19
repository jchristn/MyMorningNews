namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Change a suggestion status.
    /// </summary>
    public class SuggestionStatusRequest
    {
        /// <summary>
        /// New status.
        /// </summary>
        public SuggestionStatusEnum Status { get; set; } = SuggestionStatusEnum.Accepted;

        /// <summary>
        /// Snooze until (for Snoozed).
        /// </summary>
        public DateTime? SnoozedUntilUtc { get; set; } = null;
    }
}
