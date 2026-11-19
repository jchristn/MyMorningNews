namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Output of research.dedupe-judge.
    /// </summary>
    public class DedupeJudgeResult : IStructuredResult
    {
        /// <summary>
        /// Verdict.
        /// </summary>
        [Documentation("Duplicate, Update, or Different.")]
        public DedupeVerdictEnum Verdict { get; set; } = DedupeVerdictEnum.Different;

        /// <summary>
        /// Reason.
        /// </summary>
        [Documentation("One sentence.")]
        public string Reason { get; set; } = String.Empty;

        /// <inheritdoc />
        public List<string> Validate()
        {
            List<string> errors = new List<string>();
            return errors;
        }
    }
}
