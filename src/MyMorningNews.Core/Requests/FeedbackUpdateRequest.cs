namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Edit the reason of a feedback item.
    /// </summary>
    public class FeedbackUpdateRequest
    {
        /// <summary>
        /// Reason; empty clears it.
        /// </summary>
        public string? Reason { get; set; } = null;
    }
}
