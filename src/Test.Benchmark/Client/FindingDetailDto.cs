namespace MyMorningNews.Benchmark.Client
{
    using System.Collections.Generic;

    /// <summary>
    /// A finding with its evidence (GET /v1.0/api/findings/{id}).
    /// </summary>
    public class FindingDetailDto
    {
        /// <summary>
        /// Finding.
        /// </summary>
        public FindingDto Finding { get; set; } = new FindingDto();

        /// <summary>
        /// Evidence.
        /// </summary>
        public List<EvidenceViewDto> Evidence { get; set; } = new List<EvidenceViewDto>();
    }
}
