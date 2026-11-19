namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Output of research.synthesize.
    /// </summary>
    public class SynthesisResult : IStructuredResult
    {
        /// <summary>
        /// Run summary.
        /// </summary>
        [Documentation("3 to 6 sentences.")]
        public string Summary { get; set; } = String.Empty;

        /// <summary>
        /// Themes.
        /// </summary>
        [Documentation("Up to 5 themes.")]
        public List<SynthesisTheme> Themes { get; set; } = new List<SynthesisTheme>();

        /// <inheritdoc />
        public List<string> Validate()
        {
            List<string> errors = new List<string>();
            if (String.IsNullOrWhiteSpace(Summary)) errors.Add("summary must not be empty.");
            if (Themes == null) Themes = new List<SynthesisTheme>();
            Themes = Themes.Take(5).ToList();
            return errors;
        }
    }
}
