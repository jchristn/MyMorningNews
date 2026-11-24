namespace MyMorningNews.Core.Requests
{
    /// <summary>Canonical research configuration. The publication determines the protected owner; caller-supplied owner IDs are ignored.</summary>
    public class CatalogResearchRequest
    {
        /// <summary>Research scope, history and audio controls. Null leaves the topic unchanged.</summary>
        public TopicUpsertRequest? Topic { get; set; } = null;

        /// <summary>Canonical research schedule. Null leaves scheduling unchanged.</summary>
        public ScheduleUpsertRequest? Schedule { get; set; } = null;
    }
}
