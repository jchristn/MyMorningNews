namespace MyMorningNews.Core.Prompts
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// A parsed prompt template: front matter plus the System and User sections.
    /// </summary>
    public class PromptDefinition
    {
        /// <summary>
        /// Prompt key.
        /// </summary>
        public string Key { get; set; } = String.Empty;

        /// <summary>
        /// Display name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Task role.
        /// </summary>
        public ModelTaskEnum Role { get; set; } = ModelTaskEnum.Synthesis;

        /// <summary>
        /// Structured output type name, or empty for text output.
        /// </summary>
        public string Output { get; set; } = String.Empty;

        /// <summary>
        /// Declared variables.
        /// </summary>
        public List<string> Variables { get; set; } = new List<string>();

        /// <summary>
        /// System section.
        /// </summary>
        public string System { get; set; } = String.Empty;

        /// <summary>
        /// User section.
        /// </summary>
        public string User { get; set; } = String.Empty;

        /// <summary>
        /// Raw content.
        /// </summary>
        public string Content { get; set; } = String.Empty;
    }
}
