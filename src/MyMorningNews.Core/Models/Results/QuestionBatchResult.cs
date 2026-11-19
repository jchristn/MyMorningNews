namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Output of refinement.questions and refinement.followup.
    /// </summary>
    public class QuestionBatchResult : IStructuredResult
    {
        /// <summary>
        /// Questions for this round.
        /// </summary>
        [Documentation("Questions for this round.")]
        public List<ProposedQuestion> Questions { get; set; } = new List<ProposedQuestion>();

        /// <summary>
        /// Whether further questions would add little.
        /// </summary>
        [Documentation("True when further questions would add little.")]
        public bool SufficientContext { get; set; } = false;

        /// <inheritdoc />
        public List<string> Validate()
        {
            List<string> errors = new List<string>();
            if (Questions == null) { errors.Add("questions must be present."); return errors; }
            if (Questions.Count == 0 && !SufficientContext) errors.Add("questions must not be empty unless sufficientContext is true.");
            if (Questions.Count > 8) Questions = Questions.Take(8).ToList();
            for (int i = 0; i < Questions.Count; i++)
            {
                ProposedQuestion q = Questions[i];
                if (String.IsNullOrWhiteSpace(q.Question)) errors.Add("questions[" + i + "].question must not be empty.");
                if ((q.Kind == QuestionKindEnum.SingleChoice || q.Kind == QuestionKindEnum.MultiChoice) && (q.Options == null || q.Options.Count < 2))
                    errors.Add("questions[" + i + "] is a choice question and needs at least two options.");
            }
            return errors;
        }
    }
}
