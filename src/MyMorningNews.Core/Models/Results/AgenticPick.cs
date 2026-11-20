namespace MyMorningNews.Core.Models.Results
{
    using System;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A page the agentic search chose for reading.
    /// </summary>
    public class AgenticPick
    {
        /// <summary>
        /// Page URL (http or https).
        /// </summary>
        [Documentation("The page URL, exactly as found (http or https).")]
        public string Url { get; set; } = String.Empty;

        /// <summary>
        /// Page title.
        /// </summary>
        [Documentation("The page title.")]
        public string? Title { get; set; } = null;

        /// <summary>
        /// Dimension it serves.
        /// </summary>
        [Documentation("The topic dimension this page serves, by name.")]
        public string? Dimension { get; set; } = null;

        /// <summary>
        /// Why it is worth reading.
        /// </summary>
        [Documentation("One line on why it is worth reading.")]
        public string? Reason { get; set; } = null;
    }
}
