namespace MyMorningNews.Core.Requests
{
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// One cell of the notification preference matrix: a kind of notification on one channel.
    /// </summary>
    public class NotificationPreferenceCell
    {
        /// <summary>
        /// Kind of notification. Default RunResults.
        /// </summary>
        public NotificationKindEnum Kind { get; set; } = NotificationKindEnum.RunResults;

        /// <summary>
        /// Channel. Default Email.
        /// </summary>
        public NotificationChannelEnum Channel { get; set; } = NotificationChannelEnum.Email;

        /// <summary>
        /// Whether the user wants this kind on this channel. Default false.
        /// </summary>
        public bool Enabled { get; set; } = false;
    }
}
