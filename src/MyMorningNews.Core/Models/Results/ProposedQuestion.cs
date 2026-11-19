namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A clarifying question proposed by the refinement model.
    /// </summary>
    public class ProposedQuestion
    {
        /// <summary>
        /// Question text.
        /// </summary>
        [Documentation("The question.")]
        public string Question { get; set; } = String.Empty;

        /// <summary>
        /// One-line reason for asking.
        /// </summary>
        [Documentation("One line on why the answer changes the research.")]
        public string Rationale { get; set; } = String.Empty;

        /// <summary>
        /// Question kind.
        /// </summary>
        [Documentation("SingleChoice, MultiChoice, Scale, YesNo, or FreeText.")]
        public QuestionKindEnum Kind { get; set; } = QuestionKindEnum.SingleChoice;

        /// <summary>
        /// Options for choice questions.
        /// </summary>
        [Documentation("Options for SingleChoice and MultiChoice questions; empty otherwise.")]
        public List<string> Options { get; set; } = new List<string>();

        /// <summary>
        /// Name of the dimension it concerns, when any.
        /// </summary>
        [Documentation("Name of the dimension the question concerns, or null.")]
        public string? Dimension { get; set; } = null;
    }
}
