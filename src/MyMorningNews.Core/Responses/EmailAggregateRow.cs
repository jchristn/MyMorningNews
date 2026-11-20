namespace MyMorningNews.Core.Responses
{
    using System;

    /// <summary>
    /// One group of email messages: a creation day, template, and status, with a count and the mean time to send.
    /// </summary>
    public class EmailAggregateRow
    {
        /// <summary>
        /// Creation day (UTC), yyyy-MM-dd.
        /// </summary>
        public string Day { get; set; } = String.Empty;

        /// <summary>
        /// Template name.
        /// </summary>
        public string Template { get; set; } = String.Empty;

        /// <summary>
        /// Status name.
        /// </summary>
        public string Status { get; set; } = String.Empty;

        /// <summary>
        /// Messages in the group.
        /// </summary>
        public long Count { get; set; } = 0;

        /// <summary>
        /// Mean seconds from queueing to sending, for sent messages. Null when none were sent.
        /// </summary>
        public double? MeanSecondsToSend { get; set; } = null;
    }
}
