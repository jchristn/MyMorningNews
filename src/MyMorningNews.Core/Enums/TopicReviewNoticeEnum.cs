namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Which keep-or-delete notice is being sent.
    /// </summary>
    public enum TopicReviewNoticeEnum
    {
        /// <summary>
        /// First prompt: keep or delete.
        /// </summary>
        Prompt,

        /// <summary>
        /// Reminder shortly before the deletion date.
        /// </summary>
        Reminder,

        /// <summary>
        /// The topic was deleted because nobody chose Keep.
        /// </summary>
        Deleted
    }
}
