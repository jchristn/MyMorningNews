namespace MyMorningNews.Core.Security
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// One permission inside a built-in role definition.
    /// </summary>
    public class BuiltInPermissionDefinition
    {
        /// <summary>
        /// Permit or deny.
        /// </summary>
        public PermissionTypeEnum PermissionType { get; }

        /// <summary>
        /// Resource types.
        /// </summary>
        public IReadOnlyList<ResourceTypeEnum> ResourceTypes { get; }

        /// <summary>
        /// Operations.
        /// </summary>
        public IReadOnlyList<OperationTypeEnum> Operations { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="permissionType">Permit or deny.</param>
        /// <param name="resourceTypes">Resource types.</param>
        /// <param name="operations">Operations.</param>
        /// <exception cref="ArgumentNullException">A list is null.</exception>
        public BuiltInPermissionDefinition(PermissionTypeEnum permissionType, IReadOnlyList<ResourceTypeEnum> resourceTypes, IReadOnlyList<OperationTypeEnum> operations)
        {
            ArgumentNullException.ThrowIfNull(resourceTypes);
            ArgumentNullException.ThrowIfNull(operations);
            PermissionType = permissionType;
            ResourceTypes = resourceTypes;
            Operations = operations;
        }
    }
}
