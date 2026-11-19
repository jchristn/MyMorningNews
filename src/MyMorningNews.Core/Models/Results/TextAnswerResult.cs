namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Minimal structured result: a single text answer. Used by tool-support probes and tests.
    /// </summary>
    public class TextAnswerResult : IStructuredResult
    {
        /// <summary>
        /// Answer.
        /// </summary>
        public string Answer { get; set; } = String.Empty;

        /// <inheritdoc />
        public List<string> Validate()
        {
            List<string> errors = new List<string>();
            if (String.IsNullOrWhiteSpace(Answer)) errors.Add("answer must not be empty.");
            return errors;
        }
    }
}
