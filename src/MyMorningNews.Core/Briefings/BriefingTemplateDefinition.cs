namespace MyMorningNews.Core.Briefings
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A parsed briefing template (plan section 21.1).
    /// </summary>
    public class BriefingTemplateDefinition
    {
        /// <summary>
        /// Name from front matter.
        /// </summary>
        public string Name { get; set; } = "Briefing";

        /// <summary>
        /// Description.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Tone. Default direct, plain, no hype.
        /// </summary>
        public string Tone { get; set; } = "direct, plain, no hype";

        /// <summary>
        /// short, medium, or long. Default medium.
        /// </summary>
        public string Length { get; set; } = "medium";

        /// <summary>
        /// Audience. Default practitioner.
        /// </summary>
        public string Audience { get; set; } = "practitioner";

        /// <summary>
        /// numbered, inline, or none. Default numbered.
        /// </summary>
        public string Citations { get; set; } = "numbered";

        /// <summary>
        /// Audio options.
        /// </summary>
        public TemplateAudioOptions Audio { get; set; } = new TemplateAudioOptions();

        /// <summary>
        /// Title line with variables.
        /// </summary>
        public string Title { get; set; } = "{{edition.name}} for {{date.long}}";

        /// <summary>
        /// Instruction for the opening paragraph (text between the title and the first section).
        /// </summary>
        public string OpeningInstruction { get; set; } = String.Empty;

        /// <summary>
        /// Sections in order.
        /// </summary>
        public List<TemplateSection> Sections { get; set; } = new List<TemplateSection>();

        /// <summary>
        /// Validation errors and warnings.
        /// </summary>
        public List<TemplateProblem> Problems { get; set; } = new List<TemplateProblem>();
    }
}
