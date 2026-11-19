namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// Prompt template with its current content.
    /// </summary>
    public class PromptTemplateResponse
    {
        /// <summary>
        /// Template record.
        /// </summary>
        public PromptTemplate Template { get; set; } = new PromptTemplate { Key = "key", Name = "name" };

        /// <summary>
        /// Current content.
        /// </summary>
        public string Content { get; set; } = String.Empty;

        /// <summary>
        /// Declared variables.
        /// </summary>
        public List<string> Variables { get; set; } = new List<string>();

        /// <summary>
        /// Shipped default content.
        /// </summary>
        public string DefaultContent { get; set; } = String.Empty;
    }
}
