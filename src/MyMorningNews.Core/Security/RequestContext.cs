namespace MyMorningNews.Core.Security
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Typed context for one request, created in pre-routing and attached to the HTTP context metadata.
    /// </summary>
    public class RequestContext
    {
        #region Public-Members

        /// <summary>
        /// Request identifier, returned in the X-Request-Id response header.
        /// </summary>
        public string RequestId { get; set; } = IdGenerator.GenerateRequestId();

        /// <summary>
        /// Caller-supplied correlation identifier (x-request header), when present.
        /// </summary>
        public string? CorrelationId { get; set; } = null;

        /// <summary>
        /// W3C trace identifier, when tracing is active.
        /// </summary>
        public string? TraceId { get; set; } = null;

        /// <summary>
        /// HTTP method.
        /// </summary>
        public string Method { get; set; } = String.Empty;

        /// <summary>
        /// URL path without the query string.
        /// </summary>
        public string Path { get; set; } = String.Empty;

        /// <summary>
        /// Source IP.
        /// </summary>
        public string? SourceIp { get; set; } = null;

        /// <summary>
        /// User agent.
        /// </summary>
        public string? UserAgent { get; set; } = null;

        /// <summary>
        /// Preferred locale from Accept-Language, when present.
        /// </summary>
        public string? Locale { get; set; } = null;

        /// <summary>
        /// UTC time the request was received.
        /// </summary>
        public DateTime ReceivedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the request is authenticated.
        /// </summary>
        public bool IsAuthenticated { get; set; } = false;

        /// <summary>
        /// Tenant identifier resolved from the authenticated material.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// User identifier (for credential principals, the owning user).
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Credential identifier, for credential principals.
        /// </summary>
        public string? CredentialId { get; set; } = null;

        /// <summary>
        /// Session identifier, for session-token requests.
        /// </summary>
        public string? SessionId { get; set; } = null;

        /// <summary>
        /// Principal email, when a user.
        /// </summary>
        public string? Email { get; set; } = null;

        /// <summary>
        /// Display name for logs and request history.
        /// </summary>
        public string? DisplayName { get; set; } = null;

        /// <summary>
        /// Global administrator (user flag or system administration key).
        /// </summary>
        public bool IsAdmin { get; set; } = false;

        /// <summary>
        /// Tenant administrator.
        /// </summary>
        public bool IsTenantAdmin { get; set; } = false;

        /// <summary>
        /// Coarse scopes for credential principals.
        /// </summary>
        public List<string> Scopes { get; set; } = new List<string>();

        /// <summary>
        /// Authentication details.
        /// </summary>
        public AuthenticationContext Authentication { get; set; } = new AuthenticationContext();

        /// <summary>
        /// Authorization details of the most recent check.
        /// </summary>
        public AuthorizationContext Authorization { get; set; } = new AuthorizationContext();

        /// <summary>
        /// Request type classified by the route handler.
        /// </summary>
        public RequestTypeEnum RequestType { get; set; } = RequestTypeEnum.Unknown;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Copy the identity fields from an authentication result.
        /// </summary>
        /// <param name="auth">Authentication result.</param>
        /// <exception cref="ArgumentNullException">auth is null.</exception>
        public void ApplyAuthentication(AuthenticationContext auth)
        {
            ArgumentNullException.ThrowIfNull(auth);
            Authentication = auth;
            IsAuthenticated = auth.IsAuthenticated;
            if (!auth.IsAuthenticated) return;
            TenantId = auth.Tenant?.Id;
            UserId = auth.User?.Id;
            CredentialId = auth.Credential?.Id;
            SessionId = auth.SessionId;
            Email = auth.User?.Email;
            IsAdmin = auth.IsSystemAdministrator || (auth.User != null && auth.User.IsAdmin && auth.Credential == null);
            IsTenantAdmin = auth.User != null && auth.User.IsTenantAdmin && auth.Credential == null;
            Scopes = auth.Credential != null ? new List<string>(auth.Credential.Scopes) : new List<string>();
            if (auth.IsSystemAdministrator) DisplayName = "system-administrator";
            else if (auth.Credential != null) DisplayName = "credential:" + auth.Credential.Name;
            else if (auth.User != null) DisplayName = auth.User.Email;
        }

        #endregion
    }
}
