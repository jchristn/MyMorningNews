namespace MyMorningNews.Core.Briefings
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// A briefing template has errors. The REST layer returns 400 with the problems.
    /// </summary>
    public class TemplateInvalidException : ArgumentException
    {
        /// <summary>
        /// Errors and warnings with line numbers.
        /// </summary>
        public List<TemplateProblem> Problems { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="problems">Problems.</param>
        public TemplateInvalidException(List<TemplateProblem> problems)
            : base("The template has errors: " + String.Join(" ", (problems ?? new List<TemplateProblem>()).Where(p => !p.IsWarning).Take(5).Select(p => (p.Line > 0 ? "Line " + p.Line + ": " : String.Empty) + p.Message)))
        {
            Problems = problems ?? new List<TemplateProblem>();
        }
    }
}
