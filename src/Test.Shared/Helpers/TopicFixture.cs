namespace Test.Shared.Helpers
{
    using MyMorningNews.Core.Models;

    /// <summary>
    /// A member with an active topic.
    /// </summary>
    public class TopicFixture
    {
        /// <summary>
        /// Topic owner.
        /// </summary>
        public TestUser User { get; set; } = null!;

        /// <summary>
        /// Topic.
        /// </summary>
        public Topic Topic { get; set; } = new Topic();
    }
}
