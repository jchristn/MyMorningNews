namespace Test.Shared.Helpers
{
    using MyMorningNews.Core.Models;
    using MyMorningNews.Server;

    /// <summary>
    /// A tenant administrator with the stub research model, a stub search provider, and an active topic.
    /// </summary>
    public class PipelineFixture
    {
        /// <summary>
        /// Server.
        /// </summary>
        public MyMorningNewsServer Server { get; set; } = null!;

        /// <summary>
        /// Tenant administrator who owns the topic.
        /// </summary>
        public TestUser User { get; set; } = null!;

        /// <summary>
        /// Topic.
        /// </summary>
        public Topic Topic { get; set; } = new Topic();

        /// <summary>
        /// Search provider.
        /// </summary>
        public SearchProvider Provider { get; set; } = new SearchProvider();
    }
}
