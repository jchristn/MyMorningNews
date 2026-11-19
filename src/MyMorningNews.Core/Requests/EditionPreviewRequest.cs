namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Preview an edition, optionally with unsaved template Markdown.
    /// </summary>
    public class EditionPreviewRequest
    {
        /// <summary>
        /// Template Markdown to preview instead of the edition template, or null.
        /// </summary>
        public string? TemplateContent { get; set; } = null;
    }
}
