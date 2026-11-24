namespace MyMorningNews.Core.Responses
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Accepted support report. Repeated requests return the same report identifier and its delivery status.
    /// </summary>
    public class SupportReportResponse
    {
        /// <summary>
        /// Outbox message identifier. Default empty.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Current delivery status. Default Queued; acceptance does not imply completed delivery.
        /// </summary>
        public EmailStatusEnum Status { get; set; } = EmailStatusEnum.Queued;
    }
}
