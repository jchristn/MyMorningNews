namespace MyMorningNews.Core.Requests
{
    /// <summary>
    /// Enumeration filter for EditionPublication records. TenantId is required; null properties are not applied.
    /// </summary>
    public class EditionPublicationFilter : EnumerationQuery
    {
        /// <summary>
        /// Match EditionId exactly when set.
        /// </summary>
        public string? EditionId { get; set; } = null;

        /// <summary>
        /// Match PublicationId exactly when set.
        /// </summary>
        public string? PublicationId { get; set; } = null;
    }
}
