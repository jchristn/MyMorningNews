namespace MyMorningNews.Core.Models
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Permission: which operations are permitted or denied on which resource types.
    /// </summary>
    public class Permission
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix prm_).
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
        /// Display name.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Name));
                _Name = value;
            }
        }

        /// <summary>
        /// Resource types covered; ["All"] is a wildcard.
        /// </summary>
        public List<string> ResourceTypes
        {
            get
            {
                return _ResourceTypes;
            }
            set
            {
                _ResourceTypes = value ?? new List<string>();
            }
        }

        /// <summary>
        /// Operation types covered; ["All"] is a wildcard.
        /// </summary>
        public List<string> OperationTypes
        {
            get
            {
                return _OperationTypes;
            }
            set
            {
                _OperationTypes = value ?? new List<string>();
            }
        }

        /// <summary>
        /// Permit or Deny.
        /// </summary>
        public PermissionTypeEnum PermissionType { get; set; } = PermissionTypeEnum.Permit;

        /// <summary>
        /// Seeded by the platform.
        /// </summary>
        public bool IsBuiltIn { get; set; } = false;

        /// <summary>
        /// Protected records cannot be edited or deleted through the API.
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

        private string _Id = IdGenerator.GeneratePermissionId();
        private string _Name = String.Empty;
        private List<string> _ResourceTypes = new List<string>();
        private List<string> _OperationTypes = new List<string>();

        #endregion
    }
}
