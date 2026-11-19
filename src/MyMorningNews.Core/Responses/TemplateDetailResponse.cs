namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Briefings;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// A briefing template with its current content and parsed outline.
    /// </summary>
    public class TemplateDetailResponse
    {
        /// <summary>
        /// Template.
        /// </summary>
        public BriefingTemplate Template { get; set; } = new BriefingTemplate();

        /// <summary>
        /// Current content.
        /// </summary>
        public string Content { get; set; } = String.Empty;

        /// <summary>
        /// Warnings and errors.
        /// </summary>
        public List<TemplateProblem> Problems { get; set; } = new List<TemplateProblem>();

        /// <summary>
        /// Parsed sections.
        /// </summary>
        public List<TemplateSection> Sections { get; set; } = new List<TemplateSection>();

        /// <summary>
        /// Tenant template everyone can use.
        /// </summary>
        public bool Shared { get; set; } = false;

        /// <summary>
        /// Title line with variables.
        /// </summary>
        public string Title { get; set; } = String.Empty;

        /// <summary>
        /// Audio options from the front matter.
        /// </summary>
        public TemplateAudioOptions Audio { get; set; } = new TemplateAudioOptions();
    }
}
