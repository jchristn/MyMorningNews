namespace MyMorningNews.Core.Responses
{
    using System;

    /// <summary>
    /// A tenant the signing-in user belongs to.
    /// </summary>
    public class TenantChoice
    {
        /// <summary>
        /// Tenant ID (send as x-tenant-guid).
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Tenant name.
        /// </summary>
        public string Name { get; set; } = String.Empty;
    }
}
