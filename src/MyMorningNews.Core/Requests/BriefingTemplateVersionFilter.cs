namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for BriefingTemplateVersion records. Null properties are not applied.
    /// </summary>
    public class BriefingTemplateVersionFilter : EnumerationFilter
    {
        /// <summary>
        /// Match TemplateId exactly when set.
        /// </summary>
        public string? TemplateId { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
