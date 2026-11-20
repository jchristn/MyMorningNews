namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Redeem a code for a user, as an administrator.
    /// </summary>
    public class PromoApplyRequest
    {
        /// <summary>
        /// Tenant.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// User.
        /// </summary>
        public string? UserId { get; set; } = null;
    }
}
