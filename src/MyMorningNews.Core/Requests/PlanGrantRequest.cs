namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Give a user a plan, as an administrator.
    /// </summary>
    public class PlanGrantRequest
    {
        /// <summary>
        /// Tenant.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// User.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Plan.
        /// </summary>
        public string? PlanId { get; set; } = null;

        /// <summary>
        /// Days; null or 0 for no end.
        /// </summary>
        public int? DurationDays { get; set; } = null;

        /// <summary>
        /// Reason.
        /// </summary>
        public string? Note { get; set; } = null;
    }
}
