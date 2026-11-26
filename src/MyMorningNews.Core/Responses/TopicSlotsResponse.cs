namespace MyMorningNews.Core.Responses
{
    using System;

    /// <summary>
    /// The account's custom topic count against its plan limit (plan section 8.4). The topics themselves, in slot order,
    /// are enumerated from GET /v1.0/api/account/topic-slots/topics.
    /// </summary>
    public class TopicSlotsResponse
    {
        /// <summary>
        /// Custom topics the plan allows, or null for unlimited. Zero when custom research is not in the plan.
        /// </summary>
        public long? Limit { get; set; } = null;

        /// <summary>
        /// Recurring, non-archived topics. Paused topics count; archived topics do not.
        /// </summary>
        public long Counted { get; set; } = 0;

        /// <summary>
        /// Topics past the limit: they keep their history but do not run, and no topic can be created, duplicated, or
        /// restored until enough are archived or the plan grows. Zero when within the limit.
        /// </summary>
        public long OverLimit { get; set; } = 0;

        /// <summary>
        /// The owner has chosen a keep order.
        /// </summary>
        public bool Customized { get; set; } = false;
    }
}
