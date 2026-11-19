namespace MyMorningNews.Core.Security
{
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// The resource type and operation a request requires.
    /// </summary>
    public class RequiredPermission
    {
        /// <summary>
        /// Resource type.
        /// </summary>
        public ResourceTypeEnum ResourceType { get; }

        /// <summary>
        /// Operation.
        /// </summary>
        public OperationTypeEnum Operation { get; }

        /// <summary>
        /// Whether the request is anonymous (no authorization check).
        /// </summary>
        public bool Anonymous { get; }

        /// <summary>
        /// Whether any authenticated principal may make the request (for example reading one's own token).
        /// </summary>
        public bool AnyAuthenticated { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="resourceType">Resource type.</param>
        /// <param name="operation">Operation.</param>
        /// <param name="anonymous">Anonymous request.</param>
        /// <param name="anyAuthenticated">Any authenticated principal.</param>
        public RequiredPermission(ResourceTypeEnum resourceType, OperationTypeEnum operation, bool anonymous = false, bool anyAuthenticated = false)
        {
            ResourceType = resourceType;
            Operation = operation;
            Anonymous = anonymous;
            AnyAuthenticated = anyAuthenticated;
        }

        /// <summary>
        /// Permission label such as Topic:Read.
        /// </summary>
        /// <returns>Label.</returns>
        public override string ToString()
        {
            return ResourceType.ToString() + ":" + Operation.ToString();
        }
    }
}
