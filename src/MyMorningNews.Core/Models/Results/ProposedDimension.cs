namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A dimension proposed by the refinement model.
    /// </summary>
    public class ProposedDimension
    {
        /// <summary>
        /// Short name.
        /// </summary>
        [Documentation("Short name, 2 to 6 words.")]
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// One-sentence description.
        /// </summary>
        [Documentation("One sentence describing the facet.")]
        public string Description { get; set; } = String.Empty;

        /// <summary>
        /// Why it might matter to the reader.
        /// </summary>
        [Documentation("Why this might matter to this reader.")]
        public string Rationale { get; set; } = String.Empty;

        /// <summary>
        /// Suggested weight.
        /// </summary>
        [Documentation("High, Normal, Low, or Exclude.")]
        public DimensionWeightEnum Weight { get; set; } = DimensionWeightEnum.Normal;

        /// <summary>
        /// Example include terms.
        /// </summary>
        [Documentation("A few example terms to include.")]
        public List<string> IncludeTerms { get; set; } = new List<string>();

        /// <summary>
        /// Example exclude terms.
        /// </summary>
        [Documentation("A few example terms to exclude.")]
        public List<string> ExcludeTerms { get; set; } = new List<string>();
    }
}
