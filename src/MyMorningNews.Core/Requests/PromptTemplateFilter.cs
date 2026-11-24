namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for PromptTemplate records. Null properties are not applied.
    /// </summary>
    public class PromptTemplateFilter : EnumerationQuery
    {
        /// <summary>
        /// Match Key exactly when set.
        /// </summary>
        public string? Key { get; set; } = null;

        /// <summary>
        /// Match TopicId exactly when set.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Match Role exactly when set.
        /// </summary>
        public ModelTaskEnum? Role { get; set; } = null;

        /// <summary>
        /// Match IsCustomized exactly when set.
        /// </summary>
        public bool? IsCustomized { get; set; } = null;

        /// <summary>
        /// True for tenant prompts only (no topic overrides). Null or false for both.
        /// </summary>
        public bool? TenantOnly { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
