namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Create a briefing template from Markdown.
    /// </summary>
    public class TemplateCreateRequest
    {
        /// <summary>
        /// Template Markdown.
        /// </summary>
        public string Content { get; set; } = String.Empty;

        /// <summary>
        /// Name override; null uses the front matter name.
        /// </summary>
        [TextLimit(128)]
        public string? Name { get; set; } = null;

        /// <summary>
        /// Tenant template everyone can use (tenant administrators only).
        /// </summary>
        public bool Shared { get; set; } = false;
    }
}
