namespace MyMorningNews.Benchmark.Client
{
    using System;

    /// <summary>
    /// Evidence with its source, as returned with a finding.
    /// </summary>
    public class EvidenceViewDto
    {
        /// <summary>
        /// Evidence.
        /// </summary>
        public EvidenceDto Evidence { get; set; } = new EvidenceDto();

        /// <summary>
        /// Source URL, or null.
        /// </summary>
        public string? SourceUrl { get; set; } = null;
    }
}
