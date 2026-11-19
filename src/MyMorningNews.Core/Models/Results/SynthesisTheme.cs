namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A theme across a run.
    /// </summary>
    public class SynthesisTheme
    {
        /// <summary>
        /// Theme.
        /// </summary>
        [Documentation("Theme name.")]
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Description.
        /// </summary>
        [Documentation("One line.")]
        public string Description { get; set; } = String.Empty;

        /// <summary>
        /// Supporting finding titles.
        /// </summary>
        [Documentation("Titles of supporting findings.")]
        public List<string> Findings { get; set; } = new List<string>();
    }
}
