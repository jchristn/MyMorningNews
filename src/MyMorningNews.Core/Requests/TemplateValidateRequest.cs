namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Validate template Markdown without saving.
    /// </summary>
    public class TemplateValidateRequest
    {
        /// <summary>
        /// Template Markdown.
        /// </summary>
        public string Content { get; set; } = String.Empty;
    }
}
