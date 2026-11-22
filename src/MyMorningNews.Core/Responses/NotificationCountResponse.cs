namespace MyMorningNews.Core.Responses
{
    /// <summary>
    /// A number of notifications: how many are unread, or how many were marked read.
    /// </summary>
    public class NotificationCountResponse
    {
        /// <summary>
        /// Count. Minimum 0.
        /// </summary>
        public long Count { get; set; } = 0;
    }
}
