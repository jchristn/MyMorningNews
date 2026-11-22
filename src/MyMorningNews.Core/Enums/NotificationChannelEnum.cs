namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Where a notification is delivered.
    /// </summary>
    public enum NotificationChannelEnum
    {
        /// <summary>
        /// Email.
        /// </summary>
        Email,

        /// <summary>
        /// Push notification on a registered phone or tablet.
        /// </summary>
        Push,

        /// <summary>
        /// The notification list in the dashboard and the mobile app.
        /// </summary>
        InApp
    }
}
