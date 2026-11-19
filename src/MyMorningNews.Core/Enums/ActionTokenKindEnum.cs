namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// What an email link token does.
    /// </summary>
    public enum ActionTokenKindEnum
    {
        /// <summary>
        /// Rate an item.
        /// </summary>
        Feedback,

        /// <summary>
        /// Stop a category of email.
        /// </summary>
        Unsubscribe
    }
}
