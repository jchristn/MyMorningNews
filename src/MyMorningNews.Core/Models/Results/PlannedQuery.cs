namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A search query planned for a run.
    /// </summary>
    public class PlannedQuery
    {
        /// <summary>
        /// Query text.
        /// </summary>
        [Documentation("The search query, as a person would type it.")]
        public string Query { get; set; } = String.Empty;

        /// <summary>
        /// Dimension name.
        /// </summary>
        [Documentation("Name of the dimension it serves, or null.")]
        public string? Dimension { get; set; } = null;

        /// <summary>
        /// Why.
        /// </summary>
        [Documentation("One line on why.")]
        public string Rationale { get; set; } = String.Empty;
    }
}
