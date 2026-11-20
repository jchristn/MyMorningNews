namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;

    /// <summary>
    /// Filters for listing permissions.
    /// </summary>
    public class PermissionListOptions : ListOptions
    {
        /// <summary>
        /// Include built-in permissions (server default true). Null by default.
        /// </summary>
        public bool? IncludeBuiltIn { get; set; } = null;

        /// <summary>
        /// Only permissions of this type. Null by default.
        /// </summary>
        public PermissionTypeEnum? PermissionType { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public PermissionListOptions()
        {
        }

        /// <summary>
        /// Add this resource's filters.
        /// </summary>
        /// <param name="query">Query to add to.</param>
        protected override void AddFilters(Dictionary<string, string> query)
        {
            base.AddFilters(query);
            QueryFormat.Add(query, "includeBuiltIn", IncludeBuiltIn);
            QueryFormat.Add(query, "permissionType", PermissionType);
        }
    }
}
