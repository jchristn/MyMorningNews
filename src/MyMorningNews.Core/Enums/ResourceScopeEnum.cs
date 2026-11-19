namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Breadth of a role assignment.
    /// </summary>
    public enum ResourceScopeEnum
    {
        /// <summary>
        /// Applies to the whole tenant.
        /// </summary>
        Tenant,

        /// <summary>
        /// Applies to one resource.
        /// </summary>
        Resource
    }
}
