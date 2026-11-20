namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A plan given to a user for a period: by default, an administrator, a promotional code, or billing later.
    /// </summary>
    public class PlanAssignment
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix pla_).
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
        /// Tenant identifier.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string TenantId
        {
            get
            {
                return _TenantId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(TenantId));
                _TenantId = value;
            }
        }

        /// <summary>
        /// User.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string UserId
        {
            get
            {
                return _UserId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(UserId));
                _UserId = value;
            }
        }

        /// <summary>
        /// Plan.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string PlanId
        {
            get
            {
                return _PlanId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(PlanId));
                _PlanId = value;
            }
        }

        /// <summary>
        /// Where it came from.
        /// </summary>
        public PlanSourceEnum Source { get; set; } = PlanSourceEnum.Admin;

        /// <summary>
        /// Promotional code, when redeemed from one. May be null.
        /// </summary>
        public string? PromoCodeId { get; set; } = null;

        /// <summary>
        /// Start.
        /// </summary>
        public DateTime StartsUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// End, or null for no end. May be null.
        /// </summary>
        public DateTime? EndsUtc { get; set; } = null;

        /// <summary>
        /// Revoked. May be null.
        /// </summary>
        public DateTime? RevokedUtc { get; set; } = null;

        /// <summary>
        /// Reason or note. May be null.
        /// </summary>
        public string? Note { get; set; } = null;

        /// <summary>
        /// Administrator who granted it. May be null.
        /// </summary>
        public string? GrantedByUserId { get; set; } = null;

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

        private string _Id = IdGenerator.GeneratePlanAssignmentId();
        private string _TenantId = String.Empty;
        private string _UserId = String.Empty;
        private string _PlanId = String.Empty;

        #endregion
    }
}
