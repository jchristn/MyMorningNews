namespace MyMorningNews.Core.Services
{
    /// <summary>
    /// What one notification did on each channel.
    /// </summary>
    public class NotificationResult
    {
        /// <summary>
        /// Emails queued. Default 0.
        /// </summary>
        public int EmailsQueued { get; set; } = 0;

        /// <summary>
        /// Devices that accepted the push. Default 0.
        /// </summary>
        public int PushAccepted { get; set; } = 0;

        /// <summary>
        /// Whether an in-app notification was created. Default false.
        /// </summary>
        public bool InApp { get; set; } = false;
    }
}
