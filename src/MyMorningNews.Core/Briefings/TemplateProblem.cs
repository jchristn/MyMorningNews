namespace MyMorningNews.Core.Briefings
{
    using System;

    /// <summary>
    /// A template validation error or warning with its line.
    /// </summary>
    public class TemplateProblem
    {
        /// <summary>
        /// Line number (1-based), or 0 for the whole template.
        /// </summary>
        public int Line { get; set; } = 0;

        /// <summary>
        /// Message.
        /// </summary>
        public string Message { get; set; } = String.Empty;

        /// <summary>
        /// Warning rather than error.
        /// </summary>
        public bool IsWarning { get; set; } = false;
    }
}
