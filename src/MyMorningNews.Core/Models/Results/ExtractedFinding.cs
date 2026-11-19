namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A candidate finding extracted from one document.
    /// </summary>
    public class ExtractedFinding
    {
        /// <summary>
        /// Title.
        /// </summary>
        [Documentation("Under 15 words.")]
        public string Title { get; set; } = String.Empty;

        /// <summary>
        /// Summary.
        /// </summary>
        [Documentation("Two sentences.")]
        public string Summary { get; set; } = String.Empty;

        /// <summary>
        /// Why it matters.
        /// </summary>
        [Documentation("Why it matters to this reader.")]
        public string WhyItMatters { get; set; } = String.Empty;

        /// <summary>
        /// Dimension name.
        /// </summary>
        [Documentation("Name of the dimension, or null.")]
        public string? Dimension { get; set; } = null;

        /// <summary>
        /// Category.
        /// </summary>
        [Documentation("News, Release, Research, Opinion, Tutorial, Data, or Other.")]
        public FindingCategoryEnum Category { get; set; } = FindingCategoryEnum.News;

        /// <summary>
        /// Verbatim quotes.
        /// </summary>
        [Documentation("1 to 3 quotes copied exactly from the text.")]
        public List<string> Quotes { get; set; } = new List<string>();
    }
}
