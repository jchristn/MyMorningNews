namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// A refinement question with its options.
    /// </summary>
    public class RefinementQuestionResponse
    {
        /// <summary>
        /// Question.
        /// </summary>
        public RefinementQuestion Question { get; set; } = new RefinementQuestion();

        /// <summary>
        /// Options.
        /// </summary>
        public List<RefinementQuestionOption> Options { get; set; } = new List<RefinementQuestionOption>();
    }
}
