namespace MyMorningNews.Core.Responses
{
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Whether a notification channel can deliver to the user right now.
    /// </summary>
    public class NotificationChannelStatus
    {
        /// <summary>
        /// Channel. Default Email.
        /// </summary>
        public NotificationChannelEnum Channel { get; set; } = NotificationChannelEnum.Email;

        /// <summary>
        /// Whether the channel can deliver. Default true.
        /// </summary>
        public bool Available { get; set; } = true;

        /// <summary>
        /// Why the channel cannot deliver. Null when it can.
        /// </summary>
        public NotificationUnavailableReasonEnum? Reason { get; set; } = null;
    }
}
