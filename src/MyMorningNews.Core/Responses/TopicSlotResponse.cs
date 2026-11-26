namespace MyMorningNews.Core.Responses
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// One custom topic in slot order.
    /// </summary>
    public class TopicSlotResponse
    {
        /// <summary>
        /// Topic identifier.
        /// </summary>
        public string TopicId { get; set; } = String.Empty;

        /// <summary>
        /// Topic name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Topic status.
        /// </summary>
        public TopicStatusEnum Status { get; set; } = TopicStatusEnum.Active;

        /// <summary>
        /// 1-based position in the slot order.
        /// </summary>
        public int Position { get; set; } = 1;

        /// <summary>
        /// The owner chose this topic's position (otherwise it follows in creation order).
        /// </summary>
        public bool Chosen { get; set; } = false;

        /// <summary>
        /// The topic is inside the plan's custom-topic limit and can run. False topics keep their history but do not run.
        /// </summary>
        public bool WithinPlan { get; set; } = true;

        /// <summary>
        /// When the topic last ran, or null.
        /// </summary>
        public DateTime? LastRunUtc { get; set; } = null;
    }
}
