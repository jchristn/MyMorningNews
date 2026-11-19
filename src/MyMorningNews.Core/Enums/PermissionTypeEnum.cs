namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Permission effect.
    /// </summary>
    public enum PermissionTypeEnum
    {
        /// <summary>
        /// Allows the operation.
        /// </summary>
        Permit,

        /// <summary>
        /// Explicitly denies the operation; wins over Permit.
        /// </summary>
        Deny
    }
}
