namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Answer a refinement question.
    /// </summary>
    public class QuestionAnswerRequest
    {
        /// <summary>
        /// Free-text, scale (1 to 5), or yes/no answer.
        /// </summary>
        public string? AnswerText { get; set; } = null;

        /// <summary>
        /// Selected option IDs for choice questions.
        /// </summary>
        public List<string>? SelectedOptionIds { get; set; } = null;

        /// <summary>
        /// Skip the question.
        /// </summary>
        public bool Skipped { get; set; } = false;
    }
}
