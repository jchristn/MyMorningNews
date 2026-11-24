namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for RefinementQuestion records. Null properties are not applied.
    /// </summary>
    public class RefinementQuestionFilter : EnumerationQuery
    {
        /// <summary>
        /// Match SessionId exactly when set.
        /// </summary>
        public string? SessionId { get; set; } = null;

        /// <summary>
        /// Match Round exactly when set.
        /// </summary>
        public int? Round { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
