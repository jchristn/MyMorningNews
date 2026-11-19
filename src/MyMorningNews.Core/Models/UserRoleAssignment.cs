namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Assignment of a role to a user at tenant or resource scope.
    /// </summary>
    public class UserRoleAssignment
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix ura_).
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
        /// User identifier.
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
        /// Role identifier; null when RoleName is used. May be null.
        /// </summary>
        public string? RoleId { get; set; } = null;

        /// <summary>
        /// Built-in role name fallback. May be null.
        /// </summary>
        public string? RoleName { get; set; } = null;

        /// <summary>
        /// Tenant or Resource.
        /// </summary>
        public ResourceScopeEnum ResourceScope { get; set; } = ResourceScopeEnum.Tenant;

        /// <summary>
        /// Target resource identifier when resource scoped. May be null.
        /// </summary>
        public string? ResourceId { get; set; } = null;

        /// <summary>
        /// Whether a tenant-scoped grant applies to resources inside the tenant.
        /// </summary>
        public bool InheritsToChildren { get; set; } = true;

        /// <summary>
        /// Protected records cannot be deleted through the API.
        /// </summary>
        public bool IsProtected { get; set; } = false;

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

        private string _Id = IdGenerator.GenerateUserRoleAssignmentId();
        private string _TenantId = String.Empty;
        private string _UserId = String.Empty;

        #endregion
    }
}
