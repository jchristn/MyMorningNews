namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Where a keep-or-delete review of a stale topic stands.
    /// </summary>
    public enum TopicReviewStatusEnum
    {
        /// <summary>
        /// The owner was asked and has not answered yet.
        /// </summary>
        Pending,

        /// <summary>
        /// The owner chose to keep the topic.
        /// </summary>
        Kept,

        /// <summary>
        /// The owner deleted the topic from the prompt.
        /// </summary>
        Deleted,

        /// <summary>
        /// The topic produced new findings again before the deadline, so the review closed by itself.
        /// </summary>
        Recovered,

        /// <summary>
        /// Nobody chose Keep before the deadline, so the topic was deleted automatically.
        /// </summary>
        AutoDeleted
    }
}
