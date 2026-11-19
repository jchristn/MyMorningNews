namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Role grouping permissions. Built-in roles have a null tenant and are protected.
    /// </summary>
    public class UserRole
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix rol_).
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
        /// Role name.
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
        /// Description. May be null.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Seeded by the platform.
        /// </summary>
        public bool IsBuiltIn { get; set; } = false;

        /// <summary>
        /// Protected roles cannot be edited or deleted through the API.
        /// </summary>
        public bool IsProtected { get; set; } = false;

        /// <summary>
        /// Scope the role is intended for.
        /// </summary>
        public ResourceScopeEnum DefaultScope { get; set; } = ResourceScopeEnum.Tenant;

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

        private string _Id = IdGenerator.GenerateUserRoleId();
        private string _Name = String.Empty;

        #endregion
    }
}
