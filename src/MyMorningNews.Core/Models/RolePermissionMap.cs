namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Association between a role and a permission.
    /// </summary>
    public class RolePermissionMap
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix rpm_).
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
        /// Role identifier.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string RoleId
        {
            get
            {
                return _RoleId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(RoleId));
                _RoleId = value;
            }
        }

        /// <summary>
        /// Permission identifier.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string PermissionId
        {
            get
            {
                return _PermissionId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(PermissionId));
                _PermissionId = value;
            }
        }

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

        private string _Id = IdGenerator.GenerateRolePermissionMapId();
        private string _RoleId = String.Empty;
        private string _PermissionId = String.Empty;

        #endregion
    }
}
