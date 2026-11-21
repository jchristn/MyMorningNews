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
        [TextLimit(128, AllowBlank = false)]
        public string? Name { get; set; } = null;

        /// <summary>
        /// Description.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Region.
        /// </summary>
        [TextLimit(64)]
        public string? Region { get; set; } = null;

        /// <summary>
        /// IANA time zone.
        /// </summary>
        [TextLimit(64)]
        public string? TimeZone { get; set; } = null;

        /// <summary>
        /// Active.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
