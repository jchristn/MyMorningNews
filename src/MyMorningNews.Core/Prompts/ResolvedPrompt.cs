namespace MyMorningNews.Core.Prompts
{
    using System;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// A prompt template resolved for use: the record, its current version, and the parsed definition.
    /// </summary>
    public class ResolvedPrompt
    {
        /// <summary>
        /// Template record.
        /// </summary>
        public PromptTemplate Template { get; }

        /// <summary>
        /// Current version.
        /// </summary>
        public PromptTemplateVersion Version { get; }

        /// <summary>
        /// Parsed definition.
        /// </summary>
        public PromptDefinition Definition { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="template">Template.</param>
        /// <param name="version">Version.</param>
        /// <param name="definition">Definition.</param>
        public ResolvedPrompt(PromptTemplate template, PromptTemplateVersion version, PromptDefinition definition)
        {
            Template = template ?? throw new ArgumentNullException(nameof(template));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }
    }
}
