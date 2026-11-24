namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for PromptTemplateVersion records. Null properties are not applied.
    /// </summary>
    public class PromptTemplateVersionFilter : EnumerationQuery
    {
        /// <summary>
        /// Match PromptTemplateId exactly when set.
        /// </summary>
        public string? PromptTemplateId { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
