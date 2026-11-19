namespace MyMorningNews.Core.Security
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Definition of a built-in role.
    /// </summary>
    public class BuiltInRoleDefinition
    {
        /// <summary>
        /// Role name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Description.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Intended scope.
        /// </summary>
        public ResourceScopeEnum Scope { get; }

        /// <summary>
        /// Whether the role definition is protected from edits.
        /// </summary>
        public bool IsProtected { get; }

        /// <summary>
        /// Permissions.
        /// </summary>
        public IReadOnlyList<BuiltInPermissionDefinition> Permissions { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <param name="description">Description.</param>
        /// <param name="scope">Scope.</param>
        /// <param name="isProtected">Protected.</param>
        /// <param name="permissions">Permissions.</param>
        /// <exception cref="ArgumentNullException">name or permissions is null.</exception>
        public BuiltInRoleDefinition(string name, string description, ResourceScopeEnum scope, bool isProtected, IReadOnlyList<BuiltInPermissionDefinition> permissions)
        {
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
            ArgumentNullException.ThrowIfNull(permissions);
            Name = name;
            Description = description ?? String.Empty;
            Scope = scope;
            IsProtected = isProtected;
            Permissions = permissions;
        }
    }
}
