namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Append-only security event: authentication, sessions, RBAC changes, denials, bypasses, settings changes.
    /// </summary>
    public class AuditRecord
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix aud_).
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Id
        {
            get
            {
                return _Id;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Id));
                _Id = value;
            }
        }

        /// <summary>
        /// Tenant identifier. May be null.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Event type.
        /// </summary>
        public AuditEventTypeEnum EventType { get; set; } = AuditEventTypeEnum.AuthSuccess;

        /// <summary>
        /// Request identifier. May be null.
        /// </summary>
        public string? RequestId { get; set; } = null;

        /// <summary>
        /// Correlation identifier. May be null.
        /// </summary>
        public string? CorrelationId { get; set; } = null;

        /// <summary>
        /// Trace identifier. May be null.
        /// </summary>
        public string? TraceId { get; set; } = null;

        /// <summary>
        /// Authentication scheme name. May be null.
        /// </summary>
        public string? AuthScheme { get; set; } = null;

        /// <summary>
        /// Principal type name. May be null.
        /// </summary>
        public string? PrincipalType { get; set; } = null;

        /// <summary>
        /// Principal identifier. May be null.
        /// </summary>
        public string? PrincipalId { get; set; } = null;

        /// <summary>
        /// Session identifier. May be null.
        /// </summary>
        public string? SessionId { get; set; } = null;

        /// <summary>
        /// User identifier. May be null.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Credential identifier. May be null.
        /// </summary>
        public string? CredentialId { get; set; } = null;

        /// <summary>
        /// Resource type. May be null.
        /// </summary>
        public string? ResourceType { get; set; } = null;

        /// <summary>
        /// Resource identifier. May be null.
        /// </summary>
        public string? ResourceId { get; set; } = null;

        /// <summary>
        /// Classified request type. May be null.
        /// </summary>
        public string? RequestType { get; set; } = null;

        /// <summary>
        /// HTTP method. May be null.
        /// </summary>
        public string? Method { get; set; } = null;

        /// <summary>
        /// URL path. May be null.
        /// </summary>
        public string? Path { get; set; } = null;

        /// <summary>
        /// Source IP. May be null.
        /// </summary>
        public string? SourceIp { get; set; } = null;

        /// <summary>
        /// Authentication outcome. May be null.
        /// </summary>
        public string? AuthenticationResult { get; set; } = null;

        /// <summary>
        /// Authorization outcome. May be null.
        /// </summary>
        public string? AuthorizationResult { get; set; } = null;

        /// <summary>
        /// Denial or event reason. May be null.
        /// </summary>
        public string? Reason { get; set; } = null;

        /// <summary>
        /// Bypass reason for administrative paths. May be null.
        /// </summary>
        public string? BypassReason { get; set; } = null;

        /// <summary>
        /// Permission the request required. May be null.
        /// </summary>
        public string? RequiredPermission { get; set; } = null;

        /// <summary>
        /// Response status code.
        /// </summary>
        public int StatusCode { get; set; } = 0;

        /// <summary>
        /// Short description. May be null.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Whether the record is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// UTC creation time.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC time of the last update.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        #endregion

        #region Private-Members

        private string _Id = IdGenerator.GenerateAuditRecordId();

        #endregion
    }
}
