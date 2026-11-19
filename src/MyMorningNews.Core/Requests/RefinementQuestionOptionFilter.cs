namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for RefinementQuestionOption records. Null properties are not applied.
    /// </summary>
    public class RefinementQuestionOptionFilter : EnumerationFilter
    {
        /// <summary>
        /// Match QuestionId exactly when set.
        /// </summary>
        public string? QuestionId { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
