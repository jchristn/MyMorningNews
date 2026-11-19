namespace MyMorningNews.Core.Security
{
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Result of authorizing a request.
    /// </summary>
    public class AuthorizationContext
    {
        /// <summary>
        /// Outcome.
        /// </summary>
        public AuthorizationOutcomeEnum Outcome { get; set; } = AuthorizationOutcomeEnum.DeniedImplicit;

        /// <summary>
        /// Request type evaluated.
        /// </summary>
        public RequestTypeEnum RequestType { get; set; } = RequestTypeEnum.Unknown;

        /// <summary>
        /// Required resource type.
        /// </summary>
        public ResourceTypeEnum ResourceType { get; set; } = ResourceTypeEnum.All;

        /// <summary>
        /// Required operation.
        /// </summary>
        public OperationTypeEnum Operation { get; set; } = OperationTypeEnum.Read;

        /// <summary>
        /// Target resource identifier, when any.
        /// </summary>
        public string? ResourceId { get; set; } = null;

        /// <summary>
        /// Grants that matched (for audit; never returned to callers).
        /// </summary>
        public List<EffectiveGrant> Matched { get; set; } = new List<EffectiveGrant>();

        /// <summary>
        /// Bypass reason when an administrative bypass applied.
        /// </summary>
        public string? BypassReason { get; set; } = null;

        /// <summary>
        /// Denial reason.
        /// </summary>
        public string? Reason { get; set; } = null;

        /// <summary>
        /// Whether the request is permitted.
        /// </summary>
        public bool IsPermitted
        {
            get { return Outcome == AuthorizationOutcomeEnum.Permitted; }
        }
    }
}
