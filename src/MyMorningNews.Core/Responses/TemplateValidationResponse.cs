namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Briefings;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// Result of validating template Markdown.
    /// </summary>
    public class TemplateValidationResponse
    {
        /// <summary>
        /// No errors.
        /// </summary>
        public bool Valid { get; set; } = false;

        /// <summary>
        /// Name from front matter.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Title line.
        /// </summary>
        public string Title { get; set; } = String.Empty;

        /// <summary>
        /// Errors and warnings with lines.
        /// </summary>
        public List<TemplateProblem> Problems { get; set; } = new List<TemplateProblem>();

        /// <summary>
        /// Parsed sections.
        /// </summary>
        public List<TemplateSection> Sections { get; set; } = new List<TemplateSection>();

        /// <summary>
        /// Audio options.
        /// </summary>
        public TemplateAudioOptions Audio { get; set; } = new TemplateAudioOptions();
    }
}
