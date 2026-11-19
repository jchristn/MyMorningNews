namespace MyMorningNews.Core.Briefings
{
    using MyMorningNews.Core.Models;

    /// <summary>
    /// A template ready for composition: the row, the version used, and the parsed definition.
    /// </summary>
    public class ResolvedTemplate
    {
        /// <summary>
        /// Template.
        /// </summary>
        public BriefingTemplate Template { get; set; } = new BriefingTemplate();

        /// <summary>
        /// Version used.
        /// </summary>
        public BriefingTemplateVersion Version { get; set; } = new BriefingTemplateVersion();

        /// <summary>
        /// Parsed definition.
        /// </summary>
        public BriefingTemplateDefinition Definition { get; set; } = new BriefingTemplateDefinition();
    }
}
