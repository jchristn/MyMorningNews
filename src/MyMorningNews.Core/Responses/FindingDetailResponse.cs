namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// A finding with its evidence, source, and dimension.
    /// </summary>
    public class FindingDetailResponse
    {
        /// <summary>
        /// Finding.
        /// </summary>
        public Finding Finding { get; set; } = new Finding();

        /// <summary>
        /// Evidence.
        /// </summary>
        public List<EvidenceView> Evidence { get; set; } = new List<EvidenceView>();

        /// <summary>
        /// Dimension.
        /// </summary>
        public TopicDimension? Dimension { get; set; } = null;

        /// <summary>
        /// Topic name.
        /// </summary>
        public string? TopicName { get; set; } = null;
    }
}
