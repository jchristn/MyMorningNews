namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// One use of a promotional code.
    /// </summary>
    public class PromoRedemption
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix prr_).
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
        /// Code.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string PromoCodeId
        {
            get
            {
                return _PromoCodeId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(PromoCodeId));
                _PromoCodeId = value;
            }
        }

        /// <summary>
        /// Code as typed.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Code
        {
            get
            {
                return _Code;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Code));
                _Code = value;
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
        /// User email at redemption. May be null.
        /// </summary>
        public string? Email { get; set; } = null;

        /// <summary>
        /// Plan assignment it created.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string? PlanAssignmentId { get; set; } = null;

        /// <summary>
        /// Where it was redeemed.
        /// </summary>
        public PromoChannelEnum Channel { get; set; } = PromoChannelEnum.Account;

        /// <summary>
        /// Revoked by an administrator. May be null.
        /// </summary>
        public DateTime? RevokedUtc { get; set; } = null;

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

        /// <summary>Captured paid offer, or null for a complimentary grant.</summary>
        public string? SignupBenefitId { get; set; } = null;

        #endregion

        #region Private-Members

        private string _Id = IdGenerator.GeneratePromoRedemptionId();
        private string _TenantId = String.Empty;
        private string _PromoCodeId = String.Empty;
        private string _Code = String.Empty;
        private string _UserId = String.Empty;

        #endregion
    }
}
