namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Interactive user within a tenant.
    /// </summary>
    public class User
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix usr_).
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
        /// First name. May be null.
        /// </summary>
        public string? FirstName { get; set; } = null;

        /// <summary>
        /// Last name. May be null.
        /// </summary>
        public string? LastName { get; set; } = null;

        /// <summary>
        /// Email address, unique within the tenant, used for login.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Email
        {
            get
            {
                return _Email;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Email));
                _Email = value;
            }
        }

        /// <summary>
        /// SHA-256 hash of the password, hex encoded. Never returned by the API.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string PasswordSha256
        {
            get
            {
                return _PasswordSha256;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(PasswordSha256));
                _PasswordSha256 = value;
            }
        }

        /// <summary>
        /// Global administrator: bypasses all tenant and permission checks.
        /// </summary>
        public bool IsAdmin { get; set; } = false;

        /// <summary>
        /// Tenant administrator: bypasses RBAC within the user's own tenant.
        /// </summary>
        public bool IsTenantAdmin { get; set; } = false;

        /// <summary>
        /// Protected records cannot be deleted through the API.
        /// </summary>
        public bool IsProtected { get; set; } = false;

        /// <summary>
        /// Preferred IANA time zone. May be null.
        /// </summary>
        public string? TimeZone { get; set; } = null;

        /// <summary>
        /// Preferred locale (BCP 47), for example en or ja. May be null.
        /// </summary>
        public string? Locale { get; set; } = null;

        /// <summary>
        /// UTC time of the last successful login. May be null.
        /// </summary>
        public DateTime? LastLoginUtc { get; set; } = null;

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

        /// <summary>
        /// Created through public sign-up (plan section 33.1). Default false.
        /// </summary>
        public bool SelfSignup { get; set; } = false;

        /// <summary>
        /// When the email address was verified, or null.
        /// </summary>
        public DateTime? EmailVerifiedUtc { get; set; } = null;

        /// <summary>
        /// Terms of service version accepted at sign-up, or null.
        /// </summary>
        public string? TermsVersion { get; set; } = null;

        /// <summary>
        /// When the terms were accepted, or null.
        /// </summary>
        public DateTime? TermsAcceptedUtc { get; set; } = null;

        /// <summary>
        /// When account deletion was requested (purge follows the grace period), or null.
        /// </summary>
        public DateTime? DeletionRequestedUtc { get; set; } = null;

        #endregion

        #region Private-Members

        private string _Id = IdGenerator.GenerateUserId();
        private string _TenantId = String.Empty;
        private string _Email = String.Empty;
        private string _PasswordSha256 = String.Empty;

        #endregion
    }
}
