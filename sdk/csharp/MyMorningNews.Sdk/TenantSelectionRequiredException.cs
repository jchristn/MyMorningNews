namespace MyMorningNews.Sdk
{
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Models;

    /// <summary>
    /// Login matched the email in more than one tenant (HTTP 409). Sign in again with one of Tenants.
    /// </summary>
    public class TenantSelectionRequiredException : MyMorningNewsException
    {
        /// <summary>
        /// Tenants the caller can choose from. Never null.
        /// </summary>
        public IReadOnlyList<TenantChoice> Tenants { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="tenants">Tenant choices, or null.</param>
        /// <param name="responseBody">Raw body, or null.</param>
        public TenantSelectionRequiredException(string message, IReadOnlyList<TenantChoice>? tenants, string? responseBody)
            : base(409, "TenantSelectionRequired", message, null, null, responseBody)
        {
            Tenants = tenants ?? new List<TenantChoice>();
        }
    }
}
