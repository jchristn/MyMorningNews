namespace MyMorningNews.Core.Requests
{
    using System.Collections.Generic;

    /// <summary>
    /// Which custom topics keep running when the plan allows fewer than the account has, in keep order.
    /// </summary>
    public class TopicSlotsRequest
    {
        /// <summary>
        /// Recurring, non-archived topic IDs of the caller, most important first. Topics not listed follow in creation
        /// order. Empty clears the choice. Duplicates and unknown topics are rejected.
        /// </summary>
        public List<string> KeepTopicIds { get; set; } = new List<string>();
    }
}
