namespace MyMorningNews.Benchmark.Client
{
    using System;

    /// <summary>
    /// A topic.
    /// </summary>
    public class TopicDto
    {
        /// <summary>
        /// Identifier.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Tenant.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Owner.
        /// </summary>
        public string UserId { get; set; } = String.Empty;

        /// <summary>
        /// Name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Status.
        /// </summary>
        public string Status { get; set; } = String.Empty;
    }
}
