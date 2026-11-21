namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Create or update a tenant permission.
    /// </summary>
    public class PermissionUpsertRequest
    {
        /// <summary>
        /// Name (required on create).
        /// </summary>
        [TextLimit(128, AllowBlank = false)]
        public string? Name { get; set; } = null;

        /// <summary>
        /// Resource types.
        /// </summary>
        public List<string>? ResourceTypes { get; set; } = null;

        /// <summary>
        /// Operation types.
        /// </summary>
        public List<string>? OperationTypes { get; set; } = null;

        /// <summary>
        /// Permit or Deny.
        /// </summary>
        public PermissionTypeEnum? PermissionType { get; set; } = null;

        /// <summary>
        /// Active.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
