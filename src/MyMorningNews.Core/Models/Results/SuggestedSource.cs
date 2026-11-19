namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A feed or seed page suggested by the refinement model.
    /// </summary>
    public class SuggestedSource
    {
        /// <summary>
        /// URL.
        /// </summary>
        [Documentation("Absolute https URL.")]
        public string Url { get; set; } = String.Empty;

        /// <summary>
        /// One-line reason.
        /// </summary>
        [Documentation("One line on why it is useful.")]
        public string Reason { get; set; } = String.Empty;

        /// <summary>
        /// Dimension it serves, when any.
        /// </summary>
        [Documentation("Name of the dimension it serves, or null.")]
        public string? Dimension { get; set; } = null;
    }
}
