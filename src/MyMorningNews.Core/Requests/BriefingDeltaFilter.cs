namespace MyMorningNews.Core.Requests
{
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for BriefingDelta records. TenantId is required; null properties are not applied.
    /// </summary>
    public class BriefingDeltaFilter : EnumerationQuery
    {
        /// <summary>
        /// Match BriefingId exactly when set.
        /// </summary>
        public string? BriefingId { get; set; } = null;

        /// <summary>
        /// Match Kind exactly when set.
        /// </summary>
        public BriefingDeltaKindEnum? Kind { get; set; } = null;
    }
}
