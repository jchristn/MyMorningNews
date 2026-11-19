namespace MyMorningNews.Core.Models
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Scope assignment for a credential, narrowing it below its owning user.
    /// </summary>
    public class CredentialScopeAssignment
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix csa_).
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
        /// Credential identifier.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string CredentialId
        {
            get
            {
                return _CredentialId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(CredentialId));
                _CredentialId = value;
            }
        }

        /// <summary>
        /// Optional role identifier. May be null.
        /// </summary>
        public string? RoleId { get; set; } = null;

        /// <summary>
        /// Optional built-in role name. May be null.
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
        /// Direct operation grants (role-less).
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
        /// Direct resource-type grants (role-less).
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

        private string _Id = IdGenerator.GenerateCredentialScopeAssignmentId();
        private string _TenantId = String.Empty;
        private string _CredentialId = String.Empty;
        private List<string> _OperationTypes = new List<string>();
        private List<string> _ResourceTypes = new List<string>();

        #endregion
    }
}
