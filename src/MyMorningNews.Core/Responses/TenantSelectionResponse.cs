namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Returned with status 409 when a sign-in without x-tenant-guid matches more than one tenant.
    /// </summary>
    public class TenantSelectionResponse
    {
        /// <summary>
        /// Error code: TenantSelectionRequired.
        /// </summary>
        public string Error { get; set; } = "TenantSelectionRequired";

        /// <summary>
        /// Message.
        /// </summary>
        public string Message { get; set; } = "Choose a tenant and sign in again with x-tenant-guid.";

        /// <summary>
        /// Tenants the credentials are valid in.
        /// </summary>
        public List<TenantChoice> Tenants { get; set; } = new List<TenantChoice>();
    }
}
