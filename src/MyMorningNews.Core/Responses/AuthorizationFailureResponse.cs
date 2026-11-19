namespace MyMorningNews.Core.Responses
{
    using System;

    /// <summary>
    /// Body returned when authorization denies a request. Never lists the permissions evaluated.
    /// </summary>
    public class AuthorizationFailureResponse : ErrorResponse
    {
        /// <summary>
        /// Short explanation.
        /// </summary>
        public string Reason { get; set; } = String.Empty;

        /// <summary>
        /// Permission the request needed, for example Topic:Read.
        /// </summary>
        public string RequiredPermission { get; set; } = String.Empty;

        /// <summary>
        /// Classified request type.
        /// </summary>
        public string RequestType { get; set; } = String.Empty;

        /// <summary>
        /// Tenant, when known.
        /// </summary>
        public string? TenantGuid { get; set; } = null;

        /// <summary>
        /// Resource, when known.
        /// </summary>
        public string? ResourceGuid { get; set; } = null;
    }
}
