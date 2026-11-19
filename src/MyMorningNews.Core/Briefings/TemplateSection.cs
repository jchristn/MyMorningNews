namespace MyMorningNews.Core.Briefings
{
    using System;

    /// <summary>
    /// One ## section of a template.
    /// </summary>
    public class TemplateSection
    {
        /// <summary>
        /// Stable key derived from the heading.
        /// </summary>
        public string Key { get; set; } = String.Empty;

        /// <summary>
        /// Heading text.
        /// </summary>
        public string Heading { get; set; } = String.Empty;

        /// <summary>
        /// Text under the heading, passed to the composer.
        /// </summary>
        public string Instruction { get; set; } = String.Empty;

        /// <summary>
        /// Directive.
        /// </summary>
        public SectionDirective Directive { get; set; } = new SectionDirective();

        /// <summary>
        /// Line number of the heading (1-based).
        /// </summary>
        public int Line { get; set; } = 0;
    }
}
