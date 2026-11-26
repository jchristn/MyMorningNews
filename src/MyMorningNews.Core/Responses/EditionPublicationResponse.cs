namespace MyMorningNews.Core.Responses
{
    using System;

    /// <summary>
    /// One catalog publication in an edition.
    /// </summary>
    public class EditionPublicationResponse
    {
        /// <summary>
        /// Publication identifier.
        /// </summary>
        public string PublicationId { get; set; } = String.Empty;

        /// <summary>
        /// Publication title, or empty when it is no longer readable.
        /// </summary>
        public string Title { get; set; } = String.Empty;

        /// <summary>
        /// Order within the edition, starting at 0.
        /// </summary>
        public int SortOrder { get; set; } = 0;

        /// <summary>
        /// False when the publication was retracted, the subscription ended, or the plan no longer allows custom
        /// composition. Unavailable publications are skipped when the edition is composed.
        /// </summary>
        public bool Available { get; set; } = true;
    }
}
