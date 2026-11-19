namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Operation types used by permissions.
    /// </summary>
    public enum OperationTypeEnum
    {
        /// <summary>
        /// Wildcard.
        /// </summary>
        All,

        /// <summary>
        /// Create.
        /// </summary>
        Create,

        /// <summary>
        /// Read.
        /// </summary>
        Read,

        /// <summary>
        /// Shorthand for Create, Update, and Delete.
        /// </summary>
        Write,

        /// <summary>
        /// Update.
        /// </summary>
        Update,

        /// <summary>
        /// Delete.
        /// </summary>
        Delete,

        /// <summary>
        /// Run or trigger an operation.
        /// </summary>
        Execute,

        /// <summary>
        /// Privileged control-plane operation.
        /// </summary>
        Admin
    }
}
