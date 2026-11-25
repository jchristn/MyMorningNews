namespace MyMorningNews.Core.Services
{
    /// <summary>
    /// What one pass of the stale topic check did.
    /// </summary>
    public class TopicLifecycleResult
    {
        /// <summary>
        /// Owners asked to keep or delete a topic.
        /// </summary>
        public int Prompted { get; set; } = 0;

        /// <summary>
        /// Reminders sent before a deletion date.
        /// </summary>
        public int Reminded { get; set; } = 0;

        /// <summary>
        /// Reviews closed because the topic found something new again.
        /// </summary>
        public int Recovered { get; set; } = 0;

        /// <summary>
        /// Topics deleted because nobody chose Keep.
        /// </summary>
        public int Deleted { get; set; } = 0;
    }
}
