namespace MyMorningNews.Benchmark.Client
{
    /// <summary>
    /// A briefing with its content (GET /v1.0/api/briefings/{id}).
    /// </summary>
    public class BriefingDetailDto
    {
        /// <summary>
        /// Briefing, including its Markdown.
        /// </summary>
        public BriefingDto Briefing { get; set; } = new BriefingDto();
    }
}
