namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Create or update a tenant.
    /// </summary>
    public class TenantUpsertRequest
    {
        /// <summary>
        /// Name (required on create).
        /// </summary>
        public string? Name { get; set; } = null;

        /// <summary>
        /// Description.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Region.
        /// </summary>
        public string? Region { get; set; } = null;

        /// <summary>
        /// IANA time zone.
        /// </summary>
        public string? TimeZone { get; set; } = null;

        /// <summary>
        /// Active.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
