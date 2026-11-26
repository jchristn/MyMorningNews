namespace MyMorningNews.Core.Responses
{
    using System;

    /// <summary>
    /// One earlier issue a briefing is, or would be, compared with.
    /// </summary>
    public class ComparisonBaseline
    {
        /// <summary>
        /// Briefing identifier.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Title, or empty when the issue is no longer available.
        /// </summary>
        public string Title { get; set; } = String.Empty;

        /// <summary>
        /// When the issue was created, or null when it is no longer available.
        /// </summary>
        public DateTime? CreatedUtc { get; set; } = null;

        /// <summary>
        /// False when the issue was deleted after the comparison was made.
        /// </summary>
        public bool Available { get; set; } = true;
    }
}
